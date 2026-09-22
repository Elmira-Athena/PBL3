# HushStore

**Hạ tầng AWS dạng production cho một nền tảng thương mại điện tử .NET 10 — dựng bằng Terraform, deploy bằng GitHub Actions không có credential dài hạn nào, và được chứng minh bằng test cùng bằng chứng đã ghi lại, chứ không bằng lời khẳng định.**

🇬🇧 English version: **[README.md](README.md)** · Toàn bộ tài liệu thiết kế trong [`docs/`](docs/) viết bằng tiếng Việt.

---

| | |
|---|---|
| **Ứng dụng** | ASP.NET Core 10 Web API · Blazor WebAssembly · PostgreSQL 17 · EF Core |
| **Hạ tầng** | Terraform (8 module, ~6.300 dòng HCL) trên AWS `ap-southeast-1` |
| **Compute** | ECS EC2 launch type · 4 container image · one-off task cho migrate & seed |
| **Mạng** | VPC 3 tier × 2 AZ, mỗi tier một Network ACL riêng, app tier không có public IP |
| **CI/CD** | GitHub Actions + OIDC — **không tồn tại AWS access key nào** |
| **Kiểm chứng** | 109 assertion `terraform test` trong 12 file · 12 kịch bản bảo mật có output thô |
| **Kiểm soát chi phí** | Mọi resource tính tiền đều sau một công tắc · Lambda tự tắt hằng đêm · đồng hồ chi phí trong `status.sh` |

> **Về giá trị đã che.** README này dùng `<AWS_ACCOUNT_ID>`, `<YOUR_IP>` và `<ALERT_EMAIL>` ở những chỗ working tree đang giữ giá trị thật. `terraform.tfvars` bị git-ignore có chủ ý; chỉ `*.tfvars.example` được commit.

---

## Vì sao repo này đáng đọc

Phần lớn đồ án chứng minh được rằng hạ tầng *dựng lên được*. Repo này tổ chức quanh một câu hỏi khó hơn: **làm sao biết nó đúng, và làm sao biết nó vẫn còn đúng?**

Ba điều đi ra từ câu hỏi đó, và đó là những phần đáng dành thời gian:

