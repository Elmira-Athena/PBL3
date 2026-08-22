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

## Tài liệu cho người mới

Hai tài liệu dành cho thành viên chưa dùng AWS, đọc theo thứ tự:

1. **[thiet-ke-he-thong-aws.md](docs/thiet-ke-he-thong-aws.md)** — *cái gì* và
   *vì sao thiết kế vậy*. Có phần kiến thức nền về mạng máy tính và AWS ở đầu,
   rồi giải thích từng thành phần, và đối chiếu với yêu cầu đề bài.
2. **[nhat-ky-trien-khai.md](docs/nhat-ky-trien-khai.md)** — *làm thế nào*. Cách
   doanh nghiệp triển khai hạ tầng và vì sao, nhật ký 10 giai đoạn với bảng cấu
   hình cụ thể từng resource, và 12 issue chính đã gặp kèm bài học.

Chưa từng dùng AWS: bắt đầu ở **Phần VI — Lộ trình học** của tài liệu số 1. Nó
gắn từng thành phần trong hệ thống với workshop tiếng Việt tương ứng trên
[cloudjourney.awsstudygroup.com](https://cloudjourney.awsstudygroup.com/vi/), kèm
lộ trình 3 tuần và ghi rõ ba chỗ workshop **không** dạy mà đồ án cần.

## Deploy lên AWS

Toàn bộ quy trình nằm ở **[docs/terraform-runbook.md](docs/terraform-runbook.md)**
— bật/tắt, deploy phiên bản mới, rollback, seed, chẩn đoán sự cố, và chi phí.

Đường dùng hằng ngày là bốn script ở [infra/tf/scripts/](infra/tf/scripts/):

```bash
bash infra/tf/scripts/up.sh          # bật đủ để mở browser (~8-12 phút)
bash infra/tf/scripts/status.sh -w   # đang chạy gì, bao lâu rồi, tốn bao nhiêu
bash infra/tf/scripts/down.sh        # tắt sạch rồi tự kiểm chứng (~6-8 phút)
bash infra/tf/scripts/nuke.sh        # terraform destroy — hỏi xác nhận
```

`status.sh` in ý muốn (`terraform.tfvars`) cạnh thực tế (AWS API), kèm đồng hồ
cho từng resource và chi phí đã phát sinh. Bật/tắt mất nhiều phút và
`terraform apply` xanh **không** có nghĩa là hệ thống dùng được, nên đây là thứ
trả lời câu "xong chưa".

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
terraform -chdir=infra/tf/envs/prod init
bash infra/tf/scripts/up.sh
```

Thư mục **[infra/legacy-cli/](infra/legacy-cli/)** chứa bộ script bash + AWS CLI
của kỳ trước, giữ lại làm spec tham chiếu. **Đừng chạy lại chúng** — chúng tạo
resource nằm ngoài Terraform state, và chúng mở port 22 kèm SSH key pair.

---

## CI/CD

Deploy = push vào `main`. Hai workflow ở
[.github/workflows/](.github/workflows/):

- **[deploy.yml](.github/workflows/deploy.yml)** — chạy khi push `main` (hoặc
  `workflow_dispatch`): build 4 image, sinh script migration, rồi migrate +
  deploy nếu hạ tầng đang bật.
- **[ci.yml](.github/workflows/ci.yml)** — chạy trên PR và các nhánh khác:
  `terraform fmt`/`validate`/`test`, và `dotnet build`.

**Không còn credential dài hạn nào.** GitHub không giữ secret AWS nào — mỗi
job xin một OIDC token ngắn hạn do GitHub ký, AWS đổi thành credential tạm 1
giờ. Trước Phase 2, deploy đi bằng `EC2_SSH_KEY`, một private key không hết
hạn nằm trong GitHub Secrets.

**Pipeline không tự bật hạ tầng.** Push khi stack đang tắt vẫn xanh và vẫn
push đủ 4 image lên ECR, nhưng chưa deploy — summary của job nói rõ điều đó.
Lý do: mỗi giờ bật tốn $0.1954 nên để pipeline tự bật là chi phí không có
trần; IAM role của nó cũng không có quyền `autoscaling:SetDesiredCapacity`
hay `rds:StartDBInstance`.

Migration vẫn là gate của deploy — xem mục "Deploy lên AWS" ở trên. Chi tiết
đầy đủ (rollback, deploy tay, việc tay cấu hình GitHub) nằm ở
**[docs/terraform-runbook.md](docs/terraform-runbook.md)**.

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
