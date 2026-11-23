# Read Replica & DataProtection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire DataProtection SSM persistence in Terraform and implement Read Replica routing (`ReadOnlyHushStoreDbContext`) across Terraform, API/Service, EF migrations, and Admin diagnostic endpoints so everything is ready for a single AWS verification window.

**Architecture:** `ReadOnlyHushStoreDbContext` subclasses `HushStoreDbContext` with `NoTracking` and disabled `SaveChanges`, falling back to `DefaultConnection` when `ReadOnlyConnection` is unset, while `StorefrontService` and `AnalyticsService` inject it for read-only workloads. Terraform `modules/ecs` grants `task_app` narrow SSM/KMS permissions + `DataProtection__SsmPrefix` in the same deploy and conditionally injects `ConnectionStrings__ReadOnlyConnection` when a Read Replica exists.

**Tech Stack:** ASP.NET Core 10, EF Core 10 + Npgsql, `Amazon.AspNetCore.DataProtection.SSM`, Terraform (`hashicorp/aws ~> 6.0`).

**Spec:** [docs/superpowers/specs/2026-11-23-read-replica-dataprotection-design.md](../specs/2026-11-23-read-replica-dataprotection-design.md)

## Global Constraints
- Target framework: `net10.0` (`<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`).
- Never leak `ex.Message` in user-facing API responses (`bash devops/scripts/check-error-message-leaks.sh` must output `0` leaks and exit `0`).
- Never use `catch (InvalidOperationException` anywhere in `src/`.
- User-facing error messages must be Vietnamese with diacritics.
- Keep `var dataProtectionSsmPrefix = builder.Configuration["DataProtection:SsmPrefix"];` verbatim in `src/API/Program.cs`.
- Never modify `DenyReadingOurSecrets` on `aws_iam_role.instance` in `infra/tf/modules/ecs/iam.tf`.
- Do NOT run `terraform apply` or touch AWS resources in this phase.

## Review Focus
- Missing `ConnectionStrings:ReadOnlyConnection` (`enable_read_replica = false`) must transparently fall back to `DefaultConnection` without startup failure.
- `dotnet ef migrations bundle` / `script` must pass `--context HushStoreDbContext` because two `DbContext` types now exist in `PBL3.Infrastructure`.
- `GetParametersByPath` IAM policy must allow BOTH the exact base path ARN (`:parameter/<project>/prod/dataprotection`) AND wildcard children (`/*`) without double slashes (`//`).
- `DataProtection__SsmPrefix` and `task_app` IAM permissions must be defined together in Terraform so no deploy window leaves SSM prefix active without permissions.
- `AdminDiagnosticsController` must require `[Authorize(Roles = "Admin")]`, map `unprotect` `CryptographicException` to 400 Vietnamese message, and never expose connection strings, hosts, IPs, or raw exception messages.

---

### Task 1: Read-Only DbContext, DI Fallback, Representative Services & Migration `--context`

**Files:**
- Modify: `src/Infrastructure/Data/HushStoreDbContext.cs:10-16`
- Create: `src/Infrastructure/Data/ReadOnlyHushStoreDbContext.cs`
- Modify: `src/API/appsettings.json:9-11`
- Modify: `src/API/Program.cs:161-175,219-255`
- Modify: `src/Service/Storefront/StorefrontService.cs:11-26`
- Modify: `src/Service/Analytics/AnalyticsService.cs:11-18`
- Modify: `src/Infrastructure/Dockerfile.migrator:39-45`
- Modify: `.github/workflows/deploy.yml:243-248`
- Modify: `CLAUDE.md:25-28`

**Interfaces:**
- Produces: `PBL3.Infrastructure.Data.ReadOnlyHushStoreDbContext` registered as scoped `DbContext` in `src/API/Program.cs`, and `IDataProtectionProvider` with application name `"HushStore"`.

- [ ] **Step 1:** Add `protected HushStoreDbContext(DbContextOptions options) : base(options)` to `src/Infrastructure/Data/HushStoreDbContext.cs`.
- [ ] **Step 2:** Create `src/Infrastructure/Data/ReadOnlyHushStoreDbContext.cs` setting `QueryTrackingBehavior.NoTracking` and overriding all 4 `SaveChanges`/`SaveChangesAsync` overloads to throw `InvalidOperationException`.
- [ ] **Step 3:** Add `"ReadOnlyConnection": ""` to `src/API/appsettings.json`, register `ReadOnlyHushStoreDbContext` in `src/API/Program.cs` (falling back to `DefaultConnection` when blank), and keep `SetApplicationName("HushStore")` unconditional on `AddDataProtection()`.
- [ ] **Step 4:** Switch constructor injection from `HushStoreDbContext` to `ReadOnlyHushStoreDbContext` in `StorefrontService` (leaving `_serialRepo`/`_orderRepo` on primary) and `AnalyticsService`.
- [ ] **Step 5:** Add `--context HushStoreDbContext` to `src/Infrastructure/Dockerfile.migrator`, `.github/workflows/deploy.yml`, and `CLAUDE.md`.