1. **Tính chất bảo mật nằm trong mã, không nằm trong một lần bấm console may mắn.** Toàn bộ hạ tầng đã bị destroy trên một account AWS (132 resource) rồi dựng lại từ chính mã Terraform đó trên một account trắng. Cả 12 kịch bản bảo mật cho kết quả y hệt — xem [`docs/security-validation-report.md`](docs/security-validation-report.md) và output thô ở [`docs/evidence/`](docs/evidence/).
2. **Chi phí là ràng buộc thiết kế, không phải ghi chú cuối trang.** NAT Gateway và ALB không có bậc free tier nào và tính tiền theo giờ. Mọi resource loại đó đều nằm sau một công tắc mặc định *tắt*, và một Lambda thi hành lại mặc định đó mỗi đêm phòng khi có người quên.
3. **Mọi khẳng định đều có điều kiện kèm theo.** Chỗ nào đã đo thì README ghi rõ đo ngày nào, trên cấu hình nào. Chỗ nào **chưa** đo thì nói thẳng là chưa — xem [Giới hạn đã biết](#giới-hạn-đã-biết). `terraform apply` xanh không phải bằng chứng hệ thống chạy được, và repo này không coi nó là bằng chứng.

---

## Kiến trúc

```
                         Internet
                            │
                   Cloudflare DNS (proxied, Full strict)
                            │
    ┌───────────────────────▼────────────────────────────────────────────┐
    │  VPC 10.20.0.0/16 — 3 tier × 2 AZ, mỗi tier một Network ACL riêng  │
    │                                                                    │
    │  public tier   ALB (ACM cert, 80→443 redirect)                     │
    │                └─ allowlist Host header; Host lạ → 403              │
    │                2 × NAT Gateway (mỗi AZ một)                        │
    │                            │                                       │
    │  app tier      EC2 t3.micro — KHÔNG public IP, ECS container inst.  │
    │                ├─ container  web       nginx :80  (Blazor WASM)    │
    │                ├─ container  api       .NET :8080                  │
    │                └─ one-off    migrator (EF bundle) · seeder (psql)   │
    │                            │                                       │
    │  db tier       RDS PostgreSQL 17 Multi-AZ — cô lập,                │
    │                chỉ nhận :5432 từ app tier, egress rỗng             │
    └────────────────────────────────────────────────────────────────────┘
```

### Ma trận security group

| Tier | Rule vào | Rule ra |
|---|---|---|
| ALB | 80, 443 ← `0.0.0.0/0` | chỉ tới `sg-web` cổng 80 và 8080 |
| app | 80, 8080 ← **chỉ từ `sg-alb`** | 5432 → `sg-rds`; 80/443 → internet (ECR, SSM) |
| db  | 5432 ← **chỉ từ `sg-web`** | **rỗng** |

**Không có SSH.** Hệ thống không có key pair nào và không có security group rule nào mở cổng 22. Truy cập quản trị đi qua **SSM Session Manager** (vào host) và **ECS Exec** (vào trong container). Đã kiểm chứng bằng cách quét ALB từ một máy bên ngoài — xem [`docs/evidence/`](docs/evidence/).

**Không có credential dài hạn nào.** Đã kiểm chứng từ bên trong container đang chạy: 0 static AWS key trong env, 0 file `.env` trên disk. Credential đến từ ECS task role qua `AWS_CONTAINER_CREDENTIALS_RELATIVE_URI`; connection string do ECS inject từ SSM Parameter Store.

---

## Infrastructure as Code

```
infra/tf/
├── bootstrap/              S3 state backend, có versioning + mã hoá (lớp con-gà-quả-trứng)
├── envs/prod/              root module — ghép 8 module bên dưới
├── modules/
│   ├── network/            VPC, 3×2 subnet, route table, NACL, flow log, ECR endpoint
│   ├── security/           security group — chính ma trận ở trên
│   ├── storage/            4 × ECR repo, S3 assets, S3 ALB logs, S3 artifacts
│   ├── data/               RDS PostgreSQL 17, subnet group, SSM parameter
│   ├── ecs/                cluster, ASG, launch template, 4 task definition, 2 service, IAM
│   ├── alb/                ALB, ACM cert, listener, target group, allowlist Host
│   ├── cicd/               GitHub OIDC provider + 2 IAM role (deploy, plan)
│   └── costguard/          Lambda + EventBridge Scheduler + SNS + Budgets
└── scripts/                up.sh · down.sh · status.sh · nuke.sh · wait-for-capacity.sh
```

**Kiểm thử.** `terraform test` chạy **109 khối `run` trong 12 file test**, không cần credential AWS và không đụng tới state. Các assertion mã hoá thẳng bất biến bảo mật — NACL là stateless nên chiều về phải mở tường minh, không security group nào được mở cổng 22, IAM policy phải giữ least-privilege. Chạy bằng:

```bash
for m in infra/tf/modules/*/; do terraform -chdir="$m" init -backend=false && terraform -chdir="$m" test; done
```

**Hai quyết định đáng nói:**

- **CI trên PR cố ý KHÔNG có `terraform plan`.** `plan` phải đọc state, mà Terraform state chứa master password của RDS ở dạng plaintext (`random_password` luôn nằm trong state — đó là bản chất của Terraform, không phải cấu hình sai ở phía ta). Với trigger `pull_request`, claim `sub` của OIDC token không phân biệt được PR của chủ repo với PR từ một fork. Thay vì chấp nhận một ranh giới mờ quanh mật khẩu DB, role plan mang một **`Deny` tường minh trên `s3:GetObject`** — ràng buộc nằm ở IAM nên nó vẫn giữ kể cả khi sau này có người thêm workflow gọi `plan`. Đường nâng cấp (GitHub Environment có required reviewer) ghi trong [`.github/workflows/ci.yml`](.github/workflows/ci.yml).
- **Trust policy dùng `StringEquals`, không bao giờ `StringLike`.** Một wildcard trong điều kiện `sub` của OIDC chính là cách một repository đặt tên na ná repo của bạn assume được role của bạn.
- **Khoá state dùng lockfile gốc của S3 backend** (`use_lockfile = true`, Terraform ≥ 1.10) thay vì một bảng DynamoDB — bớt một resource tính tiền và bớt một thứ có thể lệch.
- **ECR repo để `IMMUTABLE` kèm `scan_on_push`.** Vì tag là git SHA và không bao giờ bị ghi đè, rollback bằng cách trỏ lại task definition revision cũ mới thực sự đáng tin: image của revision đó đúng từng byte với thứ đã được kiểm.

---

## CI/CD

Deploy = push vào `main`. Hai workflow ở [`.github/workflows/`](.github/workflows/):

| Workflow | Trigger | Làm gì |
|---|---|---|
| [`ci.yml`](.github/workflows/ci.yml) | PR, push `main`, chạy tay | `terraform fmt` → `validate` → `test`, và `dotnet build` |
| [`deploy.yml`](.github/workflows/deploy.yml) | push `main`, chạy tay | build 4 image → sinh SQL migration → preflight → migrate → deploy |

**GitHub không giữ secret AWS nào.** Mỗi job xin một OIDC token ngắn hạn do GitHub ký, AWS đổi thành credential tạm 1 giờ. Bản trước deploy qua SSH bằng `EC2_SSH_KEY` — một private key không hết hạn nằm trong GitHub Secrets; gỡ nó đi là cải thiện bảo mật lớn nhất của cả dự án.

**Migration là gate của deploy.** Nó chạy như một one-off ECS task. Exit code khác 0 thì không deploy, bản cũ vẫn phục vụ. `MigrateAsync()` lúc app khởi động đã bị xoá khỏi `Program.cs` — chạy migration từ N replica lúc boot là một cuộc đua, và nó buộc việc đổi schema dính vào việc restart tiến trình.

**Pipeline không tự bật hạ tầng.** Push khi stack đang tắt vẫn xanh và vẫn push đủ 4 image lên ECR, nhưng dừng trước bước deploy và nói rõ điều đó trong job summary. Mỗi giờ bật là tiền thật, nên để pipeline tự bật là hoá đơn không có trần; IAM role của nó cũng không có quyền `autoscaling:SetDesiredCapacity` hay `rds:StartDBInstance` — ràng buộc nằm ở IAM chứ không ở file YAML.

**Image tag bằng git SHA**, không bao giờ `latest` — nên một task đang chạy luôn truy ngược được về một commit, và rollback là đổi tag chứ không phải build lại.

---

## Kỹ thuật kiểm soát chi phí

Dự án chạy trên một account AWS dùng chung với ngân sách credit 100 USD, nên chi phí trở thành ràng buộc kỹ thuật chứ không phải ghi chú. Bất ngờ là đây lại là phần sinh ra nhiều bài học mang đi được nhất.

- **Mọi resource tính tiền đều sau một công tắc mặc định tắt.** `enable_nat`, `enable_alb`, `instance_count`, `enable_flow_logs`. Các công tắc này tách bạch với `max_instance_count` — thứ là *trần* (miễn phí) chứ không phải *trạng thái* (tính tiền). Gộp hai khái niệm đó lại chính là cách bán kính thiệt hại phình ra mà không ai biết.
- **`hushstore-cost-guard`** — một Lambda Python chạy qua EventBridge Scheduler lúc 00:00 giờ Việt Nam. Nó stop RDS, EC2 container instance và ECS service nếu có người quên tắt. Nó cố ý **không** đụng tới NAT Gateway và ALB: hai thứ đó do Terraform quản lý, xoá bằng API sẽ làm lệch state. Nó cũng là thứ chặn rủi ro RDS tự khởi động lại một instance đã `stopped` sau 7 ngày.
- **Nói thật về việc guard làm được tới đâu.** Sau khi nó chạy, hoá đơn giảm **~41%**, còn khoảng \$0.1512/giờ — **không** về $0. Tỉ lệ này giảm từ 55% ở giai đoạn trước, và lý do mới là điều đáng chú ý: không phải Lambda kém đi, mà phần nó **không** chạm tới được đã phình ra (2 NAT + ALB = \$0.1432/giờ, tự nó đã là 95% của hoá đơn sau khi guard chạy). Muốn về gần $0 thì phải `down.sh`.
- **`status.sh` in ý muốn cạnh thực tế** — giá trị trong `terraform.tfvars` đặt cạnh thứ AWS API thật sự trả về, kèm đồng hồ cho từng resource và chi phí đã phát sinh. Vì `terraform apply` xanh không có nghĩa là hệ thống dùng được, đây là script trả lời câu "xong chưa".

---

## Vận hành hằng ngày

Toàn bộ quy trình — bật/tắt, deploy phiên bản mới, rollback, seed, chẩn đoán sự cố, chi phí — nằm ở **[`docs/terraform-runbook.md`](docs/terraform-runbook.md)**.

```bash
bash infra/tf/scripts/up.sh          # bật đủ để mở browser (~8–12 phút)
bash infra/tf/scripts/status.sh -w   # đang chạy gì, bao lâu rồi, tốn bao nhiêu
bash infra/tf/scripts/down.sh        # tắt sạch rồi tự kiểm chứng (~6–8 phút)
bash infra/tf/scripts/nuke.sh        # terraform destroy — hỏi xác nhận
```

Ba ràng buộc không được bỏ qua:

- **Thứ tự bật là ràng buộc, không phải khuyến nghị.** RDS phải `available` trước khi bật ECS service, và phải chạy `wait-for-capacity.sh` sau khi apply — apply xanh không có nghĩa là instance đã đăng ký vào cluster.
- **Giữa hai cửa sổ làm việc, domain cố ý không hoạt động.** NAT và ALB đều tắt. Đó là trạng thái mặc định.
- **Đừng bật `enable_read_replica` rồi để qua đêm.** Có replica thì AWS từ chối stop primary, tức vô hiệu hoá cả `down.sh` lẫn cost guard. `down.sh` đã tự huỷ replica trước khi stop; `status.sh` in một dòng đỏ khi thấy replica — nhưng cả hai chỉ chạy khi có người gõ.

```bash
# Log và cách vào hệ thống — không dùng ssh, không dùng certbot
# (ACM cấp và tự gia hạn TLS, ALB terminate)
aws logs tail /ecs/hushstore-api --since 15m --follow --profile hushstore
aws ecs execute-command --cluster hushstore --task <arn> --container api \
  --interactive --command /bin/sh --profile hushstore
```

---

## Kiểm chứng & bằng chứng

| Cái gì | Ở đâu | Trạng thái |
|---|---|---|
| 12 kịch bản bảo mật (quét cổng, kết nối thẳng DB, SSH, blast radius IAM, giả mạo OIDC, rate limit, allowlist Host, NACL, flow log, cost guard) | [`docs/security-validation-report.md`](docs/security-validation-report.md), output thô ở [`docs/evidence/`](docs/evidence/) | 12/12 đạt, đo ngày 2026-08-24 — xem lưu ý bên dưới |
| Assertion cho module Terraform | [`infra/tf/modules/*/tests/`](infra/tf/) | 109 khối `run`, chạy trong CI ở mọi PR |
| Tính đúng đắn của ứng dụng dưới tải | [`tools/LoadProbe/`](tools/LoadProbe/), [`docs/evidence/loadprobe/`](docs/evidence/loadprobe/) | 9/9 bất biến giữ được ở cả 1 lẫn 2 instance |
| Khả năng dựng lại từ đầu | [`docs/security-validation-report.md`](docs/security-validation-report.md) | Destroy 132 resource rồi dựng lại trên account trắng; kết quả y hệt |

**`tools/LoadProbe/` không phải công cụ đo hiệu năng.** Nó bắn request song song rồi khẳng định bất biến bằng LINQ trên database, bởi vì **mọi lỗi đúng đắn dữ liệu tìm thấy ở repo này đều trả HTTP 200**. Voucher vượt hạn mức, sổ tổn thất nhân đôi, hai phiếu cùng một serial — tất cả đều "thành công" ở tầng HTTP. Nó cũng phân biệt `KHÔNG KẾT LUẬN` với `ĐẠT`: một kịch bản bị rate limiter chặn sẽ thoả mọi bất biến vì code cần đo chưa hề chạy — đó là bằng chứng an toàn giả, nguy hiểm hơn không có bằng chứng.

---

## Giới hạn đã biết

Nói thẳng, vì người review DevOps kiểu gì cũng tìm ra, và vì phần lập luận đằng sau còn đáng đọc hơn một danh sách trông sạch sẽ.

- **12 kịch bản bảo mật được đo trên SQL Server cổng 1433**, trước khi đợt chuyển sang PostgreSQL 17 đổi nó thành 5432 và thêm NAT Gateway thứ hai cùng Multi-AZ. *Hình dạng* của mọi rule không đổi ("đúng một cổng DB, chỉ từ app tier"), nên kết luận gần như chắc chắn giữ nguyên — nhưng *gần như chắc chắn* không phải *đã đo*, và repo này không làm tròn lên.
- **Đơn giá RDS (`$0.098`) là số đo trên SQL Server**, giữ lại như một cận trên. PostgreSQL Multi-AZ niêm yết \$0.051. Chưa sửa cho tới khi có hoá đơn thật xác nhận.
- **Mặc định chỉ một instance ứng dụng.** Chạy hai cái đòi bộ đếm rate limit phải dùng chung giữa các task trước; `terraform validate` chặn việc nâng `max_instance_count` mà không đặt kèm `rate_limiter_is_distributed`, nên trạng thái không an toàn đó không biểu diễn được. Bộ đếm dùng chung sau đó đã được cài và đo — xem [`docs/evidence/2026-09-06-rate-limit-dung-chung.md`](docs/evidence/2026-09-06-rate-limit-dung-chung.md).
- **Chưa có bộ test tự động cho mã ứng dụng.** `tools/LoadProbe/` phủ các bất biến về tương tranh; unit test thông thường thì chưa có. Đây là thiếu sót thật, không phải đánh đổi có cân nhắc.
- **Read replica có trong mã nhưng chưa mã nào đọc từ nó.** Bật lên là thêm một instance tính tiền mà primary không được giảm tải chút nào. Nó tồn tại để trình bày kiến trúc, chưa phục vụ lưu lượng.

---

## Chạy ở local

```bash
# PostgreSQL 17 — chỉ định rõ tên service: file compose còn service `sqlserver`
# của đợt trước, gõ thiếu là bật cả hai và tốn RAM vô ích
docker compose -f Infrastructure/db/docker-compose.yml up -d postgres

dotnet run --project src/API/API.csproj       # https://localhost:7010
dotnet run --project src/Client/Client.csproj # https://localhost:7107

dotnet ef migrations add <TênMigration> --project src/Infrastructure --startup-project src/API
dotnet ef database update            --project src/Infrastructure --startup-project src/API
```

Stack nhiều replica ở local (cho mọi tính chất **chỉ sai khi có nhiều hơn một tiến trình** — khoá tài khoản, seed lúc boot, phiên đăng nhập nhảy instance):

```bash
docker compose -f devops/docker/docker-compose.multi.yml up -d   # 2 replica sau nginx
```

**Deploy lên AWS** cần profile `hushstore` (IAM user thuần — không phải SSO, nên không cần `aws sso login`):

```bash
aws sts get-caller-identity --profile hushstore
terraform -chdir=infra/tf/envs/prod init
bash infra/tf/scripts/up.sh
```

---

## Bản đồ repo

| Đường dẫn | Nội dung |
|---|---|
| [`infra/tf/`](infra/tf/) | Terraform — 8 module, root env, bootstrap, script vận hành |
| [`.github/workflows/`](.github/workflows/) | `ci.yml` (validate + test + build) · `deploy.yml` (build → migrate → deploy) |
| [`devops/`](devops/) | Stack compose local, cấu hình nginx, script cổng chặn CI |
| [`src/`](src/) | Ứng dụng — Core · Shared · Infrastructure · Service · API · Client |
| [`tools/LoadProbe/`](tools/LoadProbe/) | Bộ đo bất biến dưới tương tranh |
| [`docs/`](docs/) | Thiết kế, runbook, báo cáo bảo mật, nhật ký triển khai |
| [`infra/legacy-cli/`](infra/legacy-cli/) | Bộ script bash + AWS CLI của kỳ trước. **Chỉ để tham chiếu — đừng chạy lại.** Chúng tạo resource nằm ngoài Terraform state và mở cổng 22 kèm SSH key pair. |

**Tài liệu nên bắt đầu từ đâu** (mục lục đầy đủ ở [`docs/README.md`](docs/README.md)):

- [`docs/thiet-ke-he-thong-aws.md`](docs/thiet-ke-he-thong-aws.md) — *cái gì* và *vì sao*, có phần kiến thức nền về mạng máy tính và AWS ở đầu
- [`docs/nhat-ky-trien-khai.md`](docs/nhat-ky-trien-khai.md) — *làm thế nào*: nhật ký 10 giai đoạn, 12 sự cố đã gặp thật kèm bài học
- [`docs/bao-mat-he-thong.md`](docs/bao-mat-he-thong.md) — 7 lớp phòng thủ, bảng 12 tấn công → lớp chặn → lớp dự phòng, và danh sách thẳng thắn những gì hệ thống **không** chặn được
- [`docs/terraform-runbook.md`](docs/terraform-runbook.md) — quy trình vận hành
- [`docs/cicd-cho-nguoi-moi.md`](docs/cicd-cho-nguoi-moi.md) — CI/CD từ con số không: vì sao dùng OIDC thay vì secret, vì sao tag bằng git SHA
- [`docs/doc-code-terraform.md`](docs/doc-code-terraform.md) — bản đồ code trong `infra/tf/`, đọc module theo thứ tự nào

Sơ đồ kiến trúc (bộ icon AWS 2026, mở bằng [app.diagrams.net](https://app.diagrams.net)): [`docs/diagrams/`](docs/diagrams/) — bản 3 trang để làm slide và bản 5 trang đầy đủ mọi resource.
