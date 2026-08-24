# Phase 4 — Chuyển sang account mới và dọn sạch account cũ

> **TRẠNG THÁI 2026-08-24: ĐÃ THỰC HIỆN.** Account cũ dọn sạch 23/08 (biên bản:
> [`cleanup-account-cu.md`](cleanup-account-cu.md)), stack dựng lại trên account
> mới `551897327153` ngày 24/08. Phần dưới giữ nguyên làm bản ghi phạm vi và lý
> do; mục "Đã thực hiện" ở cuối ghi kết quả thật và những chỗ lệch kế hoạch.

## Mục tiêu

1. Dựng lại toàn bộ stack trên **account free-tier mới ($200 credit)**, dùng
   **IAM user thuần** — không Organization, không Identity Center.
2. Dọn **sạch** tài nguyên của dự án khỏi account cũ `667836586836`, kể cả
   khoản miễn phí, để thẻ ngừng bị trừ.
3. Đảm bảo không lặp lại lịch sử: chi phí chỉ trừ vào credit, không vào thẻ.

## Quy tắc cứng — nguyên nhân mất credit lần trước

**Không tạo AWS Organization. Không bật IAM Identity Center.**

Chuỗi nhân quả (AWS Support xác nhận 2026-08-23): dùng Identity Center thì buộc
phải tham gia Organization → account tham gia Organization thì AWS **tự chuyển
sang paid plan** → **credit cũ hết hạn**. Không có bước nào trong đó cần ta đồng
ý, và không đảo lại được.

Nên account mới dùng **IAM user + access key dài hạn** cho vận hành. Đánh đổi
này ngược với tinh thần "không có credential dài hạn nào" ở spec, và phải ghi
vào báo cáo như một quyết định có ý thức — nhưng nó không làm mất điểm
least-privilege, vì điểm đó nằm ở **4 role workload** (container-instance,
task-execution, task-app, github-actions), không nằm ở role vận hành. Bốn role
đó giữ nguyên, kèm cả `Deny` tường minh trên `ssm:GetParameter*`.

## Không có gì không thể tạo lại — đã kiểm, không suy đoán

Đây là phát hiện làm việc dọn dẹp an toàn hơn nhiều so với dự đoán ban đầu:

| Thứ | Trong account cũ | Tạo lại bằng |
|---|---|---|
| Schema DB | RDS `stopped` | 20 EF migration trong repo |
| Dữ liệu seed | trong RDS | `Infrastructure/db/seed_data.sql` + `seed_product_data.sql` (repo) |
| Ảnh sản phẩm | **1 object, 78 byte** — `proof/33a365ed….png`, chính là file test của KB-10 | không cần |
| ALB access log | 26 object, 24 KB | log của lần test cũ, đã trích vào `docs/evidence/` |
| Artifacts | **0 object** | — |
| ECR image | 4 repo | CI build lại từ source |
| tfstate | 352 KB | account mới có state riêng, mới hoàn toàn |
| RDS snapshot thủ công | **không có** | — |

Kết luận: **không cần bước di trú dữ liệu nào.** Không phải copy S3, không phải
dump DB, không phải snapshot rồi share sang account khác. Trước đó tôi lo bucket
ảnh sản phẩm chứa ảnh thật (spec cũ có `import` block cho nó với đúng lý do đó);
kiểm ra chỉ có một file proof 78 byte, nên lo đó không còn.

## Coupling với account id — chỉ 3 chỗ trong code

```
infra/tf/envs/prod/backend.tf:5    bucket = "hushstore-tfstate-667836586836"
src/API/appsettings.json:20        "BucketName": "hushstore-public-assets-667836586836"
docs/ (4 file)                     tham chiếu trong tài liệu
```

**Mẹo tránh churn:** đặt tên profile AWS của account mới cũng là `hushstore`.
`profile = "hushstore"` đang nằm trong **11 file test + backend.tf**; giữ nguyên
tên thì không phải sửa một dòng test nào, chỉ đổi credential mà profile trỏ tới.

## Việc `terraform destroy` KHÔNG tự dọn

- **`infra/tf/bootstrap/main.tf:31` có `prevent_destroy = true`** trên bucket
  tfstate → phải bỏ dòng đó trước, nếu không destroy sẽ chặn.
- **`hushstore-public-assets` có `force_destroy = false`** (cố ý, xem comment ở
  `modules/storage/s3.tf:18-22`) → phải xoá object thủ công trước. Chỉ 1 file.
  `alb-logs` và `artifacts` có `force_destroy = true` nên tự dọn.
- **Automated backup của RDS** và **final snapshot** — kiểm lại sau destroy,
  chúng tính tiền storage độc lập với instance.
- **Log group AWS tự tạo** (không do TF) — quét lại `aws logs describe-log-groups`.
- **Identity Center + Organization** — xem mục dưới.

## Điều chỉnh một giả định trong đề bài của bạn

> "dọn hết tất cả tài nguyên acc này … làm nó về y như mới"

Dọn hết tài nguyên: được, và nên làm. Nhưng **không đóng được account**, vì:

