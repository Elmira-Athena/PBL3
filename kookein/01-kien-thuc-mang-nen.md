# Buổi 1 — Kiến thức mạng nền: IP, CIDR, subnet, route table

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần I, mục 1–6 (dòng 30–151).
> Mục tiêu buổi này: hiểu **vì sao** mạng phải chia thành nhiều "phòng" nhỏ,
> và phòng nào thì thông ra ngoài được, phòng nào thì không — trước khi động
> tới bất kỳ dịch vụ AWS nào.

## 1. Địa chỉ IP và CIDR

Ví dụ đời thường: IP giống địa chỉ nhà — `10.20.10.37`. Bốn số, mỗi số từ 0
đến 255.

**CIDR** là cách viết gọn một *dải* địa chỉ, kiểu như nói "toàn bộ số nhà trên
đường Nguyễn Văn A" thay vì đọc từng số nhà. `10.20.0.0/16` nghĩa là "mọi IP
bắt đầu bằng `10.20.`".

Con số sau dấu `/` là **số bit bị cố định**. IP có 32 bit, nên số địa chỉ còn
tự do = 2^(32 − số sau dấu `/`):

| CIDR | Bit cố định | Số địa chỉ | Nghĩa |
|---|---|---|---|
| `10.20.0.0/16` | 16 | 65.536 | mọi IP bắt đầu bằng `10.20.` |
| `10.20.10.0/24` | 24 | 256 | mọi IP bắt đầu bằng `10.20.10.` |
| `42.1.89.156/32` | 32 | 1 | **đúng một** máy |

Quy tắc nhớ: **số sau `/` càng lớn, dải càng nhỏ**. `/32` = một địa chỉ duy
nhất — dùng để chỉ đích danh một máy (ví dụ laptop của bạn khi viết rule chặn
riêng nó).

`0.0.0.0/0` = **mọi địa chỉ trên Internet**. Đây là dấu hiệu cần soi kỹ nhất
khi đọc bất kỳ rule bảo mật nào — thấy nó trong một rule *cho phép* nghĩa là
"mở cho cả thế giới".

**IP private vs public.** Ba dải `10.x.x.x`, `172.16–31.x.x`, `192.168.x.x` là
IP *riêng* — giống số nội bộ trong một công ty, không ai ngoài công ty gọi
được trực tiếp. Máy chỉ có IP private thì **Internet không gọi vào được** —
đây là lớp bảo vệ mạnh nhất trong toàn hệ thống, mạnh hơn mọi firewall, vì kẻ
tấn công không thể gõ cửa một địa chỉ không tồn tại trên bản đồ Internet.

## 2. Subnet — vì sao phải chia nhỏ mạng

**Subnet** là một dải IP con, gắn với một khu vực vật lý (AZ). Chia
`10.20.0.0/16` thành nhiều subnet nhỏ giống chia một toà nhà thành các phòng
có cửa riêng.

Vì sao không để một mạng phẳng (mọi máy chung một subnet)? Vì **rule chỉ đặt
được lên biên giới**. Nếu web server và database ở cùng subnet, không có "cửa"
nào để viết luật "chỉ web server được vào database" ở tầng subnet — vì cả hai
đã ở chung một phòng rồi. Tách subnet là **điều kiện tiên quyết** để áp dụng
nguyên tắc tối thiểu (chỉ mở đúng thứ cần).

## 3. Route table — bảng chỉ đường

Khi một máy gửi gói tin đi, nó tra route table: "địa chỉ này thuộc dải nào,
đẩy ra cửa nào".

Dòng đặc biệt nhất: `0.0.0.0/0` → *cửa mặc định* — nghĩa là "cái gì không rõ
đường thì đẩy ra đây".

Điểm cốt tử: **subnet không có dòng `0.0.0.0/0` trỏ ra Internet thì không ra
được, và Internet cũng không có đường vào.** Không cần firewall nào — đây là
cách database của HushStore được bảo vệ.

Ba trạng thái, đừng nhầm chỉ có hai:

| Route table của subnet | Máy trong đó | Internet vào được? |
|---|---|---|
| `0.0.0.0/0` → **Internet Gateway** | ra được Internet | **được** (nếu máy có IP public) |
| `0.0.0.0/0` → **NAT Gateway** | ra được Internet | **KHÔNG** |
| **không có** dòng `0.0.0.0/0` nào | không ra được | không |

App tier của HushStore ở hàng giữa: *có* đường ra (qua NAT) nhưng *không có*
đường vào. Db tier ở hàng cuối: không ra, không vào.

## 4. Cổng (port)

IP là địa chỉ toà nhà; **port** là số phòng. Một số port hay gặp trong hệ
thống này:

| Port | Dịch vụ | Trong HushStore |
|---|---|---|
| 22 | SSH | **cố ý đóng hoàn toàn** — xem buổi 6, 12 |
| 80 | HTTP | chỉ dùng để redirect sang 443 |
| 443 | HTTPS | cửa chính duy nhất |
| 5432 | PostgreSQL | chỉ mở giữa app tier và db tier |
| 8080 | API .NET | chỉ mở giữa ALB và container |

