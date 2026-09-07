# Buổi 9 — Application Load Balancer

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần III, mục "Application Load Balancer" (dòng 601–636).
> Mục tiêu buổi này: hiểu vì sao ALB "hiểu HTTP" khác gì một load balancer
> thường, và cách allowlist Host header chặn được một loại tấn công cụ thể.

## ALB khác gì load balancer thường

ALB là cửa vào duy nhất của hệ thống (trong phạm vi AWS — nhắc lại buổi 4).
Nó khác một load balancer tầng mạng (chỉ nhìn IP/port) ở chỗ **hiểu HTTP**,
nên định tuyến được theo **tên miền** và **đường dẫn**, không chỉ theo
IP/port.

## Hai listener

- `:80` → **redirect 301** sang `:443`. Không phục vụ gì trên HTTP — mọi
  request HTTP đều bị đẩy sang HTTPS ngay.
- `:443` → có chứng chỉ ACM (buổi 2), bóc TLS, rồi áp rule định tuyến.

## Hai target group

| Target group | Cổng | Health check | Nhận traffic khi |
|---|---|---|---|
| `tg-web` | 80 | `/healthz` | Host = `hushstore.io.vn` |
| `tg-api` | 8080 | `/health/ready` | Host = `api.hushstore.io.vn` |

## Allowlist Host header — điểm đáng nói nhất

Default action của listener là **trả 403**, không phải chuyển tiếp đi bừa.
Chỉ hai tên miền trong danh sách (`hushstore.io.vn`,
`api.hushstore.io.vn`) được đi tiếp vào target group.

Nghĩa là gọi ALB bằng tên DNS thô của nó (địa chỉ do AWS tự sinh, ví dụ
`xxx.ap-southeast-1.elb.amazonaws.com`) sẽ nhận **403 — và đó là đúng thiết
kế**, không phải lỗi.

⚠️ Nếu nhận **503** khi gọi tên DNS thô thì mới là lỗi — điều đó chứng tỏ
allowlist chưa có hiệu lực (traffic bị chuyển tới target group nhưng không
target nào khoẻ, thay vì bị chặn ngay ở listener). Phân biệt hai mã lỗi này
là một câu hỏi hay gặp khi bảo vệ đồ án.

Rule này chặn được hai thứ:
1. **Host header injection** — kẻ tấn công gửi request với header `Host` giả
   mạo để đánh lừa app xử lý sai (ví dụ sinh link reset password trỏ sang
   domain khác).
2. **Domain fronting của người khác** — ai đó trỏ tên miền của họ vào IP của
   ALB này để "mượn" hạ tầng, sẽ nhận 403 vì Host header không khớp.

## Health check — là gì và vì sao quan trọng

ALB gọi một URL trên mỗi target theo chu kỳ (15 giây). Trả 200 thì target là
*healthy*, được nhận traffic; sai 3 lần liên tiếp thì bị coi là *unhealthy*
và bị rút khỏi vòng phục vụ (ALB ngừng gửi request mới tới nó).

Chi tiết đáng chú ý: `/health/ready` của API **có mở kết nối tới database**.
Đây là chủ ý — một API không nối được DB thì **không thực sự sẵn sàng**, dù
tiến trình vẫn "sống" (process vẫn chạy, chỉ là không làm được việc). Nhưng
điều này tạo ra một ràng buộc thứ tự khi khởi động: nếu API container lên
trước khi database sẵn sàng, health check sẽ fail liên tục cho tới khi DB
kết nối được — chi tiết này liên quan tới thứ tự khởi động, không đi sâu ở
buổi này.

## TLS hai lớp (nhắc lại buổi 2, 4)

Cloudflare đặt chế độ Full (strict): người dùng ↔ Cloudflare mã hoá bằng
chứng chỉ Cloudflare; Cloudflare ↔ ALB mã hoá bằng chứng chỉ ACM. Không có
đoạn nào chạy plaintext trên Internet — chỉ đoạn ALB → container (trong VPC
riêng) là HTTP không mã hoá (đã học ở buổi 2, mục TLS termination).

## Bài tập đọc code

Mở [`infra/tf/modules/alb/alb.tf`](../infra/tf/modules/alb/alb.tf):

1. Tìm resource listener cho port 443 — tìm `default_action` của nó, xác
   nhận là `fixed-response` với `status_code = "403"` (không phải
   `forward`).
2. Tìm `aws_lb_listener_rule` — có bao nhiêu rule dựa trên `host_header`?
   Xác nhận có đúng hai domain: `hushstore.io.vn` và `api.hushstore.io.vn`.
3. Tìm 2 `aws_lb_target_group` — xác nhận port và `health_check.path` khớp
   với bảng ở mục "Hai target group" phía trên.
4. Tìm resource listener cho port 80 — xác nhận `default_action` là loại
   `redirect` sang `443`.

## Câu hỏi tự kiểm tra

1. Vì sao 403 khi gọi DNS thô của ALB là đúng thiết kế, còn 503 mới là lỗi?
2. Allowlist Host header chặn được loại tấn công gì? Nêu một ví dụ cụ thể.
3. `/health/ready` của API mở kết nối tới database — điều này giúp phát hiện
   loại lỗi nào mà một health check "chỉ kiểm tiến trình còn sống" không
   phát hiện được?
4. Vì sao listener `:80` không phục vụ nội dung gì mà chỉ redirect?

<details>
<summary>Gợi ý đáp án</summary>

1. 403 là kết quả của default action "trả 403" khi Host header không khớp —
   đúng như thiết kế allowlist. 503 nghĩa là request đã được *chuyển tiếp*
   tới một target group (vượt qua allowlist) nhưng không target nào khoẻ —
   nghĩa là allowlist không hoạt động như mong đợi hoặc có lỗi cấu hình khác.
2. Host header injection: kẻ tấn công gửi request tới IP của ALB nhưng đặt
   header `Host: evil.com` hoặc tương tự để app xử lý sai (ví dụ sinh URL
   reset password trỏ sai domain). Allowlist chặn ngay vì `evil.com` không
   nằm trong 2 domain được phép.
3. Phát hiện lỗi "process còn sống nhưng không làm được việc" — ví dụ API
   process không crash nhưng mất kết nối DB (do mạng, do DB quá tải, do
   credential sai) thì vẫn coi là chưa sẵn sàng, được ALB rút khỏi vòng phục
   vụ thay vì tiếp tục nhận traffic và trả lỗi cho người dùng.
4. Vì HTTP không mã hoá — không muốn phục vụ nội dung thật qua kênh không an
   toàn. Redirect ngay sang HTTPS để đảm bảo mọi traffic thật đều được mã
   hoá từ đầu.

</details>

Buổi tiếp theo: [10-ec2-ecs-container.md](10-ec2-ecs-container.md).
