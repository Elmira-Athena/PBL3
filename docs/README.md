# Mục lục tài liệu — HushStore

Thư mục này có 11 tài liệu và ba thư mục con. Trang này để bạn **không phải mở
từng file ra xem nó nói gì**.

Đọc trang này mất 3 phút. Nó trả lời đúng một câu hỏi: *tôi đang cần gì thì mở
file nào.*

---

## Tôi muốn… → mở file này

| Tôi muốn… | Mở | Dài |
|---|---|---|
| Hiểu hệ thống này **là cái gì** và **vì sao thiết kế vậy** | [thiet-ke-he-thong-aws.md](thiet-ke-he-thong-aws.md) | 1096 dòng |
| **Bật hệ thống lên** / tắt đi / deploy bản mới / sửa sự cố | [terraform-runbook.md](terraform-runbook.md) | 864 dòng |
| Biết hệ thống **chặn được tấn công gì**, và vì sao | [bao-mat-he-thong.md](bao-mat-he-thong.md) | 598 dòng |
| Xem **bằng chứng** đã tấn công thật và bị chặn thật | [security-validation-report.md](security-validation-report.md) | 668 dòng |
| Biết dự án **đã đi qua những gì**, vấp ở đâu | [nhat-ky-trien-khai.md](nhat-ky-trien-khai.md) | 931 dòng |
| **Đang tốn tiền** và cần tắt gấp ngay bây giờ | [emergency-shutdown.md](emergency-shutdown.md) | 210 dòng |
| Ôn để **bảo vệ đồ án** (câu hỏi vấn đáp phần .NET/Blazor) | [on-tap-van-dap.md](on-tap-van-dap.md) | 704 dòng |
| Biết **phần code ứng dụng còn lỗi gì** | [ra-soat-ung-dung-multi-task.md](ra-soat-ung-dung-multi-task.md) | 292 dòng |
| Test tay các chức năng nghiệp vụ | [manual-test/](manual-test/) | 9 kịch bản |

---

## Chưa từng dùng AWS? Đọc theo đúng thứ tự này

Ba tài liệu, khoảng **4 tiếng** nếu đọc kỹ. Đừng đảo thứ tự — tài liệu sau giả
định bạn đã đọc tài liệu trước.

### Bước 1 — [thiet-ke-he-thong-aws.md](thiet-ke-he-thong-aws.md)

Chỉ đọc **Phần I** trước (khoảng 130 dòng). Nó dạy đúng 8 khái niệm mạng máy
tính mà mọi phần sau cần: địa chỉ IP và CIDR, subnet, route table, port và TCP,
**stateful vs stateless**, NAT, DNS, TLS.

Nếu chỉ nhớ được một điều từ Phần I, hãy nhớ mục **stateful vs stateless** — đó
là khác biệt giữa Security Group và Network ACL, và là lý do bảng rule NACL ở
Phần III trông rối như vậy.

Rồi đọc tiếp Phần II (AWS) và Phần III (thiết kế thật). Trong Phần III, mục
**"Linux — hệ điều hành chạy bên dưới tất cả"** đọc được độc lập nếu bạn quan
tâm phía hệ điều hành.

### Bước 2 — [nhat-ky-trien-khai.md](nhat-ky-trien-khai.md)

Sau khi biết hệ thống *là gì*, đây là *nó được dựng lên thế nào* — 10 giai đoạn,
bảng cấu hình từng resource, và 12 sự cố đã vấp thật kèm bài học.

Đọc phần sự cố trước nếu bạn ít thời gian. Lỗi thật dạy nhanh hơn cấu hình đúng.

### Bước 3 — [bao-mat-he-thong.md](bao-mat-he-thong.md)

*Vì sao nó chặn được.* Bảy lớp phòng thủ, mỗi lớp làm được điều gì mà lớp khác
không làm được, bảng 12 tấn công → lớp chặn → lớp dự phòng, và một danh sách
thẳng thắn những gì hệ thống **không** chặn được.

