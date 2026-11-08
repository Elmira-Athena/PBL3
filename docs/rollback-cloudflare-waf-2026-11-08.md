# Rollback Cloudflare WAF — 08/11/2026

Theo yêu cầu của chủ dự án, gỡ phương án Cloudflare WAF để chuẩn bị cho một đợt
triển khai WAF mã nguồn mở trên EC2 sau này. **Đợt này chỉ rollback, không
triển khai WAF thay thế.**

## Phạm vi và cơ sở rollback

Biên bản ngày [02/11/2026](bien-ban-apply-sg-2026-11-02.md) ghi nhận Cloudflare WAF
và origin lockdown chưa được apply. Kiểm tra chỉ đọc ngày 08/11/2026 bằng profile
`hushstore` xác nhận account `551897327153`, key S3 dự kiến
`cloudflare/terraform.tfstate` trả `404 Not Found`; state prod có 134 địa chỉ
resource, không có resource Cloudflare hoặc custom NACL. Không có file credential,
tfvars hoặc state local của root Cloudflare; các biến credential cũng chưa được
đặt trong phiên làm việc.

Vì vậy đây là **rollback mã nguồn của phương án chưa triển khai**, không chạy
`terraform destroy`, không sửa state và không thực hiện lệnh ghi AWS/Cloudflare.
Không có credential Cloudflare để xác minh thay đổi thủ công ngoài Terraform;
nếu đã tự tạo rule trên dashboard sau biên bản cũ, cần rà soát riêng các rule đó.
Key state không tồn tại không chứng minh dashboard hoàn toàn không có WAF.

## Những phần được gỡ

- Root `infra/cloudflare/`: custom/rate-limit/header-transform rules, provider,
  backend, preflight, fixture và kiểm thử đi kèm.
- Input `enable_cloudflare_origin_lockdown`, `cloudflare_origin_secret` và phần
  truyền biến trong prod/dev; snapshot IP Cloudflare và SG ingress có điều kiện.
- Điều kiện header `x-hushstore-origin` trên ALB. Giữ định tuyến theo Host,
  phản hồi 403 cho Host lạ, redirect HTTP → HTTPS, ACM và access log.
- Chế độ API tin `CF-Connecting-IP`. API dùng `X-Forwarded-For` do ALB append,
  `ForwardLimit = 1`, cùng `X-Forwarded-Proto`; giữ kiểm thử chống giả mạo header.
- Các bước CI dành riêng cho Cloudflare. Giữ kiểm thử Terraform AWS, migration
  NACL, shell guard và proxy headers.

Không revert toàn bộ commit `136d7fb`, vì commit đó đồng thời chứa migration SG
đã được apply. Hướng dẫn vận hành và sơ đồ được đưa về kiến trúc không có WAF do
dự án quản lý. Tài liệu WAF cũ giữ làm lịch sử, không dùng để triển khai tiếp.

## Những phần giữ nguyên

- Sáu subnet dùng default NACL allow-all; không khôi phục custom ACL/rule DENY.
- Chuỗi SG Internet 80/443 → ALB → EC2 80/8080 → PostgreSQL 5432; default SG rỗng,
  ECR endpoint chỉ nhận 443 từ SG web khi bật. Không đổi ID/tên/description SG.
- Cloudflare DNS/proxy và Full (strict) đã có trước phương án WAF, bản ghi ACM,
  tên miền, chứng chỉ và tài nguyên AWS hiện hữu.
- NAT, IAM, công tắc endpoint, cấu hình chi phí, symlink dev, dữ liệu ứng dụng,
  báo cáo đã đo và toàn bộ `docs/evidence/` cũ.

## Kiểm chứng cấu hình

- Terraform validate đạt ở prod, dev và các module SG, ALB, ECS.
- Terraform mock tests: SG **8/8**, ALB **11/11**, ECS baseline proxy **1/1** đạt.
- Python: migration **20/20**, shell guard **5/5** đạt; cú pháp các script hạ tầng đạt.
- Probe **12/12** ca với `ForwardedHeadersMiddleware` thật đạt, gồm giả mạo XFF/CF,
  IPv4/IPv6, header thiếu/sai và scheme; không gọi AWS hay ứng dụng đang chạy.
- Format **64 file HCL + 2 tfvars mẫu**, `git diff --check`, symlink dev và XML
  hai sơ đồ (5 trang + 3 trang) đạt. Không có diff trong `docs/evidence/`.
- Rà soát độc lập không tìm thấy lỗi critical/important trong diff rollback.

Không chạy full application build, full ECS suite có data source AWS, GitHub CI,
live plan/apply hoặc kiểm thử runtime. XML hợp lệ không thay thế kiểm tra render
sơ đồ. Các kết quả test WAF cũ là lịch sử, không tính vào các số kiểm tra trên.

## Giới hạn sau rollback

Không có WAF mới được triển khai trong đợt này. ALB vẫn cho phép truy cập trực
tiếp với Host hợp lệ; SG không lọc SQLi/XSS hoặc chặn riêng một IP khi đã allow
Internet. Các biện pháp bảo mật và rate limit trong ứng dụng vẫn cần thiết.

Khi DNS còn proxied, ALB append IP edge Cloudflare; API ở chế độ một hop có thể
đếm rate limit theo edge IP thay vì từng visitor. Không tin lại `CF-Connecting-IP`
hoặc tăng `ForwardLimit` khi chưa thiết kế và kiểm thử ranh giới proxy tin cậy.

Không bật serving stack hoặc chạy kiểm thử runtime trong đợt rollback này.
State và số đo ngày 02/11 chỉ chứng minh trạng thái tại ngày đó. Một plan root
tương lai có thể còn cập nhật AMI từ SSM ngoài phạm vi; không tự apply diff đó
chỉ để hoàn tất rollback mã nguồn.
