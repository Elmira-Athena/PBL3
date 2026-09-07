# Buổi 13 — RDS: database do AWS quản

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần III, mục "RDS — database do AWS quản" (dòng 973–1063).
> Buổi này trả lời: vì sao đổi từ SQL Server sang PostgreSQL, Multi-AZ thật
> ra bảo vệ cái gì (và không bảo vệ cái gì), và hai thuộc tính bảo mật dễ
> hiểu nhầm nhất của kết nối database: `publicly_accessible` và `SSL
> Mode=VerifyFull`.

## Cấu hình hiện tại

**PostgreSQL 17**, `db.t4g.micro`, 20GB `gp3`, **Multi-AZ**, nằm trong db
subnet (buổi 5) — chỉ hai SG rule cho phép chạm tới nó (buổi 6), và NACL
db là lớp ngoài cùng, đơn giản nhất (buổi 7).

## Vì sao đổi khỏi SQL Server Express

Lý do là **giới hạn của edition** (bản miễn phí), không phải kiến trúc hay
sở thích. Ba giới hạn của SQL Server Express, cái nào cũng không vá được
bằng cấu hình — phải đổi hẳn engine:

| | `sqlserver-ex` | `postgres` |
|---|---|---|
| Multi-AZ | **không hỗ trợ** (`MultiAZCapable = False`) | **có** (`True` cho cả `db.t3.micro` lẫn `db.t4g.micro`) |
| Read replica | chỉ có ở Enterprise Edition, đòi ≥ 4 vCPU | có |
| Trần dung lượng | **10 GB — vượt là bị TỪ CHỐI GHI** | không có trần |
| Họ instance khả dụng | **1** (chỉ `t3`) | **20**, kể cả Graviton (CPU ARM riêng của AWS, rẻ hơn Intel cùng cỡ) |
| CPU lúc rảnh | ~36% (baseline `t3.micro` chỉ 10%) → luôn phải trả CPU vay | dưới baseline |

Hai dòng đầu **tự kiểm chứng lại được**, không phải khẳng định suông:

```bash
aws rds describe-orderable-db-instance-options \
  --engine postgres --db-instance-class db.t4g.micro \
  --query 'OrderableDBInstanceOptions[0].MultiAZCapable'
```

(đổi `postgres` thành `sqlserver-ex` để so sánh — lệnh chỉ đọc, miễn phí).

Trần **10 GB** là dòng nguy hiểm nhất trong bảng: nó không làm hệ thống
*chậm dần*, nó làm hệ thống **hỏng đột ngột** — và hỏng đúng lúc dữ liệu
tăng tới ngưỡng, không phải lúc ai đó chủ động kiểm tra trước.

*(**CPU burstable, nhắc lại từ buổi liên quan:** `t3`/`t4g` chỉ cho dùng
miễn phí một mức CPU nền — 10% với cỡ `micro`. Vượt lên là "vay" credit và
bị tính thêm tiền phần vay đó.)*

## Multi-AZ — nghĩa là gì, và không nghĩa là gì

AWS giữ một bản **standby đồng bộ** ở AZ thứ hai, tự chuyển sang đó khi
primary hỏng. Nhưng theo đúng tài liệu AWS:

> *"You can't configure the secondary DB instance to accept database read
> activity."*

**Standby không phục vụ đọc.** Multi-AZ là **tính sẵn sàng** (availability
— hệ thống sống sót qua sự cố), **không phải chia tải đọc** (đó là việc
của một resource khác: read replica). Đây là cặp khái niệm dễ gộp nhầm
nhất khi viết báo cáo — phải tách rõ hai câu, đừng nói "Multi-AZ giúp chia
tải".

**Điều làm Multi-AZ khả thi trong đồ án này** (và là lý do việc đổi sang
Postgres không chỉ để "có Multi-AZ" mà còn giữ được khả năng tắt máy tiết
kiệm tiền):

> *"RDS for SQL Server doesn't support stopping a DB instance in a
> Multi-AZ deployment."*

PostgreSQL thì **stop được** dù đang Multi-AZ. Nghĩa là bật Multi-AZ
**không phá** cơ chế tắt tiền (`down.sh` + cost guard) — trên SQL Server
thì bật Multi-AZ đồng nghĩa với việc mất luôn khả năng tắt máy qua đêm. Đây
là khác biệt quyết định khiến việc đổi engine đáng công, không phải một
chi tiết phụ.

## Read replica — khác standby ở đúng một chữ

