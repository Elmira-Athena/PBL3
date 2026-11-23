# Thiết kế hoàn thiện Read Replica (App + Terraform) & DataProtection (Terraform)

**Ngày:** 2026-11-23  
**Trạng thái:** Đã chốt thiết kế với chủ dự án; triển khai mã nguồn + test local/CI trước, chưa mở cửa sổ AWS (`terraform apply` để gộp vào 1 cửa sổ test hệ thống sau).

---

## 1. Mục tiêu và phạm vi

### Mục tiêu
1. Nối hoàn chỉnh **DataProtection key ring qua AWS SSM Parameter Store** giữa tầng ứng dụng (đã viết sẵn trong `src/API/Program.cs`) và tầng hạ tầng Terraform (`infra/tf/modules/ecs/` + `envs/prod/`), đảm bảo IAM và env var đi **cùng một lần deploy**.
2. Hoàn thiện **Read Replica** từ đầu đến cuối ở cả tầng hạ tầng (`ConnectionStrings__ReadOnlyConnection` có điều kiện theo `enable_read_replica`) lẫn tầng ứng dụng (`ReadOnlyHushStoreDbContext` dùng cho nhóm API đọc tiêu biểu), tự động **fallback về primary** khi `enable_read_replica = false`.
3. Bổ sung **2 endpoint chẩn đoán chỉ dành cho Admin** (`/api/admin/diagnostics/*`) để trong 1 cửa sổ AWS sắp tới có thể xác minh nhanh bằng `curl` rằng query đọc thực sự đi vào Read Replica (`pg_is_in_recovery() = true`) và DataProtection đọc/ghi được key ring thật trên SSM mà không cần build lại image tạm.

### Ngoài phạm vi
- Không bật `enable_read_replica = true` hoặc `terraform apply` trong đợt này (chưa mở AWS).
- Không chuyển toàn bộ 20+ service sang replica; chỉ chuyển **2 service đọc tiêu biểu** (`StorefrontService`, `AnalyticsService`).
- Không thay đổi cấu hình autoscaling (2 instance) hay Redis cache.

---

## 2. Các phương án định tuyến Read Replica đã cân nhắc

- **Bị loại — Tự động bắt `SELECT` qua `DbConnectionInterceptor` trên một `DbContext` duy nhất:**  
  Vi phạm trực tiếp hai bất biến an toàn của hệ thống: (1) middleware kiểm tra `IsActive`, Identity và các bước đọc-rồi-ghi trong transaction **tuyệt đối không được** đọc từ replica bất đồng bộ vì trễ replication có thể mở lại tài khoản vừa khoá hoặc đọc dữ liệu cũ; (2) khó kiểm soát tường minh điểm nào đọc replica.
- **Phương án B — Lớp bọc `IReadOnlyDbContext` tự quản lý vòng đời `DbContext`:**  
  Tránh được việc khai báo `DbContext` thứ hai (không cần `--context`), nhưng phải phơi bày thủ công từng `IQueryable<T>` và tách khỏi chuẩn `AddDbContext` / retry / DI scope của ASP.NET Core.
- **Phương án A (Được chọn) — `ReadOnlyHushStoreDbContext : HushStoreDbContext`:**  
  Tái sử dụng toàn bộ `OnModelCreating` (`citext`, query filter `IsDeleted`, cấu hình quan hệ), dùng chung `AddDbContext` + `EnableRetryOnFailure`, mặc định `NoTracking`, khoá cứng `SaveChanges`/`SaveChangesAsync` ném `InvalidOperationException` để chống ghi nhầm vào replica. Tách biệt rạch ròi bằng kiểu inject ở constructor service: chỉ service nào nhận `ReadOnlyHushStoreDbContext` mới đi replica.

---

## 3. Kiến trúc tầng ứng dụng (.NET 10)

### 3.1. `ReadOnlyHushStoreDbContext` & Đăng ký DI
- `src/Infrastructure/Data/HushStoreDbContext.cs`: thêm constructor `protected HushStoreDbContext(DbContextOptions options) : base(options)` để lớp con truyền `DbContextOptions<ReadOnlyHushStoreDbContext>`.
- `src/Infrastructure/Data/ReadOnlyHushStoreDbContext.cs`:
  - Kế thừa `HushStoreDbContext`.
  - Constructor đặt `ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;`.
  - Override cả 4 overload của `SaveChanges` và `SaveChangesAsync` để ném `InvalidOperationException`.
