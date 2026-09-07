# Buổi 4 — Toàn cảnh: đường đi của một request

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần III, mục "Đường đi của một request" (dòng 358–422).
> Mục tiêu buổi này: có một sơ đồ tổng trong đầu **trước khi** đi sâu vào
> từng thành phần ở các buổi 5–14. Buổi này không giải thích chi tiết từng
> lớp (đó là việc của các buổi sau) — chỉ vẽ bức tranh lớn.

## Ví dụ đời thường

Hình dung bạn muốn vào phòng họp của một công ty ở tầng 10:

1. Bạn tra Google Maps để biết địa chỉ công ty (DNS).
2. Đến cổng bảo vệ ngoài toà nhà — bảo vệ kiểm giấy tờ (lớp mạng ngoài).
3. Vào sảnh, có máy quét thẻ ở cửa xoay (lớp mạng trong).
4. Lễ tân hỏi bạn gặp ai, phòng nào (định tuyến theo "tên").
5. Đi vào khu vực văn phòng, qua thêm một cửa từ (lớp máy).
6. Vào tới phòng họp — nhưng phòng lưu trữ hồ sơ mật ở tầng hầm thì không có
   đường đi từ đây, phải qua một cửa khác hoàn toàn.

Hệ thống HushStore có cấu trúc tương tự: mỗi "cửa" là một lớp kiểm soát khác
nhau, và **lớp trong cùng (database) không có đường nào dẫn thẳng tới nó
từ ngoài**.

## Sơ đồ đầy đủ

```
Người dùng
   │  ① DNS: hushstore.io.vn → Cloudflare → ALB
   ▼
Cloudflare (proxy, TLS lớp ngoài — NGOÀI AWS, không do Terraform quản)
   │  ② NACL public (stateless) — cho vào 443?
   ▼
   │  ③ SG alb (stateful) — cho vào 443?
   ▼
Application Load Balancer  ── bóc TLS, đọc header Host
   │  ④ Listener rule — Host có trong danh sách cho phép?
   │       hushstore.io.vn     → target group web
   │       api.hushstore.io.vn → target group api
   │       tên khác            → trả 403, KHÔNG chuyển đi đâu
   ▼
   │  ⑤ NACL app (stateless) — cho vào 80/8080 từ public tier?
   ▼
   │  ⑥ SG web (stateful) — cho vào 80/8080 từ sg-alb?
   ▼
EC2 (không có IP public) ── container nginx :80 · container API :8080
   │  ⑦ NACL db + SG rds — cho vào 5432 từ app tier?
   ▼
RDS PostgreSQL — Multi-AZ (không có đường ra Internet)
```

## Ba cách đếm số lớp — không mâu thuẫn, chỉ đếm khác thứ

Tài liệu gốc và các tài liệu bảo mật khác của dự án dùng ba con số khác nhau
cho "số lớp bảo vệ". Đừng hoảng khi thấy chúng không khớp — chúng đếm ba thứ
khác nhau:

- **8 lớp** (sơ đồ trên) — số *chốt chặn* trên đường đi của **một** request.
- **7 lớp phòng thủ** ([bao-mat-he-thong.md](../docs/bao-mat-he-thong.md)) —
  số *nhóm* biện pháp bảo vệ, gộp theo loại chứ không theo vị trí.
- **12 chốt kiểm** (cũng tài liệu đó) — vì NACL **stateless**, mỗi NACL bị
  hỏi **hai lần**: một lần lúc gói tin vào, một lần nữa lúc trả lời đi ra
  (nhắc lại bài học buổi 1, mục 5).

Nếu ai hỏi "hệ thống có mấy lớp bảo vệ", câu trả lời đúng là hỏi lại: "đếm
chốt trên đường đi hay đếm nhóm biện pháp?" — đó không phải né tránh, đó là
câu trả lời chính xác.

## Cloudflare — ai và vì sao có mặt

