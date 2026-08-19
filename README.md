# HushStore — IT Hardware E-commerce & Management System

Hệ thống thương mại điện tử và quản lý phần cứng IT, xây dựng trên ASP.NET Core 10 (API) + Blazor WebAssembly (Frontend) + SQL Server 2025.

---

## Kiến trúc

Hạ tầng dựng hoàn toàn bằng **Terraform** (`infra/tf/`), chạy trên **ECS EC2
launch type** — container thật, nhưng vẫn là EC2 instance thật.

    Internet
      │
      ├── Cloudflare DNS (proxied, Full strict)
      │
      ▼
    VPC 10.20.0.0/16 — 3 tier × 2 AZ, mỗi tier một Network ACL riêng
      │
      ├── public tier   ALB (ACM cert, listener 80→443) + NAT Gateway
      │                 └── allowlist Host header; Host lạ → 403
      │
      ├── app tier      EC2 t3.micro, KHÔNG public IP, ECS container instance
      │                 ├── container web  nginx :80   (Blazor WASM bake trong image)
      │                 ├── container api  .NET :8080
      │                 └── one-off task   migrator (EF bundle) · seeder (sqlcmd)
      │
      └── db tier       RDS SQL Server Express, isolated, chỉ nhận :1433 từ app tier

**Không có SSH.** Hệ thống không có key pair nào và không có Security Group rule
nào mở port 22. Truy cập quản trị đi qua **SSM Session Manager** (vào host) và
**ECS Exec** (vào trong container).

**Không có credential dài hạn nào.** Đã kiểm chứng từ bên trong container đang
chạy: 0 static AWS key trong env, 0 file `.env` trên disk; credential đến từ ECS
task role qua `AWS_CONTAINER_CREDENTIALS_RELATIVE_URI`, connection string do ECS
inject từ SSM Parameter Store.

| Tier | Rule vào | Rule ra |
|---|---|---|
| ALB | 80, 443 ← `0.0.0.0/0` | chỉ tới `sg-web` cổng 80 và 8080 |
| app | 80, 8080 ← **chỉ từ `sg-alb`** | 1433 → `sg-rds`, 80/443 → internet (ECR, SSM) |
| db | 1433 ← **chỉ từ `sg-web`** | **rỗng** |

---

## Deploy lên AWS

Toàn bộ quy trình nằm ở **[docs/terraform-runbook.md](docs/terraform-runbook.md)**
— bật/tắt, deploy phiên bản mới, rollback, seed, chẩn đoán sự cố, và chi phí.

Ba điều cần biết trước khi chạy bất cứ thứ gì:

**Mặc định NAT Gateway và ALB đều tắt.** Cả hai tính theo giờ và không có bậc free
tier. Bật khi làm việc, tắt ngay khi xong. Giữa hai cửa sổ làm việc, domain cố ý
không hoạt động.

**Thứ tự bật là ràng buộc, không phải khuyến nghị.** RDS phải `available` trước
khi bật ECS service, và phải chạy `infra/tf/scripts/wait-for-capacity.sh` sau khi
apply — `terraform apply` xanh không có nghĩa là instance đã đăng ký vào cluster.
Runbook giải thích vì sao.

**Migration là gate của deploy.** Nó chạy như một one-off ECS task; exit code khác
0 thì không deploy, bản cũ vẫn phục vụ. `MigrateAsync()` lúc app khởi động đã bị
xoá khỏi `Program.cs`.

```bash
aws sso login --profile hushstore
cd infra/tf/envs/prod
terraform init
# rồi theo đúng thứ tự trong runbook
```

Thư mục **[infra/legacy-cli/](infra/legacy-cli/)** chứa bộ script bash + AWS CLI
của kỳ trước, giữ lại làm spec tham chiếu. **Đừng chạy lại chúng** — chúng tạo
resource nằm ngoài Terraform state, và chúng mở port 22 kèm SSH key pair.

---

## Development (local)

```bash
# Khởi động SQL Server
docker compose -f Infrastructure/db/docker-compose.yml up -d

# Chạy API (https://localhost:7010)
dotnet run --project src/API/API.csproj

# Chạy Blazor WASM (https://localhost:7107)
dotnet run --project src/Client/Client.csproj

# Tạo migration mới
dotnet ef migrations add <TênMigration> --project src/Infrastructure --startup-project src/API

# Áp dụng migrations
dotnet ef database update --project src/Infrastructure --startup-project src/API
```

---

## Troubleshooting

### Trên AWS

Xem bảng "Sự cố thường gặp" trong
**[docs/terraform-runbook.md](docs/terraform-runbook.md)** — nó liệt kê hiện
tượng, nguyên nhân và cách xử lý cho 13 sự cố đã gặp thật, kèm cả hai trường hợp
dễ đọc sai: gọi ALB bằng tên DNS thô trả **403** là *đúng thiết kế* (allowlist Host
header), còn trả **503** mới là lỗi.

Log và cách vào hệ thống:

```bash
aws logs tail /ecs/hushstore-api --since 15m --follow --profile hushstore
aws ecs execute-command --cluster hushstore --task <arn> --container api \
  --interactive --command /bin/sh --profile hushstore
```

Không dùng `ssh` và không dùng `certbot` — TLS do ACM cấp và ALB terminate, gia
hạn tự động.

### Local

**API không start:**
```bash
docker compose -f Infrastructure/db/docker-compose.yml ps
dotnet run --project src/API/API.csproj
```

**Lỗi kết nối SQL Server:**
```bash
docker compose -f Infrastructure/db/docker-compose.yml logs --tail=50
```

Nếu mật khẩu trong `.env` không có tác dụng, khả năng cao volume cũ vẫn còn dữ
liệu của lần chạy trước — `MSSQL_SA_PASSWORD` chỉ có tác dụng khi khởi tạo volume
mới.

**Schema chưa có:**
```bash
dotnet ef database update --project src/Infrastructure --startup-project src/API
```
