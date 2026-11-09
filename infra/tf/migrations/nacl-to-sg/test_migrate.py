"""Regression cases for refusing unsafe NACL migrations (no AWS calls)."""

import contextlib
from copy import deepcopy
import importlib.util
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("migrate", Path(__file__).with_name("migrate.py"))
migrate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(migrate)


def acl_fixture():
    subnets = {"Subnets": [{"SubnetId": f"subnet-{i}", "VpcId": "vpc-stack"} for i in range(6)]}
    acls = {"NetworkAcls": [{
        "NetworkAclId": "acl-default", "VpcId": "vpc-stack", "IsDefault": True,
        "Entries": [{"Egress": direction, "RuleNumber": 100, "Protocol": "-1",
                     "RuleAction": "allow", "CidrBlock": "0.0.0.0/0"} for direction in (False, True)],
        "Associations": [{"SubnetId": row["SubnetId"]} for row in subnets["Subnets"]],
    }]}
    return acls, subnets


def change(address, actions, before=None, after=None):
    return {"address": address, "mode": "managed",
            "change": {"actions": actions, "before": before, "after": after}}


class MigrationSafetyTests(unittest.TestCase):
    def setUp(self):
        self.output = contextlib.redirect_stdout(io.StringIO())
        self.output.__enter__()

    def tearDown(self):
        self.output.__exit__(None, None, None)

    def test_default_allow_all_and_six_associations_pass(self):
        acls, subnets = acl_fixture()
        migrate.check_acls(acls, subnets, detached=True)

    def test_low_number_deny_prevents_migration(self):
        acls, subnets = acl_fixture()
        acls["NetworkAcls"][0]["Entries"].append({
            "Egress": False, "RuleNumber": 50, "Protocol": "-1",
            "RuleAction": "deny", "CidrBlock": "203.0.113.1/32"})
        with self.assertRaises(ValueError):
            migrate.check_acls(acls, subnets)

    def test_missing_egress_allow_is_rejected(self):
        acls, subnets = acl_fixture()
        acls["NetworkAcls"][0]["Entries"].pop()
        with self.assertRaises(ValueError):
            migrate.check_acls(acls, subnets)

    def test_one_subnet_still_on_custom_acl_blocks_stage_two(self):
        acls, subnets = acl_fixture()
        assoc = acls["NetworkAcls"][0]["Associations"].pop()
        acls["NetworkAcls"].append({"NetworkAclId": "acl-app", "IsDefault": False, "Associations": [assoc]})
        migrate.check_acls(acls, subnets)  # Valid preflight, before stage one.
        with self.assertRaises(ValueError):
            migrate.check_acls(acls, subnets, detached=True)

    def test_missing_or_cross_vpc_subnets_are_rejected(self):
        for variant in ("missing", "cross-vpc"):
            acls, subnets = acl_fixture()
            if variant == "missing":
                subnets["Subnets"].pop()
            else:
                subnets["Subnets"][0]["VpcId"] = "vpc-unrelated"
            with self.subTest(variant=variant), self.assertRaises(ValueError):
                migrate.check_acls(acls, subnets)

    def test_stage_one_allows_only_detach_and_new_sg_rule(self):
        rows = [change(address, ["update"], {"id": address, "subnet_ids": ["subnet-0"]},
                       {"id": address, "subnet_ids": []}) for address in migrate.ACL_ADDRESSES]
        rows.append(change(migrate.NEW_ENDPOINT_RULE, ["create"], after={
            "ip_protocol": "tcp", "from_port": 443, "to_port": 443, "cidr_ipv4": None,
            "security_group_id": "sg-endpoint", "referenced_security_group_id": "sg-web"}))
        plan = {"resource_changes": rows, "planned_values": {"root_module": {"child_modules": [
            {"resources": [{"address": "module.security.aws_security_group.web", "values": {"id": "sg-web"}}]},
            {"resources": [{"address": "module.network.aws_security_group.vpce[0]", "values": {"id": "sg-endpoint"}}]},
        ]}}}
        migrate.check_plan(plan, 1)
        rows[-1]["change"]["after"]["referenced_security_group_id"] = "sg-wrong-source"
        with self.assertRaises(ValueError):
            migrate.check_plan(plan, 1)

    def test_stage_one_rejects_rule_deletion_and_replacement(self):
        for address, actions in [(next(iter(migrate.ACL_ADDRESSES)), ["delete", "create"]),
                                 ('module.network.aws_network_acl_rule.app_ingress["100"]', ["delete"])]:
            with self.subTest(address=address), self.assertRaises(ValueError):
                migrate.check_plan({"resource_changes": [change(address, actions)]}, 1)

    def test_stage_one_rejects_acl_rule_edit_during_detach(self):
        before = {"subnet_ids": ["subnet-0"], "ingress": ["allow"]}
        after = {"subnet_ids": [], "ingress": []}
        with self.assertRaises(ValueError):
            migrate.check_plan({"resource_changes": [change(next(iter(migrate.ACL_ADDRESSES)), ["update"], before, after)]}, 1)

    def test_stage_one_rejects_unknown_inline_rule_change(self):
        row = change("module.network.aws_network_acl.app", ["update"],
                     before={"subnet_ids": ["subnet-0"], "ingress": None},
                     after={"subnet_ids": [], "ingress": None})
        row["change"]["after_unknown"] = {"ingress": True}
        with self.assertRaises(ValueError):
            migrate.check_plan({"resource_changes": [row]}, 1)

    def test_stage_one_rejects_unknown_endpoint_cidr_source(self):
        row = change(migrate.NEW_ENDPOINT_RULE, ["create"], after={
            "ip_protocol": "tcp", "from_port": 443, "to_port": 443,
            "security_group_id": "sg-endpoint", "referenced_security_group_id": "sg-web"})
        row["change"]["after_unknown"] = {"id": True, "cidr_ipv4": True}
        plan = {"resource_changes": [row], "planned_values": {"root_module": {"resources": [
            {"address": "module.security.aws_security_group.web", "values": {"id": "sg-web"}},
            {"address": "module.network.aws_security_group.vpce[0]", "values": {"id": "sg-endpoint"}},
        ]}}}
        with self.assertRaises(ValueError):
            migrate.check_plan(plan, 1)

    def test_stage_two_accepts_only_legacy_removal(self):
        addresses = list(migrate.ACL_ADDRESSES) + [migrate.OLD_ENDPOINT_RULE,
                    'module.network.aws_network_acl_rule.app_ingress["100"]',
                    "module.network.aws_network_acl_rule.public_deny_demo[0]"]
        rows = [change(a, ["delete"], before={"id": a, "subnet_ids": []})
                for a in migrate.ACL_ADDRESSES]
        rows += [change(a, ["delete"], before={"network_acl_id": rows[0]["change"]["before"]["id"]})
                 for a in addresses if a not in migrate.ACL_ADDRESSES]
        migrate.check_plan({"resource_changes": rows}, 2)

    def test_stage_two_refuses_attached_or_unknown_acl_deletion(self):
        for before in (None, {}, {"subnet_ids": None}, {"subnet_ids": ["subnet-live"]}):
            with self.subTest(before=before), self.assertRaises(ValueError):
                migrate.check_plan({"resource_changes": [
                    change("module.network.aws_network_acl.app", ["delete"], before=before)]}, 2)

    def test_stage_two_refuses_rules_without_detached_parent_evidence(self):
        rule = change('module.network.aws_network_acl_rule.app_ingress["100"]', ["delete"],
                      before={"network_acl_id": "acl-app"})
        for before in (None, {"id": "acl-app", "subnet_ids": ["subnet-live"]}):
            rows = [deepcopy(rule)]
            if before:
                rows.append(change("module.network.aws_network_acl.app", ["no-op"], before=before, after=before))
            with self.subTest(before=before), self.assertRaises(ValueError):
                migrate.check_plan({"resource_changes": rows}, 2)

    def test_stage_two_accepts_rule_cleanup_with_detached_parent_in_prior_state(self):
        migrate.check_plan({"resource_changes": [
            change('module.network.aws_network_acl_rule.app_ingress["100"]', ["delete"],
                   before={"network_acl_id": "acl-app"})],
            "prior_state": {"values": {"root_module": {"child_modules": [{"resources": [{
                "address": "module.network.aws_network_acl.app",
                "values": {"id": "acl-app", "subnet_ids": []},
            }]}]}}}}, 2)

    def test_paid_resources_and_new_replacements_are_rejected(self):
        for stage in (1, 2):
            for address in ("module.network.aws_nat_gateway.this[0]", "module.data.aws_db_instance.this",
                            "module.network.aws_security_group.vpce[0]"):
                with self.subTest(stage=stage, address=address), self.assertRaises(ValueError):
                    migrate.check_plan({"resource_changes": [change(address, ["create"])]}, stage)

    def test_incomplete_drift_and_wrong_json_rejected(self):
        for extra in ({"complete": False}, {"errored": True}, {"resource_drift": [{"address": "drift"}]}):
            with self.subTest(extra=extra), self.assertRaises(ValueError):
                migrate.check_plan({"resource_changes": [], **extra}, 1)
        with self.assertRaises(ValueError):
            migrate.check_plan({}, 1)

    def test_prepare_does_not_copy_tfvars_or_state_and_keeps_dev_symlinks(self):
        with tempfile.TemporaryDirectory() as tmp:
            destination = Path(tmp) / "stage1"
            migrate.prepare(destination)
            self.assertTrue((destination / "envs/dev/main.tf").is_symlink())
            self.assertFalse(list(destination.rglob("*.tfvars")))
            self.assertFalse(list(destination.rglob("*.tfstate*")))
            self.assertFalse(list(destination.rglob(".terraform")))
            legacy = (destination / "modules/network/migration_legacy.tf").read_text()
            self.assertEqual(legacy.count("subnet_ids = []"), 3)
            self.assertIn('resource "aws_network_acl_rule" "public_deny_demo"', legacy)
            self.assertIn('resource "aws_vpc_security_group_ingress_rule" "vpce_443"', legacy)
            self.assertIn("my_ip = var.my_ip", (destination / "envs/dev/main.tf").read_text())
            with self.assertRaises(ValueError):
                migrate.prepare(destination)

    def test_stage_two_tree_has_no_transition_resources(self):
        with tempfile.TemporaryDirectory() as tmp:
            destination = Path(tmp) / "stage2"
            migrate.prepare(destination, stage=2)
            for file in (destination / "modules/network").glob("*.tf"):
                self.assertNotIn('resource "aws_network_acl', file.read_text())
                self.assertNotIn('resource "aws_default_network_acl', file.read_text())
            self.assertFalse((destination / "envs/prod/migration_variables.tf").exists())

    def test_prepare_excludes_local_secrets_overrides_and_saved_plans(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / "source"
            migrate.prepare(root, stage=2)
            for name in (".env", "credentials", "config.json", "plan.json", "override.tf", "local_override.tf"):
                (root / "envs/prod" / name).write_text("local sensitive content")
            (root / ".local").mkdir(exist_ok=True)
            (root / ".local" / "apply.log").write_text("sensitive apply log")
            destination = Path(tmp) / "safe-copy"
            with patch.object(migrate, "TF_ROOT", root):
                migrate.prepare(destination, stage=2)
            for name in (".env", "credentials", "config.json", "plan.json", "override.tf", "local_override.tf"):
                self.assertFalse((destination / "envs/prod" / name).exists(), name)
            self.assertFalse((destination / ".local").exists())

    def test_prepare_refuses_external_source_symlinks(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / "source"
            migrate.prepare(root, stage=2)
            external = Path(tmp) / "secret.tf"
            external.write_text("local sensitive content")
            source = root / "envs/prod/variables.tf"
            source.unlink()
            source.symlink_to(external)
            with patch.object(migrate, "TF_ROOT", root), self.assertRaises(ValueError):
                migrate.prepare(Path(tmp) / "unsafe-copy", stage=2)


if __name__ == "__main__":
    unittest.main()
