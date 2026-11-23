# Cửa sổ pentest 2026-11-23 (17:24–17:40 giờ VN) — tóm tắt phát hiện

Kịch bản: [`docs/kich-ban-pentest.md`](../../../kich-ban-pentest.md). Image đang chạy: `940877b` (đã có 5 bản vá 23/11,
CI + Deploy `success`). Task def `hushstore-web:7`, `hushstore-api:7`, WAF `MODSEC_RULE_ENGINE=On`, PL1.
Pentester IP `<PENTESTER_IP>`. ALB origin `175.41.180.13`, `47.129.19.11`. Mọi token/mật khẩu đã che.

## Findings

| # | Mức | Phát hiện | Bằng chứng | Sửa |
|---|---|---|---|---|
| F1 | **Critical** | Tài khoản seed `admin@hushstore.com` / `Admin@123` đăng nhập được trên môi trường public; mật khẩu ghi rõ trong `Infrastructure/db/seed_data.sql:24` của **repo PUBLIC** → bất kỳ ai cũng có quyền Admin. | đăng nhập 1 lần → `200 Đăng nhập thành công`, JWT role `Admin` (không lưu token) | Đổi mật khẩu admin ngay; seeder sinh mật khẩu ngẫu nhiên từ SSM, xoá comment mật khẩu khỏi seed. |
| F2 | **High** | WAF chặn nhầm **mọi checkout**: CRS `932260` khớp tên trường JSON `userAddressId` (chứa `useradd`). Đặt hàng online không dùng được. | `p3-04b-waf-fp-checkout.txt` (5/5 biến thể → 403), log WAF `932260 … ARGS_NAMES:json.userAddressId` | Exclusion hẹp: `ctl:ruleRemoveTargetById=932260;ARGS_NAMES:json.userAddressId` cho `POST /api/orders/checkout`; thêm ca vào `devops/waf/test/run.sh`. |
| F3 | **High (tiềm ẩn)** | `CheckoutRequest.ShippingFee` do client gửi, không validator, cộng thẳng vào tổng (`OrderService.cs:150`) → gửi số âm là mua với giá tuỳ ý. Hiện bị F2 che; sửa F2 xong là khai thác được ngay. | code + `p3-10-shippingfee-tampering.txt` (bị WAF chặn trước khi tới app) | Server tự tính phí ship; bỏ trường khỏi DTO hoặc bỏ qua giá trị client. **Sửa F3 trước hoặc cùng lúc với F2.** |
| F4 | **High** | Mọi rate limit theo IP (`LoginRateLimit`, `LookupRateLimit`, …) **vô hiệu khi đi qua Cloudflare**: `ForwardLimit=1` lấy IP edge Cloudflare, mỗi request ra edge khác. Ngược lại, người dùng chung edge bị khoá chung. | `p3-07…` (ALB: 429 từ lần 6; CF: 39/42 lần qua), `p3-08e…` (ALB 429 từ lần 11; CF 15/15 qua), log WAF: 15 IP edge khác nhau | Lấy `CF-Connecting-IP` khi peer thuộc dải IP Cloudflare (hoặc KnownNetworks = dải CF + `ForwardLimit=2`); hoặc chặn ALB chỉ nhận dải CF. Per-account lockout Identity vẫn giới hạn đoán theo từng tài khoản. |
| F5 | Medium | WAF image mang **5 CVE CRITICAL + 1 HIGH của nginx** (`owasp/modsecurity-crs:4.30.0-nginx-alpine-202610180210`); web `nginx:alpine` 1 CRITICAL (zlib) + 5 HIGH; seeder 12 HIGH. api/migrator: 0. | `p3-11-ecr-scan-container.txt` | Nâng tag CRS mới nhất, build lại, quét lại. |
| F6 | Medium | `GET /api/service-tickets/{id}/history` cho Customer nhưng **không kiểm tra chủ sở hữu** (`ServiceTicketsController.cs:691`, `GetTicketHistoryAsync(id)`) → IDOR đọc lịch sử + `Note` phiếu người khác. Chưa đo runtime (DB chưa có phiếu nào). | code; runtime trả `200 []` cho id bất kỳ | Truyền `userId, IsCustomer` như `GetTicketByIdAsync`. |
| F7 | Medium | Dò tài khoản: email tồn tại → sau 5 lần sai trả "Tài khoản đã bị tạm khóa…", email không tồn tại thì không bao giờ; thời gian 0.34–0.61s vs ~0.31s. `register` báo "Số điện thoại này đã được sử dụng." | `p3-08d-enum-lockout.txt` | Trả cùng một câu cho locked/sai mật khẩu; hash giả khi user không tồn tại. |
| F8 | Medium | Origin ALB truy cập trực tiếp được (bỏ qua Cloudflare) — `sg-alb` mở `0.0.0.0/0`. | `p1-02`, `p2-01` | Chấp nhận có chủ đích (Cloudflare WAF đã rollback) hoặc giới hạn ALB theo dải IP Cloudflare (cũng sửa F4). |
| F9 | Low | Trang 403 của WAF không có security headers/CORS; `Server: nginx` vẫn lộ (không version). HSTS `max-age=86400`, không `includeSubDomains`. CSP `script-src 'unsafe-inline'`; `connect-src` còn `api-dev.hushstore.io.vn`. | `p1-02`, `p3-03` | `add_header … always` ở WAF; HSTS ≥ 1 năm; dọn CSP. |
| F10 | Low | RDS `DeletionProtection=false` (primary + replica). Endpoint policy S3/ECR `Allow *`. API dùng master DB user (`data/main.tf:227`). `readonlyRootFilesystem` chưa bật. | `p2-03` | Đã biết (§2.4 kịch bản). |
| F11 | Info | `dev.hushstore.io.vn` còn bản ghi DNS proxied → 530. Thêm giỏ hàng báo hết hàng dù `stockQuantity=5` (nhiều khả năng `ProductSerials` chưa seed) — lỗi chức năng. File polyglot PNG+`<script>` upload được nhưng S3 phục vụ `image/png`. | `p1-02`, `p3-08`, `p3-05` | Xoá DNS dev; seed serial. |

