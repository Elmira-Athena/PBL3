# Đo lại trên AWS 2026-11-26 — đóng 3 gap còn mở + xác minh F8/route-table

Nối tiếp cửa sổ 2026-11-23. Mục tiêu: đo trên AWS 3 thứ trước đó chỉ sửa trong code vì DB demo rỗng.
Image `b601104` (PR #50 đã merge: F8 + F5 + F-breach + route-table DB). RDS mới dựng, seed lại từ đầu.
Đi qua đúng đường Cloudflare→WAF→app (sau khi chủ dự án cập nhật DNS gốc Cloudflare sang ALB mới).
Mọi token/secret đã che.

## Kết quả

| # | Mục | Kết quả đo | Bằng chứng |
|---|---|---|---|
| F8 | sg-alb chỉ nhận Cloudflare | ✅ Gọi thẳng IP ALB → **timeout (000)**; qua Cloudflare → 200. Chặn đúng. | `r2-f8-recheck.txt` |
| write-flow | Checkout qua WAF | ✅ `POST /api/orders/checkout` → **200**, tạo đơn `ORD-20261126-001000` (exclusion 1007 không chặn nhầm). | `r2-f3-checkout.txt` |
| write-flow | Sửa sản phẩm qua WAF | ✅ PUT mô tả HTML hợp lệ → 200 (1005 cho qua); mô tả có `<script>` → **403** (WAF vẫn chặn XSS). | `r2-writeflow-waf.txt` |
| write-flow | Upload ảnh qua WAF | ✅ PNG vào `products` → 200 (1006 cho qua). | `r2-writeflow-waf.txt` |
| F3 | Thao túng `ShippingFee` | ✅ Gửi `shippingFee=-11989000`; đơn ghi **shippingFee=30000** (server áp `StandardShippingFee`), total=12.020.000 = 11.990.000 + 30.000. Giá trị client bị bỏ qua. | `r2-f3-checkout.txt` |
| F6 | IDOR lịch sử phiếu dịch vụ | ✅ B (chủ phiếu) đọc history → 200 có dữ liệu; A (không phải chủ) → "Phiếu này không thuộc về bạn." ở cả `/history` và chi tiết. | `r2-f6-idor-ticket.txt` |

## Chuỗi seed đã dùng (để lần sau tái lập)
1. Admin tạo Supplier (#1).
2. Phiếu nhập kho (#1) kèm 2 serial `PT1126-SN-001/002` cho variant 52 → tồn thật = 2.
3. Customer B: thêm địa chỉ → checkout BuyNow variant 52 (đồng thời đo F3 + write-flow checkout) → order #1.
4. Admin: confirm order → `export-order` serial PT1126-SN-001 (serial → Sold, gắn order của B).
5. Admin: tạo phiếu dịch vụ (#1) trên serial đó → CustomerId = B (lấy từ `order.UserId`).
6. Customer A thử đọc phiếu của B → F6.

## Dọn dẹp
Khôi phục mô tả sản phẩm 49; xoá ảnh test trên S3; deactivate tài khoản `pt-a-1126` (B trả 400 —
có đơn gắn với tài khoản, để lại có nhãn). Còn lại trên DB demo (có nhãn PENTEST, không có API xoá
hàng loạt): supplier#1, import#1, order#1 (ORD-20261126-001000), ticket#1, 2 serial PT1126.
Hạ tầng tắt bằng `down.sh` + kiểm bằng AWS API sau khi đo.

## Trạng thái các finding sau đợt này
Mọi finding đã vá của pentest 2026-11-23 nay **đã đo trên AWS**. Còn lại thuần lộ trình/quyết định:
F1 (mật khẩu admin — chấp nhận), F-egress (hoãn vì chi phí endpoint), F10 (readonlyRootFilesystem,
CVE base `web`/`seeder` — F5 đã hạ waf về 0 CVE), thiếu một số VPC endpoint. F-breach: role DML-only
đã có trong code, mặc định TẮT; bật cần theo `docs/runbook-dml-db-role.md` (chưa bật trong đợt này).