Tên miền `hushstore.io.vn` được quản lý DNS bởi **Cloudflare** (dịch vụ miễn
phí, **không** thuộc AWS). Dự án bật chế độ *proxy* của Cloudflare — nghĩa là
Cloudflare đứng chắn phía trước và chuyển tiếp request vào ALB, thay vì chỉ
trả IP của ALB cho người dùng gọi trực tiếp.

Ba điều cần nhớ:

1. **Có hai lớp TLS nối tiếp**, không chồng lên nhau: người dùng ↔ Cloudflare
   dùng chứng chỉ của Cloudflare; Cloudflare ↔ ALB dùng chứng chỉ ACM của
   HushStore. Hai chặng riêng, mỗi chặng mã hoá độc lập.
2. **"ALB là cửa vào duy nhất" vẫn đúng — trong phạm vi AWS.** Trong VPC của
   HushStore không có đường nào khác vào. Cloudflare nằm *ngoài* AWS, ở phía
   trước ALB.
3. Cloudflare **không** nằm trong phạm vi đề bài và không do Terraform quản.
   Nó có mặt chỉ vì tên miền cần một nơi quản DNS, và bản miễn phí của
   Cloudflare tiện nhất.

## Điểm cốt lõi: các lớp không trùng lặp vô ích

Mỗi lớp bù cho điểm yếu của lớp khác — đây là ý nghĩa thật của **defense in
depth** (phòng thủ nhiều lớp), không phải "làm hai lần cho chắc":

- SG không chặn được một IP cụ thể (chỉ có danh sách cho phép) → NACL làm
  được (buổi 7).
- NACL không hiểu HTTP (không biết tên miền, không biết đường dẫn) → ALB làm
  được (buổi 9).

Mỗi lớp bắt được loại tấn công mà lớp khác bỏ sót.

## Bài tập

Không có bài tập đọc code cho buổi này — buổi này là bản đồ tổng, các buổi 5
đến 14 sẽ lần lượt mở từng ô ①–⑦ ra bằng code thật. Việc cần làm bây giờ là:
chép lại sơ đồ trên ra giấy hoặc một file riêng, và tự giải thích bằng lời
mỗi mũi tên — không cần đúng thuật ngữ, chỉ cần đúng ý.

## Câu hỏi tự kiểm tra

1. Nếu bạn gọi trực tiếp vào tên DNS thô của ALB (không qua Cloudflare, không
   qua `hushstore.io.vn`) mà nhận **403**, đó là lỗi hay đúng thiết kế?
2. Vì sao Cloudflare "đứng trước ALB" không phá vỡ câu "ALB là cửa vào duy
   nhất"?
3. Ô ⑦ trong sơ đồ gộp hai lớp (NACL db và SG rds) lại thành một ô — vì sao
   sơ đồ vẫn đếm được 8 lớp dù có ô gộp đó?

<details>
<summary>Gợi ý đáp án</summary>

1. Đúng thiết kế. ALB có allowlist Host header, default action là trả 403
   cho tên miền không nằm trong danh sách — DNS thô của ALB không phải
   `hushstore.io.vn` hay `api.hushstore.io.vn` nên bị chặn. Chi tiết ở buổi 9.
2. Vì "cửa vào duy nhất" chỉ khẳng định về phạm vi VPC của HushStore — không
   có đường nào khác *trong AWS* dẫn vào hệ thống. Cloudflare là một tầng
   *trước* ALB, nằm ngoài AWS hoàn toàn, không mở thêm đường nào vào VPC.
3. Vì đếm theo *lớp thật sự tồn tại*: mỗi tier (public/app/db) có một NACL
   tầng subnet và một SG tầng máy, 3 tier = 6, cộng ALB (lớp thứ 7 theo
   listener rule) và NACL public/SG alb ở đầu vào = 8. Ô ⑦ gộp hình ảnh cho
   sơ đồ ngắn, không gộp thực tế số lớp.

</details>

Buổi tiếp theo: [05-vpc-va-subnet.md](05-vpc-va-subnet.md).