## ĐẠT (đúng kỳ vọng)

- Mạng: ALB chỉ mở 80/443; 22/5432/8080/… `filtered` + `REJECT` trong Flow Logs (`p2-01`, `p2-04`). EC2/RDS không public IP. `default` SG rỗng; 6 subnet default NACL.
- TLS chỉ 1.2/1.3, cipher hạng A; 80→301 giữ path/query; Host lạ/IP → 403 (`p2-02`).
- WAF: SQLi/XSS/traversal/Log4Shell/RCE/UA scanner → 403, log JSON có rule ID (942/941/930/932/913); app không có host port (`portMappings: []`). Audit `AHZ` không chứa password/Bearer.
- Exclusion 1005 hẹp: HTML hợp lệ qua, 8/9 biến thể XSS bị chặn; `<form>` lọt WAF nhưng `HtmlContentSanitizer` bỏ `form/input/button` (code).
- CORS chỉ nhận `https://hushstore.io.vn`. Swagger/OpenAPI 404. `/appsettings.json` chỉ có `ApiBaseUrl`. `.git/.env/.pdb` → 403.
- JWT: 10 biến thể giả (alg none, đổi role, secret yếu, hết hạn, bỏ chữ ký) → 401; Customer → endpoint Admin 403; token sống 15 phút.
- Bản vá 23/11 đã chạy đúng trên runtime: khoá tài khoản chặn token cũ ở cả `change-password` (403); login sai mật khẩu không lộ `LockReason`; đổi mật khẩu thu hồi refresh token; refresh rotation chặn replay; upload: Customer → `products/banners` 403, folder lạ/traversal 400/403, magic bytes sai 400, request thứ 21 → 429 (`p3-05`, `p3-08b/c`).
- IAM: host role `explicitDeny` 4 action SSM `/hushstore/*`; plan role `explicitDeny` S3/SSM/KMS/Logs/AssumeRole; OIDC token giả bị STS từ chối, trust `StringEquals` ghim `sub` (`p3-12`, `p3-12b`). S3: 4 bucket chặn list ẩn danh; ECR 5 repo `IMMUTABLE` + scan-on-push, không repo policy. RDS `rds.force_ssl=1`, mã hoá, Multi-AZ, backup 7 ngày.

## CHƯA LÀM / KHÔNG KẾT LUẬN

- **Giai đoạn 4 Assumed Breach (ECS Exec vào `api`)**: bị chặn bởi permission classifier của Claude Code → chờ chủ dự án duyệt. Kéo theo: IMDS từ container, `169.254.170.2`, kết nối RDS non-TLS từ trong VPC, lateral `172.17.0.1`.
- IDOR đơn hàng/giỏ của người khác, race condition voucher/checkout: không dựng được dữ liệu (checkout bị F2 chặn, không có đơn/phiếu nào).
- Stored XSS end-to-end trên trình duyệt (cần admin ghi sản phẩm thật), CSP trên trình duyệt thật.
- Chạy đối chiếu `DetectionOnly` (cần `terraform apply`).

## Dọn dẹp

Xoá 2 object test trên S3 `avatars/`; 2 tài khoản `pentest-{a,b}-1123@example.com` đã deactivate (soft). Còn lại trong DB:
2 user đã khoá, 1 `UserAddress` của B, các dòng `RateLimitCounters` (tự hết hạn). Không tạo đơn hàng nào.
