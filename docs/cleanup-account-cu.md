# Dọn sạch account cũ `667836586836` — biên bản

Ngày thực hiện: **2026-08-23**. Mục đích: chấm dứt việc bị trừ tiền vào thẻ
trước khi dời sang account mới (Phase 4).

## Kết quả

**132 resource bị xoá** qua hai lượt `terraform destroy`, cả hai `exit 0`:

| Lượt | Phạm vi | Số resource |
|---|---|---|
| 1 | `infra/tf/envs/prod` — toàn bộ stack | **127** |
| 2 | `infra/tf/bootstrap` — bucket tfstate | **5** |

Phân bổ lượt 1 theo module:

```
network    42      security  13      data       7
ecs        27      costguard 11      cicd       6
storage    20                        alb        1
```

RDS `hushstore-db-tf` mất 4 phút 44 giây để xoá — bước lâu nhất.

## Hai việc phải làm trước khi destroy chạy được

1. **`prevent_destroy = true`** ở `infra/tf/bootstrap/main.tf` chặn việc xoá
   bucket tfstate. Đã bỏ có ý thức, và để lại khối comment kèm ghi chú **bật lại
   ngay lần apply đầu trên account mới**. Lưới an toàn này đã làm đúng việc của
   nó: nó buộc việc xoá state bucket phải là một lần sửa code có chủ đích.
2. **`force_destroy = false`** ở bucket `public-assets` (cố ý, xem comment
   `modules/storage/s3.tf:18-22`) làm destroy thất bại nếu bucket còn object.
   Bucket chỉ có 1 object 78 byte — chính file proof của KB-10. Đã **tải về
   `docs/evidence/artifacts/kb10-upload-proof.png`** rồi mới xoá.

Bucket tfstate bật versioning nên phải dọn **hai** lượt, không phải một:
**184 version** rồi **114 delete marker**. Xoá version xong vẫn còn delete
marker, và `aws s3 rm --recursive` không chạm tới chúng — đây là chỗ dễ tưởng
đã sạch mà thật ra chưa.

## Xác minh sau khi xoá — `ap-southeast-1`

25 loại resource, **tất cả rỗng**:

```
S3 · RDS · RDS snapshot · ECR · ECS · EC2 instance · EBS volume
EBS snapshot · AMI · EIP · NAT · ALB · Lambda · EventBridge Scheduler
SNS · SSM Parameter · ACM · CloudWatch log group · Budget
VPC (non-default) · launch template · VPC endpoint
IAM role hushstore-* · OIDC provider · Secrets Manager
```

Không còn snapshot RDS nào: `skip_final_snapshot` mặc định `true` nên destroy
không tạo snapshot cuối, và 2 snapshot tự động cũng đi theo instance.

### Đa region

| Lượt | Kết quả |
|---|---|
| Trước destroy | 8 region (`us-east-1`, `us-west-2`, `ap-southeast-2`, `ap-northeast-1`, `eu-west-1`, `ap-south-1`, `us-east-2`, `eu-central-1`) — **sạch** |
| Sau destroy | `ap-southeast-1` + 5 region đầu — **sạch** |
| Sau destroy | 7 region còn lại — **CHƯA xác minh lại**, token SSO hết hạn giữa lượt quét |

Bảy region đó đã sạch ở lượt quét trước destroy, và destroy không tạo ra gì ở
đâu cả, nên gần như chắc chắn vẫn sạch — nhưng đó là suy luận, không phải phép
đo. Muốn đóng lại thì `aws sso login --profile hushstore` rồi quét lại.

## Những thứ CỐ TÌNH không xoá

| Thứ | Vì sao giữ |
|---|---|
| **IAM Identity Center** `ssoins-82101bfffeed71c4` + role `AWSReservedSSO_AdministratorAccess_*` | Đây là **đường đăng nhập console**. Xoá là tự khoá mình ra ngoài, mà root email `dangbathinh0901@gmail.com` là của bạn user nên phải qua người đó mới lấy lại được. |
| **AWS Organization** `o-d224wj4qd1` | Điều kiện để Identity Center hoạt động. Xoá cũng **không hoàn lại credit đã hết hạn** — thiệt hại đã xảy ra rồi. |
| **IAM user `athena232`** (AdministratorAccess, tạo 18/08, **không có access key**) | Miễn phí, và là đường break-glass nếu SSO hỏng. |
| **Default VPC `172.31.0.0/16`** | Mọi account AWS đều được cấp một cái, miễn phí. Xoá đi chỉ làm hỏng các lab console về sau mà tiết kiệm $0. |

Nói cách khác: **"xoá sạch" ở đây = xoá sạch tài nguyên của DỰ ÁN.** Ba thứ còn
lại đều miễn phí và đều là hạ tầng truy cập, không phải thứ sinh hoá đơn.

## Hệ quả cho người khác — cần thông báo

Ngưỡng cảnh báo **36%** của bạn user **không phải một budget riêng**. Nó là một
`notification` gắn trên budget `hushstore-monthly-spend` của dự án. Budget đó
vừa bị xoá, nên **bạn ấy mất luôn cảnh báo chi tiêu** trên account này.

Budget đó có 5 notification: 25% / 36% / 50% / 100% ACTUAL và 100% FORECASTED.
Muốn bạn ấy có lại thì tạo một budget riêng — **2 budget đầu miễn phí**.

## Chi phí

| | Trước | Sau |
|---|---|---|
| Sàn hằng tháng | **~$2.45** (RDS storage $2.30 + ECR $0.06 + S3/log $0.10) | **$0** |
| Mốc AWS tự bật lại RDS 2026-08-27T01:44 UTC | $2.35/ngày nếu để xảy ra | **không còn tồn tại** |

Mốc 7 ngày đó là deadline thật của việc dọn dẹp, và nó đã bị vô hiệu hoá bằng
cách xoá luôn instance — không còn gì để AWS bật lại.