**Ephemeral port (cổng tạm).** Khi máy A gọi máy B ở port 443, máy A tự mở
một port ngẫu nhiên trong dải **1024–65535** để nhận câu trả lời. Nhớ kỹ dải
này — nó là nguyên nhân của phần rắc rối nhất trong thiết kế Network ACL
(buổi 8).

## 5. Stateful vs stateless — phân biệt quan trọng nhất

Hình dung một bảo vệ ở cổng khu chung cư.

**Stateful** (có ghi sổ): bảo vệ ghi "ông A vừa vào lúc 9h". Khi ông A đi ra,
bảo vệ nhìn sổ, cho ra luôn — không cần rule riêng cho chiều ra. **Security
Group hoạt động như vậy**: cho phép chiều vào thì chiều trả lời tự động được
phép.

**Stateless** (không ghi sổ): mỗi gói tin, vào hay ra, đều bị xét lại từ đầu.
**Network ACL hoạt động như vậy**: phải viết rule cho **cả hai chiều**.

Hệ quả trực tiếp: NACL buộc phải mở dải ephemeral 1024–65535 (vì câu trả lời
luôn đi về một port trong dải đó), và vì mở dải đó, hai port nguy hiểm (5432,
8080) vô tình nằm trong khoảng mở — nên phải chặn chúng bằng rule số nhỏ hơn.
Toàn bộ buổi 8 chỉ là hệ quả của đúng ý này.

## 6. NAT — máy không có IP public vẫn tải được phần mềm

App tier không có IP public nên Internet không vào được, nhưng nó *cần đi
ra* (tải image container, gọi API AWS).

**NAT (Network Address Translation)**: máy private gửi gói tin ra, NAT đổi
địa chỉ nguồn thành IP public của chính NAT, ghi nhớ, rồi khi trả lời về thì
dịch ngược và chuyển vào đúng máy.

Tính chất quan trọng: **NAT chỉ mở đường một chiều** — ra được, nhưng không
tạo đường cho ai gọi vào. Đó chính là điều ta muốn: đi ra tự do, không ai gõ
cửa được.

## Bài tập đọc code

Mở [`infra/tf/modules/network/vpc.tf`](../infra/tf/modules/network/vpc.tf) và
tìm:

1. CIDR block của VPC là gì? (Gợi ý: tài liệu gốc nói `10.20.0.0/16`.)
2. Có bao nhiêu `aws_subnet` resource? CIDR của từng subnet có khớp với bảng
   ở [buổi 5](05-vpc-va-subnet.md) không? (Buổi 5 sẽ giải thích chi tiết —
   ở đây chỉ cần xác nhận số lượng và CIDR khớp.)
3. Tìm resource `aws_route_table` cho app subnet — có bao nhiêu cái? Vì sao
   không dùng chung một route table cho cả `app-a` và `app-b`? (Trả lời bằng
   chính lý do ở mục 3 phía trên: một route table chỉ chứa được một dòng
   `0.0.0.0/0`.)

## Câu hỏi tự kiểm tra

1. `10.20.0.0/23` gộp được bao nhiêu địa chỉ? (Gợi ý: `/23` có 9 bit tự do.)
2. Vì sao một subnet có IP public nhưng route table không có dòng
   `0.0.0.0/0` thì Internet vẫn không vào được?
3. Vì sao NAT không thể dùng để bảo vệ database — mà phải dùng "không có
   route ra Internet"?
4. Stateful và stateless, cái nào cần khai báo rule cho chiều trả lời? Vì
   sao Security Group không cần?

<details>
<summary>Gợi ý đáp án</summary>

1. 2^9 = 512 địa chỉ, gộp `10.20.0.x` và `10.20.1.x`.
2. Route table quyết định gói tin đi đâu. Không có `0.0.0.0/0` nghĩa là máy
   không biết đường ra Internet, dù có IP public — có địa chỉ mà không có
   đường thì vẫn vô dụng, giống có địa chỉ nhà nhưng đường bị chặn.
3. NAT chỉ ngăn *chiều vào chủ động từ Internet*, nhưng máy phía sau NAT vẫn
   *có thể* bị gọi tới nếu có ai đó (kẻ tấn công đã chiếm được một máy trong
   VPC) chủ động kết nối trong nội bộ. "Không có route ra Internet" loại bỏ
   hoàn toàn khả năng có đường đi tới database từ bên ngoài, bất kể qua NAT
   hay không.
4. Stateless (NACL) cần khai rule cho cả hai chiều. Security Group là
   stateful nên chỉ cần rule chiều vào; chiều trả lời tự động được phép vì
   SG "ghi sổ" kết nối.

</details>

Buổi tiếp theo: [02-tls-https-dns.md](02-tls-https-dns.md).
