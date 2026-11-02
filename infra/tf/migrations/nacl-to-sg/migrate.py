#!/usr/bin/env python3
"""Prepare a local transition tree and inspect read-only AWS/plan JSON.

This tool never invokes Terraform or AWS and never copies state or tfvars.
"""

import argparse
import json
import os
from pathlib import Path
import re
import shutil
import sys


HERE = Path(__file__).resolve().parent
TF_ROOT = HERE.parent.parent
ACL_ADDRESSES = {f"module.network.aws_network_acl.{tier}" for tier in ("public", "app", "db")}
OLD_RULE = re.compile(
    r'module\.network\.aws_network_acl_rule\.(?:'
    r'(?:public|app|db)_(?:ingress|egress)\["\d+"\]|public_deny_demo\[0\])$'
)
OLD_ENDPOINT_RULE = "module.network.aws_vpc_security_group_ingress_rule.vpce_443[0]"
NEW_ENDPOINT_RULE = "module.security.aws_vpc_security_group_ingress_rule.vpce_443[0]"

# This one-off migration copies only the reviewed infrastructure sources it
# needs. A suffix blacklist also copies local .env, credentials, plan JSON,
# Terraform overrides and runtime logs, so it cannot protect secret material.
MODULE_FILES = {
    "alb": ["acm.tf", "alb.tf"],
    "cicd": ["main.tf", "policy.tf"],
    "costguard": ["main.tf", "lambda.tf", "schedule.tf", "src/cost_guard.py"],
    "data": ["main.tf"],
    "ecs": ["cluster.tf", "iam.tf", "service.tf", "taskdef.tf", "user_data.sh.tftpl"],
    "network": ["vpc.tf"],
    "security": ["main.tf", "cloudflare-ipv4.txt"],
    "storage": ["ecr.tf", "s3.tf"],
}
SOURCE_FILES = {
    Path("modules") / module / filename
    for module, filenames in MODULE_FILES.items()
    for filename in filenames + ["outputs.tf", "variables.tf", "versions.tf"]
} | {
    Path("envs") / env / filename
    for env in ("prod", "dev")
    for filename in ("backend.tf", "main.tf", "outputs.tf", "providers.tf", "variables.tf", ".terraform.lock.hcl")
}


def prepare(destination, stage=1):
    destination = Path(destination).resolve()
    source_root = TF_ROOT.resolve()
    if destination == source_root or source_root in destination.parents:
        raise ValueError("Destination must be outside infra/tf to avoid recursive copying.")
    if destination.exists():
        raise ValueError("Destination already exists; choose a new directory.")

    if (source_root / "modules/network/nacl.tf").exists():
        raise ValueError("Prepare from the final SG-only configuration, not the old stack.")
    for relative in SOURCE_FILES:
        source = source_root / relative
        resolved = source.resolve()
        if not source.is_file() or not resolved.is_relative_to(source_root):
            raise ValueError(f"Missing or external migration source: {relative}")
        if source.is_symlink() and (Path(os.readlink(source)).is_absolute()
                                    or resolved.relative_to(source_root) not in SOURCE_FILES):
            raise ValueError(f"Unsafe migration source symlink: {relative}")

    destination.mkdir(mode=0o700, parents=True)
    for relative in sorted(SOURCE_FILES):
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_root / relative, target, follow_symlinks=False)
    network = destination / "modules/network"
    if stage == 2:
        print(f"Prepared stage 2: {destination}")
        print("No state, credentials or tfvars copied. No Terraform/AWS command executed.")
        return
    shutil.copyfile(HERE / "legacy-network.tf.in", network / "migration_legacy.tf")
    # dev/main.tf is a relative symlink to prod/main.tf; edit the target once.
    main = destination / "envs/prod/main.tf"
    text = main.read_text()
    marker = 'module "network" {\n'
    if text.count(marker) != 1:
        raise ValueError("Expected one network module block in prod/main.tf.")
    main.write_text(text.replace(marker, marker + "  my_ip = var.my_ip\n  enable_deny_demo = var.enable_deny_demo\n", 1))
    variables = '''# Transition only: retain existing deny-demo values until detached.
variable "my_ip" {
  type    = string
  default = null
}
variable "enable_deny_demo" {
  type    = bool
  default = false
}
'''
    for env in ("prod", "dev"):
        (destination / "envs" / env / "migration_variables.tf").write_text(variables)
    print(f"Prepared stage 1: {destination}")
    print("No state, credentials or tfvars copied. No Terraform/AWS command executed.")


def check_acls(acls, subnets, detached=False):
    subnet_rows = subnets.get("Subnets", [])
    ids = {s["SubnetId"] for s in subnet_rows}
    vpcs = {s["VpcId"] for s in subnet_rows}
    if len(ids) != 6 or len(subnet_rows) != 6 or len(vpcs) != 1:
        raise ValueError("Expected exactly six distinct stack subnets in one VPC.")
    vpc_id = next(iter(vpcs))
    rows = acls.get("NetworkAcls", [])
    defaults = [a for a in rows if a.get("IsDefault") and a.get("VpcId") == vpc_id]
    if len(defaults) != 1:
        raise ValueError("Expected exactly one default NACL in the stack VPC.")
    default = defaults[0]
    for egress in (False, True):
        ipv4 = sorted((e for e in default.get("Entries", []) if e.get("Egress") == egress and "CidrBlock" in e),
                      key=lambda e: e["RuleNumber"])
        # Reject custom lower-priority rules too: expected AWS default, not a
        # superficially similar ACL that will drift from the documented policy.
        ordinary = [e for e in ipv4 if e["RuleNumber"] != 32767]
        if len(ordinary) != 1 or not all((ordinary[0].get("RuleNumber") == 100,
                                        ordinary[0].get("Protocol") == "-1",
                                        ordinary[0].get("RuleAction") == "allow",
                                        ordinary[0].get("CidrBlock") == "0.0.0.0/0")):
            raise ValueError("Default NACL must have only rule 100 ALLOW all IPv4 in each direction; stop before detach.")
    if detached:
        associations = [(a["NetworkAclId"], assoc["SubnetId"]) for a in rows
                        for assoc in a.get("Associations", []) if assoc.get("SubnetId") in ids]
        if len(associations) != 6 or {s for _, s in associations} != ids or any(
                acl_id != default["NetworkAclId"] for acl_id, _ in associations):
            raise ValueError("All six stack subnets must be associated only with the default NACL before stage 2.")
    print("Default NACL IPv4 allow-all verified" + ("; all six subnets detached." if detached else "."))


