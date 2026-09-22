# `envs/dev` — môi trường thứ hai

Sinh ra để trả lời gợi ý số 6 của thầy: *"tách môi trường (dev/staging) bằng
workspace hoặc thư mục"*. Chọn **thư mục**, không phải workspace.

## Vì sao thư mục, không phải `terraform workspace`

`terraform workspace` dùng chung MỘT backend key và MỘT file cấu hình, phân
biệt nhau bằng `terraform.workspace` rải trong code. Ba vấn đề với dự án này:

1. **Không có ranh giới cứng.** Quên `terraform workspace select` một lần là
   apply thẳng vào prod. Thư mục thì `cd` sai là thấy ngay, và `backend.tf`
   khai key khác nhau nên state không thể lẫn.
2. **Mọi khác biệt phải viết thành `terraform.workspace == "prod" ? a : b`**
   rải khắp code. Ở đây khác biệt nằm gọn trong `variables.tf`.
3. **Backend S3 của workspace đặt state ở `env:/<tên>/<key>`** — một tiền tố
   mà IAM policy hiện tại không khai, nên vẫn phải sửa policy.

## Cấu trúc

```
envs/dev/
├── main.tf     → symlink ../prod/main.tf      ← KHÔNG sửa ở đây
├── outputs.tf  → symlink ../prod/outputs.tf   ← KHÔNG sửa ở đây
├── backend.tf              key = "dev/terraform.tfstate"
├── providers.tf            Env = "dev"
├── variables.tf            TOÀN BỘ khác biệt nằm ở đây
└── terraform.tfvars.example
```

**`main.tf` là symlink có chủ ý.** Hai môi trường phải dựng cùng một kiến
trúc; khác nhau ở *giá trị*, không ở *cấu trúc*. Copy 200 dòng sang đây là tạo
hai nguồn sự thật, và loại lỗi sinh ra từ đó là loại tệ nhất: dev xanh, prod
đỏ, vì một `module` block chỉ được thêm ở một bên.

Đánh đổi: thêm `var.foo` vào `../prod/main.tf` mà quên khai trong
`dev/variables.tf` thì `terraform validate` của dev đỏ. **Đó là hỏng to tiếng
có chủ ý** — CI chạy validate cho cả hai env nên drift bị chặn ở PR.

> Windows: cần `git config --global core.symlinks true` trước khi clone, nếu
> không git tạo ra file text chứa đường dẫn thay vì symlink.

## Ba thứ không được trùng prod

| | prod | dev | hỏng gì nếu trùng |
|---|---|---|---|
| backend `key` | `prod/terraform.tfstate` | `dev/terraform.tfstate` | apply ở dev nhận diện resource của prod rồi sửa chúng theo biến của dev |
| `var.project` | `hushstore` | `hushstore-dev` | `down.sh`/cost guard lọc theo `tag:Project` ⇒ dev tắt RDS của prod |
| `var.vpc_cidr` | `10.20.0.0/16` | `10.30.0.0/16` | hai VPC chồng dải, không bao giờ peer được |

Cả ba đều có `validation` chặn đúng giá trị của prod — nhưng validation chỉ
biết hai giá trị đã biết trước, nên đừng coi nó là lưới an toàn đầy đủ.

## Dev cố ý RẺ HƠN prod

| | prod | dev | lý do |
|---|---|---|---|
| `nat_gateway_count` | 2 | **1** | dev không đo tính sẵn sàng; tiết kiệm $0,059/giờ |
| `enable_multi_az` | true | **false** | Multi-AZ tính storage 2 AZ *kể cả khi RDS stopped* ⇒ nâng sàn chi phí ~$2,3/tháng, `down.sh` không tắt được |
| `enable_budget` | true | **false** | AWS chỉ cho 2 budget miễn phí **mỗi account**; dev dùng chung hạn mức đó |
| `monthly_budget_usd` | 20 | 5 | |

## Chạy

```bash
cd infra/tf/envs/dev
cp terraform.tfvars.example terraform.tfvars   # rồi sửa my_ip, alert_email
terraform init
terraform plan
```

⚠️ **Bộ script `up.sh`/`down.sh`/`status.sh`/`nuke.sh` hiện HARDCODE prod**
(`HS_PROJECT=hushstore`, `-chdir=envs/prod`). Chúng CHƯA chạy được cho dev —
dùng `terraform` trực tiếp, và tự nhớ tắt. Đây là việc còn nợ, không phải
thiết kế: xem `scripts/lib.sh`.

⚠️ **4 repo ECR của dev đang rỗng.** `deploy.yml` chỉ push vào repo của prod,
nên dev dựng được hạ tầng nhưng chưa deploy được ứng dụng. Cũng là việc còn nợ.