- account `667836586836` là **management account** của org `o-d224wj4qd1`;
- root email là `dangbathinh0901@gmail.com` — **của bạn bạn**, không phải bạn;
- bạn ấy **đang dùng account này để học lab AWS** (chính là chủ ngưỡng budget
  36% mà bạn bảo giữ lại).

Nên phạm vi thực tế của Phase 4 là: **xoá sạch tài nguyên của DỰ ÁN**, để lại
account và mọi thứ của bạn bạn nguyên vẹn. Xoá Organization / Identity Center
cũng **không** thuộc phạm vi của ta — đó là hạ tầng bạn ấy đang dùng, và xoá đi
cũng không hoàn credit đã hết hạn.

Về mục tiêu thật — thẻ ngừng bị trừ — thì xoá tài nguyên dự án là đủ: sau khi
destroy, phần dự án đóng góp vào hoá đơn về $0. Sàn ~$2.45/tháng hiện tại
(RDS storage $2.30 + ECR $0.06 + S3/log $0.10) biến mất hoàn toàn.

**Cần bạn xác nhận một điểm khi tới Phase 4:** giữ lại `shared_notifications`
(ngưỡng 36% của bạn bạn) trên account MỚI hay bỏ? Account mới là của riêng bạn,
nên mặc định tôi sẽ **bỏ** — nhưng nếu bạn ấy cũng dùng chung account mới thì
nói để tôi giữ.

## Thứ tự thực hiện, và vì sao thứ tự đó

Dựng account mới **trước**, dọn account cũ **sau**. Không phải vì sợ mất dữ
liệu (đã chứng minh không có), mà vì:

1. Account cũ là bản tham chiếu **đang chạy được** để so sánh khi account mới
   lệch hành vi.
2. Các bằng chứng bảo mật KB-01…KB-13 đã chụp trên account cũ; nếu account mới
   ra kết quả khác, cần cái cũ để đối chiếu trước khi kết luận cái nào đúng.
3. Sàn $2.45/tháng ≈ **$0.08/ngày** — giữ thêm vài ngày gần như không tốn gì,
   trong khi phá cầu sớm thì mất điểm tựa.

Phác thảo các bước (chi tiết hoá khi viết plan):

1. Account mới: tạo IAM user vận hành + MFA, access key, profile `hushstore`.
   **Kiểm ngay `aws organizations describe-organization` phải trả về lỗi** —
   đó là bằng chứng account chưa vào Organization nào.
2. Ghi mốc credit: chụp console Billing → Credits (không có API).
3. `bootstrap/` trên account mới → bucket tfstate mới; sửa `backend.tf`.
4. Đổi bucket name trong `appsettings.json`; quét hết `667836586836` còn lại.
5. `terraform apply` với toggle **tắt hết** (`enable_nat/alb=false`,
   `instance_count=0`) → dựng phần $0 trước, xác nhận plan sạch.
6. Budget + cost guard **trước** khi bật bất cứ thứ gì tốn phí.
7. GitHub: cập nhật `AWS_DEPLOY_ROLE_ARN` / `AWS_PLAN_ROLE_ARN` sang account id
   mới. OIDC provider phải tạo lại — nó gắn theo account.
8. Bật hạ tầng, chạy migration + seed, test lại KB-01…KB-13, sửa lỗi phát sinh.
9. Chỉ khi account mới xanh hết: dọn account cũ (bỏ `prevent_destroy`, xoá
   object trong assets, `terraform destroy`, quét sót, xác nhận Budgets về $0).

## Cảnh báo giữ nguyên hiệu lực

- Không cấu hình gì gắn với `bach.huynhvan@smartdev.com`. Mọi alert dùng
  `dacvinh2322006@gmail.com`.
- Không mở port 22 ở bất kỳ Security Group nào.
- Giữ `Deny` tường minh trên `ssm:GetParameter*` cho `/hushstore/*` ở
  container-instance role — đây là deliverable được chấm.
- Không dùng Cost Explorer (`aws ce`, $0.01/lần). Dùng `aws budgets describe-budget`.
- **RDS trên account cũ tự bật lại ~2026-08-27T01:44 UTC** nếu chưa destroy
  trước đó ($2.35/ngày). Đây là deadline thật của việc dọn dẹp.


---

# Đã thực hiện — 2026-08-24

## Account mới `551897327153`

| Kiểm | Kết quả |
|---|---|
| Danh tính | `arn:aws:iam::551897327153:user/hushstore-ops` — **IAM user**, không phải SSO role |
| `aws organizations describe-organization` | `AWSOrganizationsNotInUseException` ✅ |
| `aws sso-admin list-instances` | `[]` ✅ |
| Free tier | 4 dòng, **tất cả "Always Free"** — không có bậc 12 tháng, giống account cũ |
| Credit | **$100** (5 khoản "Explore AWS" x $20), chưa dùng đồng nào, hết hạn 16/08/2027 |