Mục **"câu hỏi phản biện"** ở cuối là thứ nên đọc ngay trước buổi bảo vệ.

### Chưa biết Terraform?

Xem **Phần VI** của tài liệu bước 1. Nó gắn từng thành phần hệ thống với
workshop tiếng Việt tương ứng trên
[cloudjourney.awsstudygroup.com](https://cloudjourney.awsstudygroup.com/vi/),
kèm lộ trình 3 tuần — và quan trọng hơn, nó nói rõ **ba thứ workshop không dạy**
(Terraform, Network ACL, ALB) rồi đưa nguồn thay thế cho từng thứ, ưu tiên tiếng
Việt.

---

## Trước khi chạy bất cứ lệnh nào

Ba điều dễ mất tiền hoặc mất thời gian nếu không biết trước:

1. **NAT Gateway và ALB mặc định tắt, và cả hai tính tiền theo giờ.** Bật khi làm
   việc, tắt ngay khi xong. Giữa hai cửa sổ làm việc, tên miền **cố ý** không
   vào được — đó không phải sự cố.
2. **`terraform apply` xanh KHÔNG có nghĩa là hệ thống dùng được.** Chạy
   `bash infra/tf/scripts/status.sh -w` để biết thật sự xong chưa.
3. **Profile `hushstore` dùng IAM user, không phải SSO.** Không có
   `aws sso login`. Kiểm tra bằng
   `aws sts get-caller-identity --profile hushstore`.

Chi tiết đầy đủ ở [terraform-runbook.md](terraform-runbook.md).

---

## Các tài liệu còn lại

| File | Là gì | Còn dùng không |
|---|---|---|
| [phase4-chuyen-account.md](phase4-chuyen-account.md) | Hồ sơ một lần: dựng lại toàn bộ stack sang account AWS mới `551897327153` | Đọc khi cần hiểu vì sao có hai account |
| [cleanup-account-cu.md](cleanup-account-cu.md) | Hồ sơ một lần: dọn sạch account cũ `667836586836` | Tham chiếu |
| [ra-soat-ung-dung-multi-task.md](ra-soat-ung-dung-multi-task.md) | Kết quả rà soát tầng code ứng dụng — lỗi đang có và việc còn phải kiểm | **Chưa sửa gì**, cố ý hoãn tới sau dự án hạ tầng |
| [archive/](archive/) | Hai bản rà soát kiến trúc app từ tháng 5/2026 | Đã lỗi thời, giữ để đối chiếu |
| [superpowers/](superpowers/) | Spec và implementation plan của 3 phase hạ tầng | Tham chiếu khi cần biết một quyết định đến từ đâu |

## Thư mục bằng chứng

`evidence/` chia theo account, vì bộ kiểm thử đã chạy **hai lần** trên hai hạ
tầng khác nhau:

| Thư mục | Account | Ý nghĩa |
|---|---|---|
| [evidence/acc-551897327153/](evidence/acc-551897327153/) | hiện hành | **Lần đo có hiệu lực.** 12/12 kịch bản đạt |
| [evidence/acc-667836586836/](evidence/acc-667836586836/) | đã xoá | Lần đo đầu, giữ để đối chiếu |

Việc hai lần đo trên hai account cho **cùng kết quả** chính là lập luận mạnh
nhất của báo cáo bảo mật: các thuộc tính an toàn nằm trong **code Terraform**,
không nằm trong một lần cấu hình may mắn.

---

## Quy ước trong toàn bộ tài liệu

- Mọi thứ viết bằng **tiếng Việt có dấu**, kể cả comment trong code.
- Đường dẫn code viết dạng `file.cs:123` để bấm được trong IDE.
- Chỗ nào **chưa kiểm chứng** thì ghi rõ là chưa kiểm, không ghi chung với chỗ
  đã kiểm. Nếu thấy một khẳng định mà không có lệnh hoặc file bằng chứng đi kèm,
  hãy coi đó là suy luận chứ không phải đo đạc.
