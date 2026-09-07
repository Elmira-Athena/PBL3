# Buổi 8 — Network ACL, phần 2: bài toán rule 90/95/115 và dải ephemeral

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần III, mục `nacl-app` (dòng 533–590).
> Đây là phần **kỹ thuật khó nhất và đáng trình bày nhất** khi bảo vệ đồ án.
> Đọc chậm, và đọc lại buổi 1 mục 4–5 (port, ephemeral, stateful/stateless)
> nếu thấy trôi.

## Bảng rule đầy đủ

| # | Vào | # | Ra |
|---|---|---|---|
| **90** | **DENY 22** ← `0.0.0.0/0` | 100 | allow 5432 → db tier |
| **95** | **DENY 5432** ← `0.0.0.0/0` | 110 | allow 80 → `0.0.0.0/0` |
| 100 | allow 80 ← public tier | 115 | allow 443 → `0.0.0.0/0` |
| 110 | allow 8080 ← public tier | 120 | allow 1024–65535 → public tier |
| **115** | **DENY 8080** ← `0.0.0.0/0` | `*` | **deny** |
| 120 | allow 1024–65535 ← `0.0.0.0/0` | | |
| `*` | **deny** | | |

## Bước 1 — vì sao rule 120 phải tồn tại

Rule 120 mở dải 1024–65535 cho **cả thế giới** ở chiều vào. Nghe như một lỗ
hổng khổng lồ.

Nhưng nó **bắt buộc phải có**: NACL stateless (buổi 1, mục 5), nên khi EC2
tải container image từ Internet qua NAT, câu trả lời quay về sẽ đi tới một
ephemeral port trong dải đó — với địa chỉ nguồn là `0.0.0.0/0` (vì server
chứa image nằm ở đâu trên Internet, ta không biết trước). Không mở rule 120,
EC2 không tải được gì — kể cả image container, kể cả gọi API AWS.

## Bước 2 — vấn đề: 5432 và 8080 nằm trong khoảng đó

`5432` và `8080` **nằm trong khoảng 1024–65535**. Nghĩa là rule 120 vô tình
mở cả database port và API port ra Internet ở tầng NACL — dù ý định thật chỉ
là mở đường trả lời ephemeral.

## Bước 3 — cách xử lý: DENY ở số nhỏ hơn

NACL xét rule theo thứ tự tăng dần, khớp đầu tiên thì dừng (buổi 7). Nên đặt
**DENY ở số nhỏ hơn 120**:

- Rule **95** (`DENY 5432`) và rule **115** (`DENY 8080`) được xét *trước*
  rule 120, nên gói tin nhắm vào hai port đó bị chặn trước khi rule 120 kịp
  cho qua.
- Rule **90** (`DENY 22`) cũng theo logic này — chặn SSH tường minh ở tầng
  mạng, thêm một lớp nữa bên cạnh việc SG không có rule 22 (buổi 6).

## Bước 4 — cái bẫy dễ bỏ sót: rule 115 bị kẹp giữa HAI ràng buộc

Nói "DENY phải đứng trước rule 120" mới là **một nửa** sự thật. Với rule 115
(`DENY 8080 ← 0.0.0.0/0`), số của nó bị kẹp giữa hai ràng buộc:

```
110  allow 8080  ← public tier      ← 115 phải đứng SAU cái này
115  DENY  8080  ← 0.0.0.0/0
120  allow 1024-65535 ← 0.0.0.0/0   ← 115 phải đứng TRƯỚC cái này
```

- Đứng **sau 120** ⇒ vô dụng: rule 120 đã cho 8080 qua rồi (khớp trước, dừng
  luôn).
- Đứng **trước 110** ⇒ tai hại hơn nhiều: nó chặn luôn traffic **hợp lệ từ
  ALB**, vì `0.0.0.0/0` bao gồm cả dải public tier. Website chết, mà nhìn
  bảng NACL vẫn "trông đúng" — đây là loại lỗi khó phát hiện nhất vì
  triệu chứng (site không vào được) không trỏ thẳng tới nguyên nhân (thứ tự
  rule).

**Rule 95 (`DENY 5432`) không có ràng buộc dưới** — vì bảng này không có
rule nào allow 5432 *vào* app tier cả (chẳng ai gọi database *vào* app tier,
chỉ có chiều ngược). Đây là lý do hai rule DENY nhìn giống nhau (cùng cấu
trúc "DENY port X từ 0.0.0.0/0") nhưng số của chúng bị ràng buộc khác nhau —
ví dụ rõ nhất cho câu "với NACL, thứ tự là một phần của cấu hình", không
phải chi tiết hình thức.

## Ba điều rút ra — đáng nói khi báo cáo đồ án

