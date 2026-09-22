# HushStore

**Production-shaped AWS infrastructure for a .NET 10 e-commerce platform — built with Terraform, deployed by GitHub Actions with zero long-lived credentials, and verified by tests and captured evidence rather than by assertion.**

🇻🇳 Bản tiếng Việt: **[README.vi.md](README.vi.md)** · All design documents in [`docs/`](docs/) are written in Vietnamese.

---

| | |
|---|---|
| **Application** | ASP.NET Core 10 Web API · Blazor WebAssembly · PostgreSQL 17 · EF Core |
| **Infrastructure** | Terraform (8 modules, ~6,300 lines HCL) on AWS `ap-southeast-1` |
| **Compute** | ECS on EC2 launch type · 4 container images · one-off tasks for migrate & seed |
| **Network** | 3-tier VPC across 2 AZ, one Network ACL per tier, no public IP on app tier |
| **CI/CD** | GitHub Actions + OIDC — **no AWS access key exists anywhere** |
| **Testing** | 109 `terraform test` assertions across 12 files · 12 security scenarios with raw output committed |
| **Cost control** | Every billable resource behind a toggle · nightly shutdown Lambda · cost telemetry in `status.sh` |

> **Note on placeholders.** This README uses `<AWS_ACCOUNT_ID>`, `<YOUR_IP>` and `<ALERT_EMAIL>` where the working tree holds real values. `terraform.tfvars` is git-ignored by design; only `*.tfvars.example` is committed.

---

## Why this repo is worth reading

Most portfolio projects prove that infrastructure *can be created*. This one is organised around a harder question: **how do you know it is correct, and how do you know it stayed correct?**

Three things follow from that, and they are the parts worth your time:

1. **Security properties live in code, not in a lucky console session.** The entire stack was destroyed on one AWS account (132 resources) and rebuilt from the same Terraform on a blank account. All 12 security scenarios produced identical results — see [`docs/security-validation-report.md`](docs/security-validation-report.md) and the raw command output in [`docs/evidence/`](docs/evidence/).
2. **Cost is treated as a design constraint, not an afterthought.** NAT Gateway and ALB have no free tier and bill by the hour. Every such resource sits behind an explicit toggle that defaults to *off*, and a Lambda enforces the default nightly in case a human forgets.
3. **Claims are qualified.** Where something has been measured, this README says on what date and what configuration. Where it has *not*, it says so — see [Known limitations](#known-limitations). A green `terraform apply` is not evidence that a system works, and this repo does not treat it as such.

---

## Architecture

```
                         Internet
                            │
                   Cloudflare DNS (proxied, Full strict)
                            │
    ┌───────────────────────▼────────────────────────────────────────────┐
    │  VPC 10.20.0.0/16 — 3 tiers × 2 AZ, one Network ACL per tier       │
    │                                                                    │
    │  public tier   ALB (ACM cert, 80→443 redirect)                     │
    │                └─ Host-header allowlist; unknown Host → 403         │
    │                2 × NAT Gateway (one per AZ)                        │
    │                            │                                       │
    │  app tier      EC2 t3.micro — NO public IP, ECS container instance  │
    │                ├─ container  web       nginx :80  (Blazor WASM)    │
    │                ├─ container  api       .NET :8080                  │
    │                └─ one-off    migrator (EF bundle) · seeder (psql)   │
    │                            │                                       │
    │  db tier       RDS PostgreSQL 17 Multi-AZ — isolated,              │
    │                accepts :5432 from app tier only, egress empty      │
    └────────────────────────────────────────────────────────────────────┘
```

### Security group matrix

| Tier | Ingress | Egress |
|---|---|---|
| ALB | 80, 443 ← `0.0.0.0/0` | only to `sg-web` on 80 and 8080 |
| app | 80, 8080 ← **`sg-alb` only** | 5432 → `sg-rds`; 80/443 → internet (ECR, SSM) |
| db  | 5432 ← **`sg-web` only** | **empty** |

**There is no SSH.** No key pair exists and no security group rule opens port 22. Administrative access goes through **SSM Session Manager** (to the host) and **ECS Exec** (into the container). This was verified by scanning the ALB from an external host — see [`docs/evidence/`](docs/evidence/).

**There are no long-lived credentials.** Verified from inside a running container: zero static AWS keys in the environment, zero `.env` files on disk. Credentials arrive from the ECS task role via `AWS_CONTAINER_CREDENTIALS_RELATIVE_URI`; the connection string is injected by ECS from SSM Parameter Store.

---

## Infrastructure as Code

```
infra/tf/
├── bootstrap/              S3 state backend, versioned + encrypted (chicken-and-egg layer)
├── envs/prod/              root module — composes the eight modules below
├── modules/
│   ├── network/            VPC, 3×2 subnets, route tables, NACLs, flow logs, ECR endpoints
│   ├── security/           security groups — the matrix above
│   ├── storage/            4 × ECR repo, S3 assets, S3 ALB logs, S3 artifacts
│   ├── data/               RDS PostgreSQL 17, subnet group, SSM parameters
│   ├── ecs/                cluster, ASG, launch template, 4 task definitions, 2 services, IAM
│   ├── alb/                ALB, ACM cert, listeners, target groups, Host allowlist
│   ├── cicd/               GitHub OIDC provider + 2 IAM roles (deploy, plan)
│   └── costguard/          Lambda + EventBridge Scheduler + SNS + Budgets
└── scripts/                up.sh · down.sh · status.sh · nuke.sh · wait-for-capacity.sh
```

**Testing.** `terraform test` runs **109 `run` blocks across 12 test files** with no AWS credentials and no state access. The assertions encode the security invariants directly — NACLs are stateless so return traffic must be allowed explicitly, no security group may open 22, IAM policies must stay least-privilege. Run them with:

```bash
for m in infra/tf/modules/*/; do terraform -chdir="$m" init -backend=false && terraform -chdir="$m" test; done
```

**Two decisions worth calling out:**

- **`terraform plan` is deliberately absent from PR CI.** `plan` must read state, and Terraform state contains the RDS master password in plaintext (`random_password` always lands in state — that is Terraform's nature, not a misconfiguration here). On a `pull_request` trigger the OIDC `sub` claim cannot distinguish a maintainer's PR from a fork's. Rather than accept a blurred boundary around the DB password, the plan role carries an **explicit `Deny` on `s3:GetObject`** — the constraint lives in IAM, so it holds even if someone later adds a workflow that calls `plan`. The upgrade path (GitHub Environments with required reviewers) is documented in [`.github/workflows/ci.yml`](.github/workflows/ci.yml).
- **Trust policies use `StringEquals`, never `StringLike`.** A wildcard in an OIDC `sub` condition is how a repository named similarly to yours assumes your role.
- **State locking uses the S3 backend's native lockfile** (`use_lockfile = true`, Terraform ≥ 1.10) rather than a DynamoDB table — one less billed resource and one less thing to drift.
- **ECR repositories are `IMMUTABLE` with `scan_on_push`.** Because tags are git SHAs and can never be overwritten, rolling back by pointing at an older task-definition revision is genuinely trustworthy: that revision's image is byte-for-byte what was tested.

---

## CI/CD

Deploy is `git push` to `main`. Two workflows in [`.github/workflows/`](.github/workflows/):

| Workflow | Trigger | Does |
|---|---|---|
| [`ci.yml`](.github/workflows/ci.yml) | PR, push to `main`, manual | `terraform fmt` → `validate` → `test`, and `dotnet build` |
| [`deploy.yml`](.github/workflows/deploy.yml) | push to `main`, manual | build 4 images → generate migration SQL → preflight → migrate → deploy |

**No AWS secret is stored in GitHub.** Each job requests a short-lived OIDC token signed by GitHub; AWS exchanges it for a 1-hour credential. The previous iteration deployed over SSH using an `EC2_SSH_KEY` private key in GitHub Secrets that never expired — removing that was the single largest security improvement in the project.

**Migration is the deploy gate.** It runs as a one-off ECS task. Non-zero exit means no deploy and the previous version keeps serving. `MigrateAsync()` at application startup was deliberately removed from `Program.cs` — running migrations from N application replicas at boot is a race, and it couples schema change to process restart.

**The pipeline will not start infrastructure.** Pushing while the stack is down still succeeds and still pushes all four images to ECR, but stops before deploy and says so in the job summary. Running hours cost real money, so an auto-starting pipeline is an unbounded bill; the deploy role also lacks `autoscaling:SetDesiredCapacity` and `rds:StartDBInstance`, so the restriction is enforced in IAM rather than in YAML.

**Images are tagged by git SHA**, never `latest` — so a running task can always be traced back to a commit, and a rollback is a tag change rather than a rebuild.

---

## Cost engineering

This ran on a shared AWS account with a \$100 credit budget, which turned cost into an engineering constraint rather than a footnote. It is, unexpectedly, the part of the project that produced the most transferable lessons.

- **Every billable resource is behind a toggle defaulting to off.** `enable_nat`, `enable_alb`, `instance_count`, `enable_flow_logs`. The toggles are separate from `max_instance_count`, which is a *ceiling* (free) rather than a *state* (billed) — conflating those two is how a blast radius grows silently.
- **`hushstore-cost-guard`** — a Python Lambda on EventBridge Scheduler, firing at 00:00 ICT. It stops RDS, the EC2 container instance and the ECS services if a human left them running. It deliberately does **not** touch NAT Gateway or ALB: those are Terraform-managed, and deleting them via API would drift state. It also defends against RDS auto-restarting a `stopped` instance after 7 days.
- **Honest accounting of what the guard achieves.** After it runs, the bill drops **~41%**, to roughly \$0.1512/hour — *not* to zero. That percentage fell from 55% earlier in the project, and the reason matters: the guard did not get worse, the part it cannot touch got bigger (2 NAT + ALB = \$0.1432/h, i.e. 95% of the post-guard bill). Getting close to \$0 requires `down.sh`.
- **`status.sh` prints intent beside reality** — the values in `terraform.tfvars` next to what the AWS API actually reports, with a per-resource uptime clock and accrued cost. Because `terraform apply` returning green does not mean the system is usable, this is the script that answers "is it up yet?".

---

## Day-2 operations

Full procedures — start/stop, deploying a version, rollback, seeding, incident diagnosis, cost — are in **[`docs/terraform-runbook.md`](docs/terraform-runbook.md)**.

```bash
bash infra/tf/scripts/up.sh          # bring up enough to serve traffic (~8–12 min)
bash infra/tf/scripts/status.sh -w   # what is running, for how long, at what cost
bash infra/tf/scripts/down.sh        # tear down, then self-verify (~6–8 min)
bash infra/tf/scripts/nuke.sh        # terraform destroy — prompts for confirmation
```

Three constraints that are not optional:

- **Start order is a dependency, not a suggestion.** RDS must reach `available` before the ECS service starts, and `wait-for-capacity.sh` must run after apply — a green apply does not mean the instance has registered with the cluster.
- **Between working windows the domain is intentionally offline.** NAT and ALB are off. This is the default state.
- **Do not enable `enable_read_replica` and walk away.** With a replica present, AWS refuses to stop the primary, which disables both `down.sh` and the cost guard. `down.sh` destroys the replica first; `status.sh` prints a red line when it sees one — but both only run when a human types them.

```bash
# Logs and shell access — no SSH, no certbot (ACM issues and renews TLS at the ALB)
aws logs tail /ecs/hushstore-api --since 15m --follow --profile hushstore
aws ecs execute-command --cluster hushstore --task <arn> --container api \
  --interactive --command /bin/sh --profile hushstore
```

---

## Verification & evidence

| What | Where | Status |
|---|---|---|
| 12 security scenarios (port scan, direct DB access, SSH, IAM blast radius, OIDC spoofing, rate limiting, Host allowlist, NACL, flow logs, cost guard) | [`docs/security-validation-report.md`](docs/security-validation-report.md), raw output in [`docs/evidence/`](docs/evidence/) | 12/12 pass, measured 2026-08-24 — see caveat below |
| Terraform module assertions | [`infra/tf/modules/*/tests/`](infra/tf/) | 109 `run` blocks, executed in CI on every PR |
| Application correctness under concurrency | [`tools/LoadProbe/`](tools/LoadProbe/), [`docs/evidence/loadprobe/`](docs/evidence/loadprobe/) | 9/9 invariants hold at both 1 and 2 instances |
| Rebuild-from-scratch reproducibility | [`docs/security-validation-report.md`](docs/security-validation-report.md) | Stack destroyed (132 resources) and rebuilt on a blank account; identical results |

**`tools/LoadProbe/` is not a load-testing tool.** It fires concurrent requests and then asserts invariants with LINQ against the database, because every data-correctness bug found in this project returned HTTP 200. Vouchers exceeding their limit, duplicated ledger entries, two tickets on one serial — all "succeeded" at the HTTP layer. It also distinguishes `INCONCLUSIVE` from `PASS`: a scenario blocked by the rate limiter satisfies every invariant because the code under test never ran, which is false assurance and worse than no evidence.

---

## Known limitations

Stated plainly, because a DevOps reviewer will find them anyway and because the reasoning is more interesting than a clean-looking list.

- **The 12 security scenarios were measured against SQL Server on port 1433**, before the PostgreSQL 17 migration changed it to 5432 and added a second NAT Gateway plus Multi-AZ. The *shape* of every rule is unchanged ("exactly one DB port, from the app tier only"), so the conclusions almost certainly hold — but *almost certainly* is not *measured*, and this repo does not round that up.
- **The RDS hourly rate (`$0.098`) is a SQL Server measurement** carried forward as an upper bound. PostgreSQL Multi-AZ lists at \$0.051. It stays unchanged until a real invoice confirms it.
- **Single application instance by default.** Running two requires the rate-limit counter to be shared across tasks first; `terraform validate` blocks raising `max_instance_count` without also setting `rate_limiter_is_distributed`, so the unsafe state is not representable. The distributed counter has since been implemented and measured — see [`docs/evidence/2026-09-06-rate-limit-dung-chung.md`](docs/evidence/2026-09-06-rate-limit-dung-chung.md).
- **No automated test suite for application code.** `tools/LoadProbe/` covers concurrency invariants; conventional unit tests do not exist. This is a real gap, not a considered trade-off.
- **A read replica exists in code but nothing reads from it.** Enabling it adds a billed instance without relieving the primary. It is there to demonstrate the topology, not to serve traffic.

---

## Running it locally

```bash
# PostgreSQL 17 — name the service explicitly; the compose file still carries a
# sqlserver service from an earlier iteration
docker compose -f Infrastructure/db/docker-compose.yml up -d postgres

dotnet run --project src/API/API.csproj       # https://localhost:7010
dotnet run --project src/Client/Client.csproj # https://localhost:7107

dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/API
dotnet ef database update      --project src/Infrastructure --startup-project src/API
```

Multi-replica local stack (for anything that only breaks with more than one process — account lockout, boot-time seeding, sessions hopping instances):

```bash
docker compose -f devops/docker/docker-compose.multi.yml up -d   # 2 replicas behind nginx
```

**Deploying to AWS** requires the `hushstore` profile (a plain IAM user — not SSO, so no `aws sso login`):

```bash
aws sts get-caller-identity --profile hushstore
terraform -chdir=infra/tf/envs/prod init
bash infra/tf/scripts/up.sh
```

---

## Repository map

| Path | Contents |
|---|---|
| [`infra/tf/`](infra/tf/) | Terraform — 8 modules, root env, bootstrap, operational scripts |
| [`.github/workflows/`](.github/workflows/) | `ci.yml` (validate + test + build) · `deploy.yml` (build → migrate → deploy) |
| [`devops/`](devops/) | Local compose stacks, nginx config, CI guard scripts |
| [`src/`](src/) | Application — Core · Shared · Infrastructure · Service · API · Client |
| [`tools/LoadProbe/`](tools/LoadProbe/) | Concurrency invariant harness |
| [`docs/`](docs/) | Design, runbook, security report, deployment journal (Vietnamese) |
| [`infra/legacy-cli/`](infra/legacy-cli/) | Previous bash + AWS CLI deployment. **Reference only — do not run.** It creates resources outside Terraform state and opens port 22 with an SSH key pair. |

**Documentation starting points** (all Vietnamese — full index at [`docs/README.md`](docs/README.md)):

- [`docs/thiet-ke-he-thong-aws.md`](docs/thiet-ke-he-thong-aws.md) — *what* and *why*, from networking fundamentals up
- [`docs/nhat-ky-trien-khai.md`](docs/nhat-ky-trien-khai.md) — *how*: 10-phase deployment journal, 12 real incidents with lessons
- [`docs/bao-mat-he-thong.md`](docs/bao-mat-he-thong.md) — 7 defence layers, 12 attacks → blocking layer → fallback, and what the system does **not** stop
- [`docs/terraform-runbook.md`](docs/terraform-runbook.md) — operational procedures
- [`docs/cicd-cho-nguoi-moi.md`](docs/cicd-cho-nguoi-moi.md) — CI/CD from zero: why OIDC over secrets, why tag by git SHA

Architecture diagrams (AWS 2026 icon set, open with [app.diagrams.net](https://app.diagrams.net)): [`docs/diagrams/`](docs/diagrams/) — a 3-page version for slides and a 5-page version with every resource.
