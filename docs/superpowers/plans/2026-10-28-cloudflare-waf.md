# Cloudflare Free WAF implementation plan

**Goal:** Thêm WAF cho cả web/API, không thêm dịch vụ AWS tính phí, không apply trong phiên này.

**Architecture:** Cloudflare Free hiện đứng trước ALB. Quản lý custom/rate/transform rules bằng Terraform root riêng; giữ Free Managed Ruleset tự động của Cloudflare. ALB chỉ nhận nguồn Cloudflare và chỉ forward khi Host cùng header bí mật hợp lệ.

**Tech Stack:** Terraform 1.15.8, AWS provider 6.60.0 hiện có, Cloudflare provider 5.28.1; Python stdlib cho kiểm tra vận hành.

**Spec:** [Phân tích và runbook WAF](../../waf-cloudflare.md).

## Ràng buộc

- Không apply AWS/Cloudflare, không đổi DNS, không đụng báo cáo/evidence đã đo.
- Giữ NACL migration hai bước độc lập; origin lockdown mặc định false cho đến khi edge sẵn sàng.
- Không đổi ECS bridge, port 80/8080, PostgreSQL 5432, IAM, NAT hoặc endpoint switches.
- Header `x-hushstore-origin`, secret 32–128 ký tự `[A-Za-z0-9_-]`, sensitive, không đưa vào git/log/output.
- Cloudflare Free: không paid managed ruleset/regex/challenge API; rate rule chỉ dùng path, IP+colo, 10s/10s.

## Thực hiện

- [x] Đọc cấu hình, so sánh Cloudflare Free / AWS WAF / ModSecurity+CRS bằng nguồn chính thức. Chọn Cloudflare vì DNS/proxy đã có và host chỉ 1 GB RAM.
- [x] Thêm `infra/cloudflare/`: ruleset custom chặn sensitive paths + IP deny tùy chọn; rate login; transform set header. Pin provider, lock đa nền tảng; mock tests và example không chứa secret thật.
- [x] Thêm AWS origin lockdown vào module security/alb và hai env; giữ địa chỉ SG/listener. Snapshot IPv4 từ nguồn chính thức, kiểm thử CIDR/port/Host AND secret/disable.
- [x] Thêm preflight đọc Cloudflare, runtime smoke không gây tải lớn, hướng dẫn import ruleset đã có và triển khai edge trước origin.
- [x] Đồng bộ CI, README, kiến trúc/bảo mật, runbook và sơ đồ; giữ lịch sử đã đo nguyên trạng.
- [x] Chạy fmt, validate, mock Terraform tests, Python tests, shell syntax và review độc lập. Ghi riêng local config validation và runtime chưa chạy.

## Kiểm tra nghiệm thu

`terraform -chdir=infra/cloudflare test`, `terraform -chdir=infra/tf/modules/security test`, `terraform -chdir=infra/tf/modules/alb test`; các run dùng `command = plan` và mock provider.

Runtime sau triển khai: benign requests cả hai host qua edge thành công; sensitive path/IP deny được Cloudflare block có Ray ID/Security Event; truy cập ALB trực tiếp với Host thật vẫn bị SG chặn; thiếu/sai header trên đường từ Cloudflare không được forward; login rate có Security Event. Cấu hình Free không chứng minh đầy đủ OWASP, strict global quota hoặc bảo vệ toàn bộ body.