Credit là **$100 chứ không phải $200**: mô hình mới cho $100 sẵn + tối đa $100
nữa do làm activity, và hiện đã lấy 5 activity.

**Chưa xác minh:** các credit này có bị giới hạn theo service hay không. Mỗi
khoản đều có link "See complete list of services" trong console và **không có
API** để đọc. Nếu bị giới hạn thì bài toán chi phí đổi hình — ví dụ credit
"Launch an instance using EC2" mà chỉ áp cho EC2 thì tiền RDS không được bù.

## Bẫy đã gặp: profile bị SSO che khuất

Key IAM mới nằm ở `[hushstore]` trong `~/.aws/credentials`, nhưng `~/.aws/config`
vẫn còn `sso_session` ở cùng tên profile. **Profile có `sso_session` thì AWS CLI
dùng SSO và bỏ qua static key cùng tên** — nên `hushstore` vẫn trỏ account cũ dù
đã cấu hình key mới, và `sts get-caller-identity` là cách duy nhất phát hiện.

Đã tách SSO sang profile riêng `old-sso` (vẫn vào được account cũ cho người dùng
chung). Backup ở `~/.aws/config.bak-2026-08-24`.

## Kết quả apply

**126 resource**, `exit 0`. Một cái ít hơn 127 của account cũ vì `enable_budget = false`.

Trạng thái sau apply — không có gì đang tính tiền ngoài storage RDS:

```
NAT Gateway  rỗng      EC2 instance  rỗng      ASG desired  0
ALB          rỗng      EBS volume    rỗng      ECS service  rỗng
EIP          rỗng      Budget        rỗng      RDS          stopped
```

Hạ tầng đã dựng, tất cả miễn phí: VPC `10.20.0.0/16`, **3 NACL**, **3 Security
Group**, Lambda cost guard, schedule `ENABLED`, 4 ECR repo, OIDC provider.

Đáng ghi vào báo cáo: ba deliverable trọng tâm của đề bài — VPC, **NACL**,
**Security Group** — đều miễn phí vĩnh viễn. Phần bị chấm không tốn đồng nào.

Lambda cost guard đã chạy thật trên account mới, trả về đúng: 4 `notes`, 0
`findings`, 0 `errors`, `"sns": "khong-can-gui"`. Xác nhận luôn hai bản vá của
fix wave Phase 3 hoạt động — `stopping` vào `notes` (im lặng, đúng), và
NAT/ALB/EIP log cả khi đếm được 0.

## Hai lỗi phát hiện nhờ lần dựng lại

Cả hai đã tồn tại từ trước, chỉ lộ ra khi dựng từ số không:

1. **Diff vĩnh viễn của ASG.** ECS tự thêm tag `AmazonECSManaged` khi ASG gắn vào
   capacity provider; Terraform đòi xoá, ECS thêm lại. Plan không bao giờ sạch,
   nên drift thật lẫn vào tiếng ồn — lớp lỗi làm mù một cảm biến, không phải làm
   sai một giá trị. Sửa bằng cách khai tường minh tag. Sau sửa:
   `plan -detailed-exitcode` = 0, "No changes".
2. **`enable_budget` chưa tồn tại.** Account mới đã hết 2 slot budget miễn phí
   nên budget dự án là cái thứ 3 và có phí. Giờ tắt được.

## Còn phải làm

- **Chạy lại 12 kịch bản KB trên account mới.** Số liệu hiện tại trong
  `security-validation-report.md` đo trên account đã bị xoá — vẫn là phép đo
  thật, nhưng không chạy lại được để đối chiếu. Xem cảnh báo ở đầu báo cáo đó.
- **Cập nhật GitHub repository variables:**

```
AWS_DEPLOY_ROLE_ARN = arn:aws:iam::551897327153:role/hushstore-github-actions-deploy-role
AWS_PLAN_ROLE_ARN   = arn:aws:iam::551897327153:role/hushstore-github-actions-plan-role
```

- **4 repo ECR đang rỗng.** CI phải build và push trước khi `up.sh` chạy được.
- Merge vào `main` — chỉ sau khi hai variable trên đã đúng, vì merge là thứ kích
  hoạt `deploy.yml`.

## Chi phí

Sàn hiện tại **~$2.30/tháng** = storage RDS 20GB (mức gp2 tối thiểu cho
`sqlserver-ex`), trừ vào credit.

Trên $100 và một năm, đó là **~28% credit chỉ để giữ một DB đang tắt**. Con số
này đáng nhìn thẳng: nó lớn hơn mọi khoản khác của stack khi tắt, cộng lại.

Đòn bẩy duy nhất còn lại là **destroy RDS giữa các lần demo**. Schema dựng từ 20
EF migration, dữ liệu từ 2 file seed SQL — cả hai nằm trong repo, nên mất khoảng
15 phút để có lại. Đánh đổi đáng cân nhắc nếu khoảng cách giữa các lần demo tính
bằng tuần. Đổi engine sang PostgreSQL cũng giảm được (storage tối thiểu 20GB
nhưng gp3 rẻ hơn, và hết luôn CPU surplus).