- `src/API/appsettings.json`: thêm `"ReadOnlyConnection": ""` dưới `ConnectionStrings`.
- `src/API/Program.cs`:
  - Đăng ký `AddDbContext<ReadOnlyHushStoreDbContext>`: đọc `ConnectionStrings:ReadOnlyConnection`, nếu rỗng/null thì **fallback về `ConnectionStrings:DefaultConnection`**. Dùng cùng `EnableRetryOnFailure(3, 5s)` và `ConfigureWarnings` như primary, cộng `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)`.
  - DataProtection: luôn gọi `AddDataProtection().SetApplicationName("HushStore")` để thống nhất application name trong DI; nếu `DataProtection:SsmPrefix` khác rỗng thì đăng ký `IAmazonSimpleSystemsManagement` theo `AwsSettings:Region` (mặc định `ap-southeast-1`) và gọi `.PersistKeysToAWSSystemsManager(dataProtectionSsmPrefix)`.
  - **Giữ nguyên trên primary (`HushStoreDbContext`):** Identity (`AddEntityFrameworkStores`), middleware kiểm tra `IsActive`, `PostgresRateLimitStore`, `/health/ready` (`AddDbContextCheck<HushStoreDbContext>`), và mọi repository ghi.

### 3.2. Nhóm API tiêu biểu đi qua Read Replica
- **`StorefrontService`** (`GET /api/storefront/*`): đổi trường `_context` và tham số constructor sang `ReadOnlyHushStoreDbContext`. Riêng `GetVirtualStockMapAsync` vẫn gọi `_serialRepo` và `_orderRepo` (bám primary) nên tồn kho giữ chính xác thời gian thực.
- **`AnalyticsService`** (`GET /api/analytics/*`): đổi trường `_context` và tham số constructor sang `ReadOnlyHushStoreDbContext` cho cả 6 báo cáo tổng hợp (`summary`, `revenue-trend`, `order-channels`, `top-products`, `category-revenue`, `inventory-summary`).

### 3.3. EF Core Migrations (`--context HushStoreDbContext`)
Vì assembly có 2 `DbContext`, mọi lệnh `dotnet ef` phải chỉ định tường minh `--context HushStoreDbContext`:
- `src/Infrastructure/Dockerfile.migrator` (`dotnet ef migrations bundle`)
- `.github/workflows/deploy.yml` (`dotnet ef migrations script --idempotent`)
- `CLAUDE.md` (các lệnh mẫu `dotnet ef migrations add` / `database update`)

---

## 4. Endpoint chẩn đoán Admin (`AdminDiagnosticsController`)

- Namespace: `PBL3.API.Controllers.Admin`, `[Authorize(Roles = "Admin")]`, `[Route("api/admin/diagnostics")]`, DTO tại `src/Shared/DTOs/Diagnostics/DiagnosticsDtos.cs`.
- **`GET /api/admin/diagnostics/read-replica`:**
  - Chạy `SELECT pg_is_in_recovery() AS "Value"` qua `SqlQueryRaw<bool>` trên cả `ReadOnlyHushStoreDbContext` và `HushStoreDbContext`.
  - Trả về `ReadReplicaDiagnosticResponse`: `ReadOnlyConfigured` (`bool`), `ReadOnlyContextInRecovery` (`bool`), `PrimaryContextInRecovery` (`bool`). **Không lộ** connection string, hostname hay IP.
- **`POST /api/admin/diagnostics/dataprotection-verify`:**
  - Nhận `DataProtectionVerifyRequest { Mode, Payload }` (`Mode`: `"roundtrip"` | `"protect"` | `"unprotect"`, `Payload`: bắt buộc, tối đa 2048 ký tự).
  - Dùng `IDataProtectionProvider.CreateProtector("HushStore.AdminDiagnostics.v1")`.
  - `roundtrip`: mã hóa rồi giải mã lại (vừa kích hoạt ghi key ring thật lên SSM, vừa kiểm tra đọc/giải mã).
  - `protect` / `unprotect`: hỗ trợ kiểm thử giải mã xuyên task khi chạy 2 instance.
  - Bắt `CryptographicException` ở nhánh `unprotect` → trả `400` tiếng Việt cố định; lỗi SSM/KMS hoặc DB → ghi log qua `ILogger` và trả `500` tiếng Việt cố định. Không nối `ex.Message` vào phản hồi (`check-error-message-leaks.sh` phải ra `0`), không dùng `catch (InvalidOperationException`.

---

## 5. Kiến trúc tầng hạ tầng (Terraform)

- **`infra/tf/modules/ecs/variables.tf`:**
  - `dataprotection_ssm_prefix` (`string`, mặc định `""`; khi khác rỗng bắt buộc khớp `^/[a-z0-9-]+/[a-z0-9-]+/dataprotection/$` để cấm tuyệt đối prefix rộng `/hushstore/prod/`).
  - `ssm_replica_connection_string_arn` (`string`, mặc định `""`; khi khác rỗng bắt buộc khớp ARN kết thúc bằng `/connection-string-readonly`).