---

### Task 2: Admin Diagnostics DTOs & Controller

**Files:**
- Create: `src/Shared/DTOs/Diagnostics/DiagnosticsDtos.cs`
- Create: `src/API/Controllers/Admin/AdminDiagnosticsController.cs`

**Interfaces:**
- Consumes: `HushStoreDbContext`, `ReadOnlyHushStoreDbContext`, `IDataProtectionProvider`, `IConfiguration`, `ILogger<AdminDiagnosticsController>`.
- Produces:
  - `GET /api/admin/diagnostics/read-replica` → `ApiResult<ReadReplicaDiagnosticResponse>`
  - `POST /api/admin/diagnostics/dataprotection-verify` → `ApiResult<DataProtectionVerifyResponse>`

- [ ] **Step 1:** Create `DiagnosticsDtos.cs` (`ReadReplicaDiagnosticResponse`, `DataProtectionVerifyRequest`, `DataProtectionVerifyResponse`).
- [ ] **Step 2:** Create `AdminDiagnosticsController.cs` with `[Authorize(Roles = "Admin")]`, `SqlQueryRaw<bool>(@"SELECT pg_is_in_recovery() AS ""Value""")`, and `roundtrip` / `protect` / `unprotect` verification using `"HushStore.AdminDiagnostics.v1"`.
- [ ] **Step 3:** Run `bash devops/scripts/check-error-message-leaks.sh` and `grep -rn --exclude-dir=bin --exclude-dir=obj 'catch (InvalidOperationException' src/` — verify `0` leaks and `0` matches.

---

### Task 3: Terraform Wiring (`modules/ecs`, `envs/prod`, `modules/data` comments)

**Files:**
- Modify: `infra/tf/modules/ecs/variables.tf`
- Modify: `infra/tf/modules/ecs/iam.tf`
- Modify: `infra/tf/modules/ecs/taskdef.tf`
- Modify: `infra/tf/envs/prod/main.tf`
- Modify: `infra/tf/modules/data/main.tf:239-258`
- Modify: `infra/tf/modules/data/outputs.tf:64-67`

**Interfaces:**
- Produces: `dataprotection_ssm_prefix` and `ssm_replica_connection_string_arn` inputs on `module "ecs"`, `task_app` narrow SSM/KMS policy, `task_execution_extra` conditional 2→3 SSM ARNs, `api` container env `DataProtection__SsmPrefix` + conditional secret `ConnectionStrings__ReadOnlyConnection`.

- [ ] **Step 1:** Add `dataprotection_ssm_prefix` and `ssm_replica_connection_string_arn` variables with validations to `infra/tf/modules/ecs/variables.tf`.
- [ ] **Step 2:** Add `aws_caller_identity.current`, `DataProtectionKeyRing`, and `DataProtectionKmsViaSsm` to `task_app` and `compact([...])` to `task_execution_extra` in `infra/tf/modules/ecs/iam.tf`.
- [ ] **Step 3:** Add `DataProtection__SsmPrefix` env var and conditional `ConnectionStrings__ReadOnlyConnection` secret to container `api` in `infra/tf/modules/ecs/taskdef.tf` (keeping `migrator` at 2 secrets).
- [ ] **Step 4:** Pass `ssm_replica_connection_string_arn` and `dataprotection_ssm_prefix` from `infra/tf/envs/prod/main.tf` (inherited by `envs/dev/main.tf` symlink) and update stale comments in `modules/data/`.

---

### Task 4: Terraform Tests & Offline Verification

**Files:**
- Modify: `infra/tf/modules/ecs/tests/iam.tftest.hcl`
- Modify: `infra/tf/modules/ecs/tests/taskdef.tftest.hcl`

- [ ] **Step 1:** Update `task_app` security contract assertions in `iam.tftest.hcl` and add negative/positive runs for `dataprotection_ssm_prefix`, `ssm_replica_connection_string_arn`, and 3-ARN `task_execution` when Read Replica is enabled.
- [ ] **Step 2:** Add `api_nhan_dataprotection_ssm_prefix_va_khong_nhan_replica_khi_tat` and `api_nhan_readonly_secret_khi_bat_read_replica_nhung_migrator_van_2_secret` to `taskdef.tftest.hcl`.
- [ ] **Step 3:** Run `terraform -chdir=infra/tf fmt -check -recursive -diff`, `terraform validate` on `modules/ecs`, `modules/data`, `envs/prod`, `envs/dev`, `terraform test` on `modules/ecs` and `modules/data`, Python unit tests, and `check-error-message-leaks.sh`. Verify all pass.
