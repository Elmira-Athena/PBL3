# Buổi 7 — Network ACL, phần 1: khái niệm và hai NACL đơn giản

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần III, mục "Network ACL" — phần đầu, `nacl-public` và `nacl-db`
> (dòng 512–532, 591–600).
> Mục tiêu buổi này: nắm cách NACL hoạt động, và làm quen với 2 trong 3 NACL
> của hệ thống (cái thứ ba, `nacl-app`, khó nhất, dành riêng cho buổi 8).

## Vì sao mục này quan trọng

Đề bài đồ án nhấn mạnh Network ACL, và đây là phần **kỹ thuật đáng nói nhất**
trong toàn bộ thiết kế — nó là chỗ một quyết định tưởng nhỏ (thứ tự đánh số
rule) có thể làm lộ cả database ra Internet nếu làm sai.

## Ba tính chất của NACL

NACL gắn vào **subnet** (không phải vào máy — khác SG gắn vào máy/resource),
**stateless** (không ghi sổ — đã học buổi 1), và xét rule **theo số thứ tự
từ nhỏ đến lớn — gặp rule khớp đầu tiên là dừng, không xét tiếp**.

Tính chất thứ ba là chìa khoá của toàn bộ buổi 7–8: **với NACL, thứ tự là
một phần của cấu hình**, không phải chi tiết trình bày.

## `nacl-public` — chứa ALB và NAT Gateway

| # | Vào | # | Ra |
|---|---|---|---|
| **50** | **DENY tất cả từ `my_ip`** *(bật khi demo)* | 100 | allow 80 → app tier |
| 100 | allow 80 ← `0.0.0.0/0` | 110 | allow 8080 → app tier |
| 110 | allow 443 ← `0.0.0.0/0` | 120 | allow 80 → `0.0.0.0/0` |
| 120 | allow 1024–65535 ← `0.0.0.0/0` | 125 | allow 443 → `0.0.0.0/0` |
| `*` | **deny** (mặc định) | 130 | allow 1024–65535 → `0.0.0.0/0` |
| | | `*` | **deny** (mặc định) |

Rule **50** là điểm chứng minh giá trị thật của NACL: nó **chặn đúng một
IP**. Security Group *không làm được việc này* — SG chỉ có rule cho phép,
không có rule cấm. Muốn chặn một kẻ tấn công cụ thể mà vẫn phục vụ mọi người
khác, chỉ NACL làm được (đây chính là kịch bản KB-08 trong báo cáo bảo mật
của dự án).

## `nacl-db` — chặt nhất, và đơn giản nhất để hiểu trước

| # | Vào | # | Ra |
|---|---|---|---|
| 100 | allow 5432 ← **chỉ app tier** | 100 | allow 1024–65535 → **chỉ app tier** |
| `*` | **deny mọi thứ khác** | `*` | **deny mọi thứ khác** |

Không có rule nào khác. Chiều ra chỉ mở dải ephemeral **về app tier** — tức
database *chỉ được trả lời*, không được chủ động gọi đi đâu (cùng tinh thần
với `sg-rds` egress rỗng ở buổi 6, nhưng ở tầng subnet).

Đây là NACL dễ đọc nhất trong ba cái, vì nó không phải giải bài toán ephemeral
port lộn xộn như `nacl-app` — vì db tier không có ai gọi *vào* nó từ hướng
khác ngoài app tier, nên không có rule allow nào bị "vô tình mở rộng" như ở
buổi 8.

## Vì sao cần cả NACL và SG (nhắc lại, nhấn sâu hơn)

- SG không chặn được một IP cụ thể → NACL rule 50 làm được.
- NACL không hiểu HTTP → ALB (buổi 9) làm được.
- NACL buộc mở dải rộng (ephemeral) → SG mới là lớp nói "chỉ từ nguồn X" một
  cách chặt (buổi 8 sẽ thấy rõ nhất).

## Bài tập đọc code

Mở [`infra/tf/modules/network/nacl.tf`](../infra/tf/modules/network/nacl.tf):

1. Tìm resource NACL cho `nacl-public` — xác nhận có rule số 50 với action
   `deny` và nguồn là một biến (`var.my_ip` hoặc tương đương), và xác nhận
   rule đó có `count = var.enable_deny_demo ? 1 : 0` hoặc tương tự (nghĩa là
   rule này chỉ tồn tại khi bật demo, khớp với dòng "$0" ở bảng chi phí của
   tài liệu gốc).
2. Tìm resource NACL cho `nacl-db` — xác nhận đúng chỉ có 1 rule allow mỗi
   chiều (ngoài rule deny mặc định), và nguồn/đích của cả hai đều là app
   tier, không phải `0.0.0.0/0`.

## Câu hỏi tự kiểm tra

1. NACL xét rule theo thứ tự nào — số nhỏ trước hay số lớn trước?
2. Rule 50 của `nacl-public` chứng minh điều gì mà Security Group không làm
   được?
3. Vì sao `nacl-db` không cần rule "DENY" nào ngoài rule deny mặc định ở
   cuối, trong khi `nacl-app` (buổi 8) cần tới 3 rule DENY tường minh?
4. Egress của `nacl-db` chỉ mở dải ephemeral về app tier — điều này ngăn cản
   database làm gì?

<details>
<summary>Gợi ý đáp án</summary>

1. Số nhỏ trước — gặp rule khớp đầu tiên (theo thứ tự tăng dần) là dừng,
   không xét các rule số lớn hơn.
2. Chứng minh khả năng chặn **một địa chỉ IP cụ thể** mà không ảnh hưởng tới
   người dùng khác — SG chỉ có allow, không có deny, nên không làm được việc
   "chặn đúng một kẻ, cho qua tất cả người khác".
3. Vì `nacl-db` chỉ có duy nhất một rule allow (5432 từ app tier) — không có
   rule mở dải rộng nào (như dải ephemeral 1024-65535 cho *cả thế giới*) nên
   không có gì để "vô tình lọt qua" cần chặn trước. `nacl-app` có rule mở dải
   ephemeral cho `0.0.0.0/0` (buộc phải có, vì NAT), và chính dải đó vô tình
   chứa cả port nguy hiểm — nên cần DENY tường minh ở số nhỏ hơn.
4. Ngăn database chủ động khởi tạo kết nối đi bất cứ đâu ngoài việc trả lời
   app tier — tức nếu bị chiếm, nó không "gọi về nhà" (exfiltrate) được qua
   đường mạng.

</details>

Buổi tiếp theo: [08-nacl-phan-2-bai-toan-ephemeral.md](08-nacl-phan-2-bai-toan-ephemeral.md)
— phần khó nhất trong toàn bộ chương trình học, dành thời gian đọc kỹ.
