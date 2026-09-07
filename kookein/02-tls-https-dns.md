# Buổi 2 — DNS, TLS/HTTPS, chứng chỉ

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần I, mục 7–8 (dòng 152–268).
> Mục tiêu buổi này: hiểu vì sao gõ `hushstore.io.vn` lại ra đúng trang web
> đó, và vì sao trình duyệt hiện khoá xanh (hoặc cảnh báo đỏ).

## 1. DNS — dịch tên thành IP

Người dùng gõ `hushstore.io.vn`; máy cần một IP để gửi gói tin tới. **DNS**
làm việc dịch đó — giống tổng đài dịch tên người sang số điện thoại.

Hai loại bản ghi HushStore dùng:
- **A record**: tên → địa chỉ IP trực tiếp.
- **CNAME record**: tên → **một tên khác**, tên đó mới ra IP.

HushStore dùng CNAME vì địa chỉ của load balancer do AWS cấp và **có thể đổi**
(ví dụ khi ALB được tạo lại) — không thể ghi cứng một IP, phải trỏ qua tên.

## 2. TLS/HTTPS — hai việc khác nhau, đừng trộn

HTTPS = HTTP + mã hoá TLS. TLS giải quyết **hai việc khác nhau**:

1. **Bí mật** — người ngồi giữa (chủ quán wifi, nhà mạng) không đọc được nội
   dung.
2. **Danh tính** — bạn đang nói chuyện đúng với `hushstore.io.vn`, không phải
   với một máy chủ giả mạo.

Việc 1 chỉ cần thuật toán mã hoá. Việc 2 cần **chứng chỉ**.

## 3. Chứng chỉ là gì

Nền tảng là **cặp khoá**: máy chủ giữ **khoá riêng** (giữ kín tuyệt đối) và
**khoá công khai** (công bố cho cả thế giới). Thứ khoá công khai mã hoá thì
chỉ khoá riêng tương ứng giải được; ngược lại, thứ khoá riêng **ký** thì ai
cũng dùng khoá công khai để kiểm chứng — nên công bố khoá công khai vẫn an
toàn.

Chứng chỉ là một file chứa: **tên miền**, **khoá công khai**, **thời hạn**,
và **chữ ký số** của một **CA** (Certificate Authority — tổ chức cấp chứng
chỉ).

Trình duyệt được cài sẵn danh sách ~100–150 CA mà nó tin. Khi máy chủ xuất
chứng chỉ, trình duyệt kiểm chuỗi:

```
chứng chỉ của hushstore.io.vn
   ├─ ký bởi CA trung gian (intermediate)
   │     └─ ký bởi CA gốc (root) — CÓ trong danh sách cài sẵn
   ├─ tên miền trong chứng chỉ có khớp tên miền đang gõ?
   └─ hôm nay còn trong thời hạn?
```

Chuỗi đó gọi là **chain of trust**. Sai một mắt xích → trình duyệt **chặn
hẳn**, không phải cảnh báo nhẹ — vì không kiểm được chứng chỉ nghĩa là không
biết đang nói chuyện với ai.

Điểm dễ nhầm: **chứng chỉ không làm mã hoá**. Nó chỉ chứng minh khoá công
khai này thuộc tên miền này. Mã hoá là việc dùng khoá đó ở bước sau.

## 4. Thời hạn chứng chỉ đang ngắn dần

Trước 2020, chứng chỉ thường có hạn 2–3 năm. Hiện trần là **200 ngày**
(từ 2026-03-15), và sẽ giảm tiếp: 100 ngày (2027), rồi 47 ngày (2029) — theo
lịch của CA/Browser Forum (nhóm các CA + hãng trình duyệt tự thoả thuận).

Lý do: cơ chế thu hồi chứng chỉ (CRL, OCSP) không đáng tin trong thực tế —
trình duyệt thường bỏ qua lỗi khi không hỏi được. Nên ngành đi theo hướng
khác: để chứng chỉ **tự hết hạn nhanh** — thời hạn ngắn chính là cơ chế thu
hồi.

Hệ quả: gia hạn tay không còn khả thi (tới 2029 phải thay 8 lần/năm). Đây là
lý do thật để dùng **ACM** (AWS Certificate Manager) — tự động gia hạn — chứ
không chỉ vì nó miễn phí.

## 5. TTL — bốn thứ khác nhau, đừng gộp