1. **Thứ tự rule trong NACL là logic, không phải hình thức.** Đổi rule 95
   thành số 130 (sau rule 120) là mở database ra Internet — không cần đổi gì
   khác.
2. **Stateless là con dao hai lưỡi.** Nó cho phép viết rule DENY (điều SG
   không làm được — buổi 7), nhưng buộc phải mở dải ephemeral rộng (điều SG
   không cần, vì SG stateful tự động cho chiều trả lời).
3. **Vì thế SG vẫn cần thiết dù đã có NACL.** NACL ở tầng app buộc phải mở
   1024–65535 cho cả thế giới; chính SG mới là lớp nói "8080 chỉ nhận từ
   `sg-alb`" (buổi 6). Hai lớp bù đúng điểm yếu của nhau — đây là ý nghĩa
   thật của defense in depth, không phải làm hai lần cho chắc.

## Bài tập đọc code

Mở [`infra/tf/modules/network/nacl.tf`](../infra/tf/modules/network/nacl.tf)
và tìm phần định nghĩa NACL cho app tier:

1. Tìm 3 rule DENY (port 22, 5432, 8080) — ghi lại số thứ tự (`rule_number`)
   của từng cái.
2. Tìm rule allow 8080 từ public tier — số thứ tự của nó có nhỏ hơn số thứ
   tự của rule DENY 8080 không? (Bắt buộc phải nhỏ hơn, theo lý luận ở bước
   4.)
3. Tìm rule allow 1024–65535 từ `0.0.0.0/0` — số thứ tự của nó có lớn hơn cả
   ba rule DENY không?
4. Nếu dự án có test tự động cho NACL (tìm trong
   [`infra/tf/modules/network/tests/`](../infra/tf/modules/network/tests/)),
   mở file đó và tìm assertion nào kiểm tra đúng thứ tự này.

## Câu hỏi tự kiểm tra

1. Vì sao rule mở dải 1024–65535 không thể xoá bỏ hoàn toàn, dù nó "nghe như
   lỗ hổng"?
2. Nếu đổi số của rule 115 thành 125 (sau rule 120), điều gì xảy ra với API?
3. Nếu đổi số của rule 115 thành 105 (trước rule 110), điều gì xảy ra với
   website?
4. Vì sao rule 95 (DENY 5432) không có ràng buộc "phải đứng sau rule nào" —
   trong khi rule 115 (DENY 8080) có?
5. Giải thích bằng lời của riêng bạn: vì sao "một NACL đúng" không chỉ cần
   đúng nội dung rule, mà còn cần đúng vị trí rule?

<details>
<summary>Gợi ý đáp án</summary>

1. Vì NACL stateless — câu trả lời cho mọi kết nối do EC2 khởi tạo ra ngoài
   (tải image, gọi API AWS) sẽ về ở một ephemeral port ngẫu nhiên trong dải
   đó. Không mở dải này, EC2 không nhận được câu trả lời nào cả, dù chính nó
   là bên gọi ra trước.
2. Rule 120 (allow 1024-65535 từ 0.0.0.0/0) sẽ khớp trước rule 115 (vì số
   nhỏ hơn được xét trước) — traffic tới port 8080 từ Internet sẽ được CHO
   QUA bởi rule 120 trước khi rule 115 kịp deny. NACL sẽ mở port API ra cả
   thế giới trong im lặng.
3. Rule 115 sẽ chặn traffic port 8080 từ MỌI nguồn kể cả public tier (ALB),
   vì nó đứng trước rule 110 (allow 8080 từ public tier). ALB gọi vào API sẽ
   bị NACL chặn — website/API chết hoàn toàn, dù NACL "nhìn có vẻ đúng" nếu
   không soi kỹ số thứ tự.
4. Vì không có rule nào trong bảng cho phép traffic tới port 5432 *vào* app
   tier — không ai (không ALB, không tier nào khác) cần gọi database *vào*
   app tier, nên không có rule allow nào mà DENY 5432 phải tránh đứng sau.
   Ngược lại, rule 110 CHO PHÉP port 8080 từ public tier (traffic hợp lệ) —
   nên DENY 8080 phải đứng sau rule đó để không chặn nhầm traffic hợp lệ.
5. Vì NACL xét rule theo thứ tự tăng dần và dừng ở rule khớp đầu tiên. Một
   rule DENY đúng nội dung nhưng đặt sai vị trí (sau rule allow rộng hơn) sẽ
   không bao giờ được xét tới — nó tồn tại trên giấy nhưng vô dụng trong thực
   tế. Ngược lại đặt quá sớm (trước rule allow hẹp và đúng) sẽ chặn nhầm cả
   traffic hợp lệ.

</details>

Buổi tiếp theo: [09-application-load-balancer.md](09-application-load-balancer.md).