- **`infra/tf/modules/ecs/iam.tf`:**
  - Thêm `data "aws_caller_identity" "current" {}`.
  - `task_app` được bổ sung 2 statement:
    - `DataProtectionKeyRing`: `ssm:GetParametersByPath`, `ssm:PutParameter` trên **đúng 2 resource ARN** là path gốc `parameter/<prefix-không-dấu-/>` (tránh `AccessDeniedException` khi gọi `GetParametersByPath`) và `parameter/<prefix-không-dấu-/>/*`. Không cấp `DescribeParameters`, `GetParameter`, `GetParameters`.
    - `DataProtectionKmsViaSsm`: `kms:Decrypt`, `kms:Encrypt` trên `*` kèm điều kiện `kms:ViaService = ssm.<region>.amazonaws.com`.
  - `task_execution_extra` dùng `compact([var.ssm_connection_string_arn, var.ssm_jwt_secret_arn, var.ssm_replica_connection_string_arn])` → giữ đúng 2 ARN khi replica tắt, tự thêm ARN readonly khi replica bật.
  - **Không sửa** khối `DenyReadingOurSecrets` của `aws_iam_role.instance`: pattern `parameter/${var.project}/*` tự động chặn luôn EC2 host đọc key ring DataProtection.
- **`infra/tf/modules/ecs/taskdef.tf`:**
  - Container `api` nhận `DataProtection__SsmPrefix` trong `environment` (cùng 1 lần apply với quyền IAM `task_app`).
  - Container `api` dùng `local.api_secrets`: gắn thêm `{ name = "ConnectionStrings__ReadOnlyConnection", valueFrom = var.ssm_replica_connection_string_arn }` **chỉ khi** `var.ssm_replica_connection_string_arn != ""`.
  - Container `migrator` giữ nguyên `local.app_secrets` (đúng 2 secret, không chạm replica).
- **`infra/tf/envs/prod/main.tf`** (tự áp cho `envs/dev` qua symlink):
  - Truyền `ssm_replica_connection_string_arn = module.data.ssm_replica_connection_string_arn` và `dataprotection_ssm_prefix = "${module.data.ssm_path_prefix}/dataprotection/"`.
- **Terraform tests (`modules/ecs/tests/iam.tftest.hcl`, `taskdef.tftest.hcl`):**
  - Cập nhật contract `task_app` (khẳng định prefix hẹp, có cả base path & `/*`, không có `DescribeParameters`/`GetParameter`, KMS bắt buộc `ViaService`).
  - Thêm ca đối chứng âm từ chối `dataprotection_ssm_prefix = "/hushstore/prod/"` và `ssm_replica_connection_string_arn` trỏ nhầm `db-password`.
  - Kiểm chứng 2 chế độ: replica tắt (`api` 2 secrets, execution 2 ARN) và replica bật (`api` 3 secrets, execution 3 ARN, `migrator` vẫn 2 secrets).

---

## 6. Kịch bản kiểm chứng cho cửa sổ AWS sắp tới

> Thực hiện cùng lúc khi mở cửa sổ AWS tổng hợp (sau khi code đã merge `main` và CI build/push image SHA mới lên ECR):

1. Cập nhật `infra/tf/envs/prod/terraform.tfvars`: đặt `image_tag = "<git-sha>"` và `enable_read_replica = true`.
2. Chạy `terraform -chdir=infra/tf/envs/prod apply` (tạo replica + parameter `connection-string-readonly` + cập nhật IAM `task_app`/`task_execution` + taskdef `api`), rồi chạy `bash infra/tf/scripts/up.sh`.
3. Đăng nhập admin lấy JWT `TOK`, kiểm tra **Read Replica**:
   ```bash
   curl -sS -H "Authorization: Bearer $TOK" https://api.hushstore.io.vn/api/admin/diagnostics/read-replica
   # Kỳ vọng: readOnlyConfigured=true, readOnlyContextInRecovery=true, primaryContextInRecovery=false
   curl -sS https://api.hushstore.io.vn/api/storefront/categories | head -c 200
   curl -sS https://api.hushstore.io.vn/api/storefront/products/featured | head -c 200
   curl -sS -H "Authorization: Bearer $TOK" "https://api.hushstore.io.vn/api/analytics/summary?from=2026-01-01&to=2026-12-31" | head -c 200
   ```
4. Kiểm tra **DataProtection → SSM**:
   ```bash
   curl -sS -X POST -H "Authorization: Bearer $TOK" -H "Content-Type: application/json" \
     -d '{"mode":"roundtrip","payload":"hushstore-ssm-probe"}' \
     https://api.hushstore.io.vn/api/admin/diagnostics/dataprotection-verify
   # Kỳ vọng: ssmPrefixConfigured=true, roundTripMatched=true
   aws ssm get-parameters-by-path --path "/hushstore/prod/dataprotection/" --profile hushstore --region ap-southeast-1 --query 'Parameters[].{Name:Name,Type:Type}'
   # Kỳ vọng: thấy ít nhất 1 SecureString key-<guid>.xml
   ```
5. Nếu bật 2 task để test xuyên instance: gọi `mode: "protect"` lấy `protectedPayload`, gửi sang `mode: "unprotect"` nhiều lần để xác nhận task khác giải mã được.
6. **Bắt buộc sau khi đo xong:** chạy `bash infra/tf/scripts/down.sh` (tự đặt `enable_read_replica = false`, huỷ replica trước khi stop primary) và kiểm bằng `bash infra/tf/scripts/status.sh` (ALB=0, NAT=0, EC2=0, replica=0, primary=`stopped`).
