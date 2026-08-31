# Mục lục tài liệu — HushStore

Thư mục này có 14 tài liệu và bốn thư mục con. Trang này để bạn **không phải mở
từng file ra xem nó nói gì**.

Đọc trang này mất 3 phút. Nó trả lời đúng một câu hỏi: *tôi đang cần gì thì mở
file nào.*

---

## Tôi muốn… → mở file này

| Tôi muốn… | Mở | Dài |
|---|---|---|
| 🔴 **Tiếp tục viết code — dự án đang làm dở** | [bat-dau-phien-moi.md](bat-dau-phien-moi.md) | **đọc đầu tiên** |
| **Xem hình để in báo cáo / làm slide** | [diagrams/hushstore-aws-don-gian.drawio](diagrams/hushstore-aws-don-gian.drawio) | 3 trang |
| **Xem hình để tra cứu khi đọc tài liệu** — đầy đủ mọi resource | [diagrams/hushstore-aws-2026.drawio](diagrams/hushstore-aws-2026.drawio) | 5 trang |
| Hiểu hệ thống này **là cái gì** và **vì sao thiết kế vậy** | [thiet-ke-he-thong-aws.md](thiet-ke-he-thong-aws.md) | 1185 dòng |
| **Bật hệ thống lên** / tắt đi / deploy bản mới / sửa sự cố | [terraform-runbook.md](terraform-runbook.md) | 864 dòng |
| Biết hệ thống **chặn được tấn công gì**, và vì sao | [bao-mat-he-thong.md](bao-mat-he-thong.md) | 843 dòng |
| Hiểu **CI/CD** từ con số không | [cicd-cho-nguoi-moi.md](cicd-cho-nguoi-moi.md) | 399 dòng |
| Biết **code Terraform nằm ở đâu**, đọc theo thứ tự nào | [doc-code-terraform.md](doc-code-terraform.md) | 289 dòng |
| Xem **bằng chứng** đã tấn công thật và bị chặn thật | [security-validation-report.md](security-validation-report.md) | 668 dòng |
| Biết dự án **đã đi qua những gì**, vấp ở đâu | [nhat-ky-trien-khai.md](nhat-ky-trien-khai.md) | 931 dòng |
| **Đang tốn tiền** và cần tắt gấp ngay bây giờ | [emergency-shutdown.md](emergency-shutdown.md) | 210 dòng |
| Ôn để **bảo vệ đồ án** (câu hỏi vấn đáp phần .NET/Blazor) | [on-tap-van-dap.md](on-tap-van-dap.md) | 704 dòng |
| Biết **phần code ứng dụng còn lỗi gì** | [ra-soat-ung-dung-multi-task.md](ra-soat-ung-dung-multi-task.md) | 392 dòng |
| Test tay các chức năng nghiệp vụ | [manual-test/](manual-test/) | 9 kịch bản |

---

## Chưa từng dùng AWS? Đọc theo đúng thứ tự này

Một sơ đồ và bốn tài liệu, khoảng **5 tiếng** nếu đọc kỹ. Đừng đảo thứ tự — tài
liệu sau giả định bạn đã đọc tài liệu trước.

### Bước 0 — mở sơ đồ ra trước, và để đó

