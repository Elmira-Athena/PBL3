# Buổi 6 — Security Group: firewall stateful, chỉ có danh sách cho phép

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần III, mục "Security Group" (dòng 484–511).
> Mục tiêu buổi này: hiểu 3 Security Group của HushStore, và vì sao chúng
> tham chiếu lẫn nhau thay vì ghi IP cứng.

## Nhắc lại nền tảng

Từ buổi 1: Security Group (SG) là firewall **stateful** — chỉ cần rule cho
chiều vào, chiều trả lời tự động được phép ("bảo vệ có ghi sổ"). SG chỉ có
rule **cho phép** (allow), không có rule cấm (deny) — đây là điểm khác biệt
lớn nhất với NACL (buổi 7–8), và là lý do NACL vẫn cần tồn tại song song.

## Ba SG của HushStore

| SG | Cho vào (ingress) | Cho ra (egress) |
|---|---|---|
| `sg-alb` | 80 ← `0.0.0.0/0`<br>443 ← `0.0.0.0/0` | 80 → `sg-web`<br>8080 → `sg-web`<br>*(không gì khác)* |
| `sg-web` | 80 ← **chỉ `sg-alb`**<br>8080 ← **chỉ `sg-alb`**<br>**không có rule port 22 nào** | 5432 → `sg-rds`<br>80, 443 → `0.0.0.0/0` *(tải image, gọi API AWS)* |
| `sg-rds` | 5432 ← **chỉ `sg-web`** | **rỗng hoàn toàn** |

## Vì sao tham chiếu SG thay vì ghi dải IP

IP của một EC2 instance thay đổi mỗi lần máy được tạo lại (ví dụ khi Auto
Scaling Group dựng máy mới). SG thì không đổi. Viết `80 ← sg-alb` nghĩa là
"cho vào nếu gói tin đến từ *bất cứ máy nào đang gắn sg-alb*" — rule tự đúng
mãi, không cần ai cập nhật thủ công khi máy đổi IP.

Đây cũng là cách **chặt hơn** so với ghi IP: không có cách nào giả mạo được
việc "tôi đang gắn SG đó" — SG là thuộc tính do AWS gán và kiểm soát, không
thể tự xưng.

## Vì sao egress của `sg-rds` rỗng hoàn toàn

Database không cần chủ động gọi ra ngoài bao giờ — nó chỉ trả lời các câu
hỏi (query) mà app gửi tới. Vì SG là stateful, câu trả lời cho app vẫn đi
được dù egress rỗng (nhớ lại: chỉ cần rule chiều vào, chiều trả lời tự động
được phép).

Egress rỗng nghĩa là: **nếu kẻ tấn công chiếm được database, nó không thể
gửi dữ liệu ra ngoài.** Đây là chặn đường rút (data exfiltration), không
phải chặn đường vào — một lớp phòng thủ khác hẳn với "ai được vào".

## Vì sao không có port 22 ở đâu cả

SSH là bề mặt tấn công lớn nhất và phổ biến nhất của một máy Linux trên
Internet — Flow Logs của dự án bắt được máy quét tự động dò port 22 và 23
chỉ trong **một giờ** hệ thống mở ra Internet (bằng chứng thật, không phải
suy đoán).

HushStore bỏ SSH hoàn toàn: **không có key pair nào, không có file `.pem`
nào**. Quản trị đi bằng **SSM Session Manager** — AWS mở kênh từ *trong* máy
ra, nên không cần mở port nào từ ngoài vào, và mọi phiên đều được ghi log và
kiểm soát bằng IAM (buổi 12, 14 sẽ nói kỹ hơn về SSM).

## Bài tập đọc code

Mở [`infra/tf/modules/security/main.tf`](../infra/tf/modules/security/main.tf):

1. Tìm 3 resource định nghĩa Security Group (`sg-alb`, `sg-web`, `sg-rds`
   — tên resource trong code có thể khác chút, tìm theo mô tả/tag).
2. Với `sg-web`, tìm rule ingress — xác nhận `source_security_group_id` (hay
   tên tương đương) trỏ tới `sg-alb`, **không** phải một CIDR block.
3. Tìm xem có bất kỳ rule nào chứa port `22` không — kỳ vọng là **không có**.
4. Với `sg-rds`, xác nhận không có resource egress nào được khai (hoặc có
   khai egress rỗng tường minh).

## Câu hỏi tự kiểm tra

1. Vì sao SG không cần rule riêng cho chiều trả lời, còn NACL thì cần (nhắc
   lại buổi 1)?
2. Nếu ai đó sửa `sg-web` để cho ingress từ `0.0.0.0/0` (thay vì
   `sg-alb`), điều gì thay đổi về mặt bảo mật?
3. `sg-rds` egress rỗng bảo vệ chống lại loại tấn công nào — chặn *vào* hay
   chặn *ra*?
4. SSM Session Manager thay SSH có cần mở port nào trên SG không?

<details>
<summary>Gợi ý đáp án</summary>

1. Vì SG stateful — nó "ghi sổ" kết nối đã được cho phép ở chiều vào, nên tự
   động cho chiều trả lời đi qua. NACL stateless, không ghi sổ, nên phải viết
   rule riêng cho cả hai chiều.
2. Bất kỳ máy nào trên Internet cũng gọi được trực tiếp vào container web,
   bỏ qua hoàn toàn ALB — mất luôn allowlist Host header, mất luôn TLS
   termination ở ALB, và mở thẳng bề mặt tấn công vào app.
3. Chặn *ra* (egress) — tức nếu kẻ tấn công đã chiếm được quyền thực thi mã
   trên RDS (kịch bản xấu nhất), nó vẫn không gửi được dữ liệu lấy trộm ra
   Internet.
4. Không — SSM hoạt động bằng cách máy tự gọi ra ngoài (outbound), không cần
   mở port nào để nhận kết nối từ ngoài vào.

</details>

Buổi tiếp theo: [07-nacl-phan-1-khai-niem.md](07-nacl-phan-1-khai-niem.md).