**Read replica khác standby của Multi-AZ ở đúng một điểm: ĐỒNG BỘ hay
KHÔNG.**

- Standby chép **đồng bộ** — mỗi thay đổi phải ghi xong ở cả hai nơi mới
  báo "thành công" cho client. Nên nó luôn khớp tuyệt đối với bản chính —
  đó chính là điều kiện để failover mà không mất dữ liệu.
- Read replica chép **bất đồng bộ** — bản chính báo xong trước, replica
  đuổi theo sau (từ vài mili-giây tới vài giây). Nhờ vậy nó **không** làm
  chậm bản chính, và **được phép** phục vụ truy vấn đọc; cái giá là dữ liệu
  đọc từ nó có thể **cũ hơn một chút** so với bản chính lúc đó.

**Read replica: hạ tầng đã có, mặc định TẮT** (`enable_read_replica =
false`). Đây là **default an toàn, không phải default tiết kiệm** — hai
thứ nghe giống nhưng lý do khác nhau:

> *"You can't stop a DB instance that has a read replica, or that is a
> read replica."*

Có replica gắn vào thì AWS **từ chối** cho stop primary ⇒ `down.sh` và cost
guard (buổi tổng kết sẽ nhắc) mất tác dụng, mà RDS lại tự khởi động lại sau
7 ngày ở trạng thái stopped — tức để qua đêm mà quên là tự động chảy tiền
lại. `down.sh` đã tự huỷ replica trước khi stop và hỏi thẳng AWS (không tin
file cấu hình local), nhưng nó chỉ chạy khi có người gõ lệnh.

⚠️ **Phải biết trước khi định bật nó lên:** chưa có dòng code nào trong
`src/` đọc từ replica (1 `AddDbContext`, 1 `UseNpgsql`, 0 tham chiếu tới
replica). Bật `enable_read_replica = true` lúc này chỉ thêm một instance
tính tiền, mà primary **không được giảm tải chút nào** — vì không ai đang
gửi truy vấn đọc sang đó.

## `publicly_accessible = false` — làm gì thật, không làm gì

Cần hiểu chính xác, vì tên cờ này dễ gây hiểu nhầm là "ẩn đi". Nó **không**
ẩn tên DNS của database — tên đó vẫn tra được từ Internet bình thường. Nó
làm một việc khác: tên đó **phân giải ra một địa chỉ IP private**
(`10.20.21.81`). Kẻ tấn công tra được tên, nhận về một IP, nhưng IP đó
**không tồn tại trên Internet** — nên không có đường nào gửi gói tin tới
nó từ ngoài.

Kiểm chứng thực tế: dò cổng 5432 tới endpoint đó từ một laptop bên ngoài →
kết quả là **timeout**, không phải "connection refused".

**Sự khác nhau giữa hai câu trả lời đó chính là bằng chứng, không phải chi
tiết vụn:**

- *"Connection refused"* nghĩa là gói tin **đã tới nơi** và có thứ gì đó
  chủ động trả lời "cổng này đóng" — tức là **đường đi tồn tại**, chỉ có
  cổng bị khoá ở đầu nhận.
- *"Timeout"* nghĩa là gói tin **đi vào hư không** — không ai trả lời, vì
  không có tuyến đường nào dẫn tới địa chỉ đó từ vị trí gửi.

Ta muốn vế thứ hai (timeout), vì nó chứng minh RDS **không có mặt** trên
Internet ở tầng định tuyến, chứ không chỉ "có mặt nhưng khoá cổng lại".

## `SSL Mode=VerifyFull` — thuộc tính bảo mật dễ mất nhất

Npgsql (thư viện .NET nối PostgreSQL) mặc định dùng `Prefer`: **mã hoá
nhưng KHÔNG xác thực chứng chỉ** — tức không chống được man-in-the-middle
(kẻ đứng giữa đường truyền, giả làm máy chủ để đọc/sửa dữ liệu mà hai đầu
không hề biết). `Require` cũng **chưa đủ** — ở Npgsql, `Require` vẫn không
kiểm chuỗi tin cậy (chain) của certificate. Chỉ **`VerifyFull`** mới vừa
xác thực cert vừa kiểm hostname khớp.

Điều nguy hiểm: tụt về mặc định (`Prefer`) là mất lớp bảo vệ này **trong im
lặng** — kết nối vẫn thành công, log vẫn sạch, không có cảnh báo nào. Bug
loại này chỉ lộ ra khi có ai chủ động kiểm tra cấu hình, không tự báo.

