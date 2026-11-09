# Biên bản apply NACL → Security Group — 02/11/2026

> **Lịch sử — cập nhật 08/11/2026:** phương án Cloudflare WAF/origin lockdown đã được gỡ khỏi mã nguồn. Không thực hiện các bước triển khai Cloudflare trong tài liệu này; các số đo và quyết định bên dưới được giữ theo mốc cũ. Xem [rollback hiện tại](rollback-cloudflare-waf-2026-11-08.md).

**Đã apply phần AWS prod. Chưa apply Cloudflare WAF hoặc origin lockdown.**

Mã triển khai: commit `136d7fb`, account `551897327153`, region `ap-southeast-1`.
Thực hiện sau khi chủ dự án yêu cầu apply; giữ nguyên các công tắc tài nguyên.

## Kết quả triển khai

| Mốc | Kết quả thực tế |
|---|---|
| Refresh-only | Apply thành công, 0 thay đổi tài nguyên; đồng bộ drift metadata RDS và route table vào state |
| Mốc 1 | Apply thành công, 3 cập nhật ACL để tháo association; giữ nguyên rule cũ |
| Chốt giữa hai mốc | Đọc lại AWS: cả **6 subnet** gắn default NACL, default ACL allow-all IPv4 hai chiều |
| Mốc 2 | Apply thành công, xóa **3 custom ACL + 20 rule**, không create/update/replacement khác |
| Chốt sau mốc 2 | VPC chỉ còn default NACL; cả 6 subnet vẫn gắn default; state không còn custom ACL/rule |
| SG | Đọc lại AWS: ID/description/chính sách SG giữ nguyên, đúng chuỗi ALB → web → RDS 5432; default SG rỗng |
| Vận hành | Hai biến `my_ip`/`enable_deny_demo` được bỏ khỏi tfvars local, có backup riêng; validate prod và guard script đều đạt |

Không adopt hoặc sửa rule của default NACL. Không thêm SSH, public database hay
dải ephemeral vào SG. ECR interface endpoints vẫn tắt; không tạo endpoint mới.

## Trạng thái tài nguyên sau apply

| Tài nguyên | Đọc lại từ AWS |
|---|---|
| RDS | `stopped`, PostgreSQL 5432, `PubliclyAccessible=false`, Multi-AZ true |
| NAT Gateway đang hoạt động | 0 |
| ALB của dự án | 0 |
| ASG | Desired 0, instances 0 |
| Interface VPC endpoints | 0 |

Phiên này không bật serving stack. Chỉ kiểm chứng cấu hình, association và trạng
thái tài nguyên; chưa chạy lại web/API, ECR pull, SSM, CloudWatch hoặc các phép thử
kết nối bị chặn trên ứng dụng đang chạy.

## Plan cuối và cập nhật AMI ngoài phạm vi

Plan của cây migration cuối trả **exit 0 / No changes**. Cây này có override tạm
để giữ AMI đang triển khai và metadata sensitive của giá trị SSM, tránh gộp nâng
AMI vào migration ACL.

Root thông thường vẫn theo AMI mới nhất từ SSM và còn **một cập nhật Launch
Template do AMI**, chưa apply. Không nói root đã No changes. Không đổi chính sách
AMI trong repo; lần vận hành sau cần review cập nhật này riêng. Các thay đổi
VPC/subnet/SG/RDS ngoài phạm vi đều không được áp dụng.

## Phần WAF còn chờ

Phiên thực thi chưa có `CLOUDFLARE_API_TOKEN`, `TF_VAR_zone_id` hoặc file credential
local để xác thực Cloudflare. Vì vậy chưa init/migrate backend WAF, import/apply
ruleset, đổi DNS hoặc bật `enable_cloudflare_origin_lockdown`.

ALB SG hiện vẫn có inbound Internet 80/443 theo chế độ chưa lockdown; ALB chưa
tồn tại. Chưa có bằng chứng WAF mới đang bảo vệ lưu lượng. Bản sửa IP visitor ở
API đã có trong mã nhưng chưa được deploy thành ứng dụng đang chạy.

Để tiếp tục, cấu hình token/zone ID trong file đã gitignore
`infra/cloudflare/.env`, quyền 600; không gửi token lên chat. Sau đó làm theo
[runbook WAF](waf-cloudflare.md), giữ các rule sẵn có khi nhận quản lý ruleset và
kiểm tra API revision trước khi khóa origin.

## Bằng chứng

- [Kết quả apply trích từ log](evidence/2026-11-02-nacl-to-sg/apply-summary.txt).
- [Receipt trạng thái sau triển khai](evidence/2026-11-02-nacl-to-sg/receipt.json).
- [Default ACL và sáu association từ AWS](evidence/2026-11-02-nacl-to-sg/network-associations.json).
- [SG nguồn/đích/cổng từ AWS](evidence/2026-11-02-nacl-to-sg/security-groups.json).

State, saved plan, JSON plan và log đầy đủ chứa dữ liệu nhạy cảm chỉ giữ trong
thư mục private của phiên vận hành, không commit. Bằng chứng runtime cũ không bị
sửa và không được dùng thay phép đo cho kiến trúc mới.