def check_plan(plan, stage):
    if plan.get("errored") or plan.get("complete") is False or plan.get("deferred_changes"):
        raise ValueError("Incomplete/errored/deferred plan cannot authorize migration.")
    if "resource_changes" not in plan:
        raise ValueError("Expected terraform show -json plan output with resource_changes.")
    # Drift is not a migration change. Resolve it in a separate reviewed plan.
    if plan.get("resource_drift"):
        raise ValueError("Plan contains resource drift; resolve it separately before migration.")
    def resources(module):
        result = {r["address"]: r.get("values", {}) for r in module.get("resources", [])}
        for child in module.get("child_modules", []):
            result.update(resources(child))
        return result

    def has_unknown(value):
        if isinstance(value, dict):
            return any(has_unknown(item) for item in value.values())
        if isinstance(value, list):
            return any(has_unknown(item) for item in value)
        return value is True

    values = resources(plan.get("planned_values", {}).get("root_module", {}))
    prior = resources(plan.get("prior_state", {}).get("values", {}).get("root_module", {}))
    # Delete plans must prove detachment in refreshed prior values as well as
    # the separately required live AWS check. Never infer it from after=null.
    for resource in plan["resource_changes"]:
        if resource["address"] in ACL_ADDRESSES and resource["change"].get("before") is not None:
            prior[resource["address"]] = resource["change"]["before"]
    changes = 0
    for resource in plan["resource_changes"]:
        action = resource["change"]["actions"]
        if action == ["no-op"] or resource.get("mode") == "data" and action == ["read"]:
            continue
        address = resource["address"]
        change = resource["change"]
        permitted = False
        if stage == 1 and address in ACL_ADDRESSES and action == ["update"]:
            before, after = change["before"], change["after"]
            keys = set(before) | set(after)
            differing = {key for key in keys if before.get(key) != after.get(key)}
            permitted = (differing == {"subnet_ids"} and after["subnet_ids"] == []
                         and not has_unknown(change.get("after_unknown", {})))
        elif stage == 1 and address == NEW_ENDPOINT_RULE and action == ["create"]:
            after = change["after"]
            source_id = values.get("module.security.aws_security_group.web", {}).get("id")
            target_id = values.get("module.network.aws_security_group.vpce[0]", {}).get("id")
            security_fields = ("ip_protocol", "from_port", "to_port", "cidr_ipv4", "cidr_ipv6",
                               "prefix_list_id", "security_group_id", "referenced_security_group_id")
            permitted = (after.get("ip_protocol") == "tcp" and after.get("from_port") == 443
                         and after.get("to_port") == 443 and after.get("cidr_ipv4") is None
                         and after.get("cidr_ipv6") is None and after.get("prefix_list_id") is None
                         and bool(source_id) and bool(target_id)
                         and after.get("security_group_id") == target_id
                         and after.get("referenced_security_group_id") == source_id
                         and not any(has_unknown(change.get("after_unknown", {}).get(key))
                                     for key in security_fields))
        elif stage == 2 and action == ["delete"]:
            if address in ACL_ADDRESSES:
                permitted = (change.get("before") or {}).get("subnet_ids") == []
            elif OLD_RULE.fullmatch(address):
                parent_id = (change.get("before") or {}).get("network_acl_id")
                permitted = bool(parent_id) and any(
                    address in ACL_ADDRESSES and acl.get("id") == parent_id and acl.get("subnet_ids") == []
                    for address, acl in prior.items())
            else:
                permitted = address == OLD_ENDPOINT_RULE
        if not permitted:
            raise ValueError(f"Unexpected stage-{stage} change: {address}: {action}. No apply.")
        changes += 1
    print(f"Stage {stage}: {changes} allowed resource changes; no unrelated create/update/replacement.")


def read_json(filename):
    return json.loads(Path(filename).read_text())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    prepare_parser = commands.add_parser("prepare")
    prepare_parser.add_argument("destination")
    prepare_parser.add_argument("--stage", type=int, choices=[1, 2], default=1)
    acl_parser = commands.add_parser("check-acls")
    acl_parser.add_argument("acls_json")
    acl_parser.add_argument("subnets_json")
    acl_parser.add_argument("--detached", action="store_true")
    plan_parser = commands.add_parser("check-plan")
    plan_parser.add_argument("stage", type=int, choices=[1, 2])
    plan_parser.add_argument("plan_json")
    args = parser.parse_args()
    try:
        if args.command == "prepare":
            prepare(args.destination, args.stage)
        elif args.command == "check-acls":
            check_acls(read_json(args.acls_json), read_json(args.subnets_json), args.detached)
        else:
            check_plan(read_json(args.plan_json), args.stage)
    except (ValueError, KeyError, TypeError, OSError) as error:
        print(f"STOP: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