"TTL" (time to live) bị dùng cho nhiều khái niệm khác nhau. Khi nghe ai nói
"TTL", phải hỏi lại TTL của cái gì:

| Loại | Nghĩa | Hết hạn thì sao |
|---|---|---|
| Thời hạn chứng chỉ | Sau ngày này trình duyệt chặn site | **xin cái mới** |
| TTL DNS | Máy khách cache kết quả phân giải bao lâu | **xin lại** |
| Thời hạn token (JWT, credential AWS) | Sau đó phải làm mới | **xin lại** |
| Thời hạn lưu trữ (log, backup) | Sau đó dữ liệu **bị xoá** | **mất luôn** |

Ba loại đầu hết hạn thì xin lại được. Loại thứ tư hết hạn là **mất vĩnh
viễn** — nhầm hai nhóm này là cách người ta tự xoá mất bằng chứng của chính
mình (ví dụ tưởng log "hết hạn thì tự làm mới" nhưng thực ra nó bị xoá).

## 6. TLS termination

**TLS termination** là chỗ mã hoá bị "bóc ra". Trong HushStore, ALB (load
balancer) bóc TLS rồi chuyển tiếp HTTP thường **vào trong VPC riêng**. Nhờ
vậy các container không cần tự quản chứng chỉ — một chỗ duy nhất (ALB) lo
việc đó.

Đánh đổi: đoạn từ ALB vào container là HTTP không mã hoá. Chấp nhận được vì
đoạn đó nằm hoàn toàn trong VPC riêng và Security Group chỉ cho đúng một
nguồn (ALB) gọi vào. Nếu cần mã hoá đầu-cuối (ví dụ hệ thống thanh toán) thì
phải mã hoá lại đoạn trong — giá phải trả là mỗi container cần chứng chỉ
riêng.

Hệ quả cần nhớ: ứng dụng bên trong nhận HTTP thô, nên nếu không đọc header
`X-Forwarded-Proto` thì nó tưởng người dùng đang dùng HTTP và sẽ redirect
vòng vòng (loop) — chi tiết nằm ở tầng ALB, xem buổi 9.

## Bài tập đọc code

Mở [`infra/tf/modules/alb/acm.tf`](../infra/tf/modules/alb/acm.tf) và tìm:

1. Chứng chỉ được xác thực bằng cách nào? (Gợi ý: tìm `validation_method` —
   nếu là `DNS` thì đó là cách ACM tự động gia hạn, không cần ai bấm tay.)
2. Tên miền nào được cấp chứng chỉ?

## Câu hỏi tự kiểm tra

1. Chứng chỉ chứng minh điều gì — và KHÔNG chứng minh điều gì?
2. Vì sao thời hạn chứng chỉ ngắn dần lại là chuyện tốt cho an ninh, dù gây
   phiền cho người vận hành?
3. TLS termination ở ALB nghĩa là đoạn nào trong hệ thống HushStore chạy
   HTTP không mã hoá? Vì sao vẫn chấp nhận được?
4. CNAME khác A record ở điểm nào, và vì sao HushStore chọn CNAME cho bản ghi
   trỏ tới ALB?

<details>
<summary>Gợi ý đáp án</summary>

1. Chứng chỉ chứng minh khoá công khai này thuộc về tên miền này (danh
   tính). Nó KHÔNG tự làm việc mã hoá — mã hoá là bước sau, dùng khoá đó.
2. Vì cơ chế thu hồi (CRL/OCSP) không đáng tin — nhiều trình duyệt bỏ qua
   lỗi khi không kiểm được. Thời hạn ngắn buộc chứng chỉ bị lộ khoá riêng
   cũng chỉ có tác dụng trong thời gian ngắn, giảm thiệt hại.
3. Đoạn từ ALB vào container (EC2, port 80/8080). Chấp nhận được vì nằm
   trong VPC riêng, và SG chỉ cho ALB gọi vào — không ai khác chen được vào
   giữa để nghe trộm.
4. A record trỏ trực tiếp một IP cố định; CNAME trỏ tới một tên khác. ALB có
   địa chỉ có thể đổi (AWS quản), nên phải dùng CNAME để không phải sửa DNS
   mỗi khi địa chỉ ALB thay đổi.

</details>

Buổi tiếp theo: [03-aws-va-terraform-co-ban.md](03-aws-va-terraform-co-ban.md).