Vì cert của RDS do Amazon RDS CA cấp, và CA đó **không** nằm trong trust
store mặc định của hệ điều hành, image `api` và `migrator` phải tự cài sẵn
bundle CA (nhắc lại từ buổi 12 — đây chính là bước `update-ca-certificates`
phải chạy trước `USER appuser`), và chuỗi kết nối trỏ thẳng `Root
Certificate` vào file đó. Npgsql/libpq **không tự đọc trust store hệ
thống** như browser vẫn làm — đây là điểm khác biệt dễ quên nhất khi mới
chuyển từ web sang làm việc với database driver.

## Vì sao dùng RDS thay vì tự cài PostgreSQL lên EC2

Vì AWS lo hộ: backup tự động, vá lỗi engine, chứng chỉ TLS, snapshot, khôi
phục theo thời điểm (point-in-time recovery). Tự cài thì tất cả những việc
đó thành việc của nhóm — và đây chính là loại việc **dễ bị bỏ quên nhất**
khi dự án chạy lâu, không phải vì khó mà vì không ai nhớ phải làm định kỳ.

## Bài tập đọc code

1. Tìm cấu hình RDS trong [`infra/tf/modules/data/`](../infra/tf/modules/data/)
   — xác nhận `engine = "postgres"`, `instance_class = "db.t4g.micro"`,
   `publicly_accessible = false`.
2. Tìm hai biến `enable_multi_az` và `enable_read_replica` — xác nhận giá
   trị mặc định của module (không phải của `envs/prod`) là gì, và so với
   giá trị thật ở `envs/prod/variables.tf` (buổi 15 sẽ nói kỹ vì sao hai
   tầng này khác nhau).
3. Tìm chuỗi kết nối (connection string) trong cấu hình của `api` hoặc
   `migrator` — xác nhận có `SSL Mode=VerifyFull` và `Root Certificate=`
   trỏ tới file CA đã cài trong image.

## Câu hỏi tự kiểm tra

1. Vì sao "Multi-AZ giúp chia tải đọc" là một câu sai, dù nghe hợp lý?
2. Giải thích bằng lời của bạn: vì sao có read replica lại làm mất khả
   năng tắt máy qua đêm để tiết kiệm tiền?
3. `publicly_accessible = false` khiến việc dò cổng 5432 từ ngoài trả về
   kết quả gì, và vì sao kết quả đó (thay vì "connection refused") là bằng
   chứng mạnh hơn?
4. `Require` và `VerifyFull` khác nhau ở điểm nào trong Npgsql — vì sao chỉ
   một trong hai đủ để chống man-in-the-middle?

<details>
<summary>Gợi ý đáp án</summary>

1. Vì standby của Multi-AZ chỉ nhận replicate đồng bộ để phục vụ failover
   — theo đúng tài liệu AWS nó **không được cấu hình để nhận truy vấn đọc**.
   Muốn chia tải đọc phải dùng một resource khác hẳn: read replica.
2. Vì AWS chặn hành động stop một DB instance nếu nó đang có read replica
   gắn vào (hoặc chính nó là replica) — đây là ràng buộc của AWS, không
   phải giới hạn tự đặt ra. Nên hễ replica còn tồn tại, lệnh `down.sh` gọi
   stop sẽ bị AWS từ chối, và máy tiếp tục chạy (và tính tiền) qua đêm.
3. Trả về **timeout**, không phải "connection refused" — vì bằng chứng
   timeout chứng minh gói tin không có tuyến đường nào tới đích (đích là
   một IP private, không tồn tại trên Internet), còn "connection refused"
   chỉ chứng minh gói tin tới được nơi nhưng bị chặn ở cổng — tức đường đi
   vẫn tồn tại, chỉ là cổng đóng.
4. `Require` chỉ đảm bảo kết nối được mã hoá (TLS) nhưng không kiểm chuỗi
   tin cậy của certificate — nghĩa là chấp nhận cả cert tự ký hoặc cert giả
   miễn có mã hoá. `VerifyFull` kiểm cả chain (đúng do CA tin cậy cấp) và
   hostname (đúng đúng server đang kết nối tới, không phải một server khác
   mượn cert). Thiếu một trong hai kiểm tra đó, kẻ đứng giữa hoàn toàn có
   thể tự tạo một kết nối TLS "hợp lệ" giả và không bị phát hiện.

</details>

Buổi tiếp theo: [14-iam-va-bi-mat.md](14-iam-va-bi-mat.md).