Có **hai** file sơ đồ, phục vụ hai việc khác nhau. Mở bằng
[app.diagrams.net](https://app.diagrams.net) (không cần cài gì) hoặc extension
*Draw.io Integration* trong VS Code.

**Để in báo cáo, làm slide, trình bày với thầy** —
[diagrams/hushstore-aws-don-gian.drawio](diagrams/hushstore-aws-don-gian.drawio),
3 trang, 93 hình. Bản đầy đủ vẽ hết mọi thứ nên đọc trên giấy A4 bị rối; bản này
cắt bớt để mỗi trang vừa **một** khổ giấy hoặc **một** slide:

| Trang | Nội dung | Tỉ lệ khung |
|---|---|---|
| 1 | Kiến trúc tổng thể — 5 thành phần đề bài yêu cầu, 3 tier, 3 NACL, 3 SG | 1,38 — vừa A4 ngang |
| 2 | Một request đi qua **10 chốt kiểm**, kèm lý do bảng NACL dài gấp đôi bảng SG | 1,79 — vừa slide 16:9 |
| 3 | 6 kịch bản tấn công: chặn ở lớp nào, luật nào chặn, **kết quả đo được** | 1,73 — vừa slide 16:9 |

**Để tra cứu trong lúc đọc tài liệu** —
[diagrams/hushstore-aws-2026.drawio](diagrams/hushstore-aws-2026.drawio), 5
trang, 240 hình, không cắt gì:

| Trang | Nội dung |
|---|---|
| 1 | Kiến trúc tổng thể — mọi resource, và cái gì nằm trong VPC / cái gì nằm ngoài |
| 2 | Một request đi từ Internet tới database qua **12 chốt kiểm**, kèm đường về |
| 3 | Luồng CI/CD — từ lúc bấm push tới lúc website đổi |
| 4 | Vòng đời bật/tắt và chi phí |
| 5 | 10 role IAM và ranh giới quyền |

Đọc tài liệu mà không có hình bên cạnh thì tới mục Network ACL sẽ mất phương
hướng. Nếu chỉ in được ba tờ giấy, in cả **ba trang của bản đơn giản**.

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

### Bước 4 — [cicd-cho-nguoi-moi.md](cicd-cho-nguoi-moi.md)

*Code đi từ máy bạn lên máy chủ bằng đường nào.* Đọc được độc lập, không cần biết
AWS trước — nhưng đọc sau bước 1 thì hiểu nhanh hơn nhiều.

Nếu chỉ có 5 phút: đọc mục **§7 — Đọc một lần chạy**. Đó là thứ bạn cần đúng lúc
vừa push code lên và đang không biết chuyện gì đang xảy ra.

### Sắp mở code Terraform ra đọc?

Đọc [doc-code-terraform.md](doc-code-terraform.md) trước. 68 file, 9713 dòng —
không có bản đồ thì rất dễ mở đúng file ít quan trọng nhất. Nó cũng chỉ ra **ba
chỗ dễ hiểu sai** trong code này, loại hiểu sai mà đọc kỹ vẫn mắc.

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
| [diagrams/](diagrams/) | Hai file `.drawio` dùng bộ icon AWS 2026: bản **đầy đủ** 5 trang để tra cứu, bản **đơn giản** 3 trang để in và trình chiếu | Nguồn duy nhất của mọi hình — sửa ở đây, không sửa bản xuất. Sửa một bản thì rà lại bản kia |
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

## Còn thiếu gì — rà soát ngày 2026-08-25

Rà soát này làm bằng cách **đóng vai một người chưa biết gì** đọc từ đầu bộ tài
liệu, và ghi lại mọi chỗ phải đi hỏi người khác. Ba tài liệu vừa được thêm
(`diagrams/`, `cicd-cho-nguoi-moi.md`, `doc-code-terraform.md`) đến từ chính rà
soát này.

Bốn chỗ **còn thiếu**, chưa làm, xếp theo mức chặn người mới:

| # | Thiếu gì | Vì sao nó chặn người mới | Vì sao chưa làm |
|---|---|---|---|
| 1 | **Bộ câu hỏi vấn đáp phần AWS/Terraform** | [on-tap-van-dap.md](on-tap-van-dap.md) chỉ có .NET/Blazor. [bao-mat-he-thong.md](bao-mat-he-thong.md) §6 có câu phản biện nhưng **chỉ về bảo mật** — không có câu nào về Terraform, state, module, ECS, hay chi phí | Đây là tài liệu để **luyện thi**, không phải để hiểu hệ thống. Nên tách ra làm việc riêng, và nên viết sau khi biết hội đồng gồm những ai |
| 2 | **Website này làm được gì** (chức năng nghiệp vụ) | Không tài liệu nào nói HushStore bán gì, có những luồng nào. Người mới đang đọc rất kỹ về cách **deploy** một thứ mà họ không biết là thứ gì | Cần quyết định của người chủ dự án về phạm vi mô tả. Và tầng ứng dụng đang **cố ý hoãn** — xem [ra-soat-ung-dung-multi-task.md](ra-soat-ung-dung-multi-task.md) |
| 3 | **Lược đồ database (ERD)** | Chuỗi `Category → Product → ProductVariant → ProductSerial` là trung tâm của cả nghiệp vụ, hiện chỉ có **một dòng** trong `CLAUDE.md`. Không có hình, không có mô tả quan hệ | Cùng lý do #2. Và ERD sinh từ code EF Core thì phải sinh lại mỗi lần đổi migration — cần quyết định có tự động hoá hay không trước khi viết |
| 4 | **Bản xuất PNG/PDF của sơ đồ** | `.drawio` cần app.diagrams.net hoặc extension VS Code mới mở được. Nộp báo cáo giấy thì phải có ảnh | Cố ý chưa xuất: hai bản (`.drawio` + `.png`) sẽ lệch nhau ngay lần sửa đầu tiên. Xuất **một lần, sát lúc nộp**, từ [bản đơn giản](diagrams/hushstore-aws-don-gian.drawio) — bản này đã dựng sẵn theo đúng tỉ lệ A4 ngang và 16:9 nên xuất ra là dùng được, không phải căn lại |

Hai chỗ đã kiểm và kết luận **không thiếu**, ghi ra để không ai rà lại:

- **Chạy dự án ở máy local** — có ở [`README.md`](../README.md) mục *Development*.
- **Cách đọc hoá đơn AWS và đơn giá thật** — có ở
  [terraform-runbook.md](terraform-runbook.md) mục *Chi phí*, kèm số đo thật và
  bằng chứng account này **không** có free tier 12 tháng.

---

## Quy ước trong toàn bộ tài liệu

- Mọi thứ viết bằng **tiếng Việt có dấu**, kể cả comment trong code.
- Đường dẫn code viết dạng `file.cs:123` để bấm được trong IDE.
- Chỗ nào **chưa kiểm chứng** thì ghi rõ là chưa kiểm, không ghi chung với chỗ
  đã kiểm. Nếu thấy một khẳng định mà không có lệnh hoặc file bằng chứng đi kèm,
  hãy coi đó là suy luận chứ không phải đo đạc.
