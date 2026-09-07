# Buổi 14 — IAM, bí mật, và khép lại Phần III

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần III, các mục "IAM", "Bí mật", "Quan sát hệ thống", "Điều khiển chi
> phí" (dòng 1064–1206) — đoạn cuối cùng của Phần III.
> Đây là buổi cuối đi sâu vào kiến trúc. Nó khép bằng đúng chủ đề mở đầu cả
> tài liệu: least-privilege — mỗi danh tính chỉ được làm đúng việc của nó,
> không hơn.

## IAM — bốn danh tính, mỗi cái một phạm vi

Đây là điểm **least-privilege** (chỉ cấp quyền tối thiểu cần để làm việc,
không hơn một quyền nào) rõ nhất của toàn thiết kế. Thay vì một danh tính
có mọi quyền, có **bốn** role tách rời:

| Role | Gắn vào | Được làm gì |
|---|---|---|
| `container-instance-role` | Bản thân máy EC2 | Tham gia ECS cluster, dùng SSM. **Không có quyền S3 nào, không đọc được bí mật nào** |
| `task-execution-role` | ECS agent, lúc *khởi động* container | Tải image từ ECR, ghi log, đọc **đúng 2** bí mật: connection string và khoá JWT |
| `task-app-role` | Container API, lúc *đang chạy* | **Chỉ** đọc/ghi S3 bucket ảnh + ECS Exec. Không RDS, không ECR, không bí mật |
| `task-execution-seeder-role` | ECS agent, khi chạy seeder | Đọc **đúng 1** bí mật: mật khẩu DB |

### Vì sao tách execution role và app role

Vì hai role phục vụ hai **thời điểm** khác nhau:

- Execution role dùng *trước khi* container chạy — tải image, lấy bí mật để
  tiêm vào biến môi trường.
- App role dùng *trong khi* container chạy — code trong container gọi API
  AWS (ví dụ ghi ảnh lên S3).

Tách ra nghĩa là **code trong container không bao giờ có quyền tự đọc bí
mật** — nó chỉ nhận được giá trị đã được tiêm sẵn vào biến môi trường từ
trước, không tự gọi Parameter Store được. Hậu quả cụ thể: kẻ tấn công
chiếm được quyền thực thi mã bên trong container (ví dụ qua một lỗ hổng của
ứng dụng) cũng **không lấy được thêm bí mật nào** — vì role đang gắn với
container lúc đó (app role) đơn giản là không có quyền `ssm:GetParameter`.

### Deny tường minh — chi tiết đáng nói nhất

Role của máy EC2 (`container-instance-role`) cần managed policy
`AmazonSSMManagedInstanceCore` để dùng được Session Manager (buổi 12). Nhưng
policy đó của AWS *có kèm sẵn* quyền đọc SSM Parameter nói chung — nghĩa là
nếu dừng lại ở đó, ai vào được máy qua SSM sẽ đọc luôn được mật khẩu
database từ Parameter Store.

Ta bịt lỗ đó bằng một statement **Deny tường minh** cho hành động
`ssm:GetParameter*` trên đường dẫn `/hushstore/*`. Nguyên tắc IAM cốt lõi ở
đây: **Deny luôn thắng Allow, bất kể Allow đến từ đâu** — kể cả khi Allow
đó nằm trong một managed policy do AWS viết sẵn.

Kiểm chứng bằng công cụ mô phỏng chính sách (IAM Policy Simulator) của AWS:
role của máy EC2 khi thử đọc bí mật trả về `explicitDeny` (bị cấm tường
minh), còn các quyền khác không liên quan (ví dụ đọc một bucket S3 không
được cấp) trả về `implicitDeny` (đơn giản là chưa được cấp). Đây là **hai
kết quả khác nhau**, và chính sự khác biệt đó chứng minh cái Deny tường
minh kia đang thật sự hoạt động — không phải trùng hợp vì "chưa cấp gì cả".

### Hai tập bí mật giao nhau bằng rỗng

`api`/`web`/`migrator` đọc được connection string + khoá JWT nhưng
**không** đọc được mật khẩu DB riêng. `seeder` đọc được mật khẩu DB nhưng
**không** đọc được hai cái kia. Nếu bốn task này dùng chung một role, cả
bốn sẽ thấy cả ba bí mật — thiết kế tách role chính là thứ giữ cho tập bí
mật mỗi bên đọc được là **rời nhau**, không phải một quy tắc lọc nào thêm
vào sau.

## Bí mật — không còn credential dài hạn nào

| Trước | Sau |
|---|---|
| File `.env` trên máy chủ | ECS tiêm bí mật từ SSM Parameter Store lúc khởi động container |
| Access key AWS ghi trong `appsettings.json` | Code lấy credential tạm thời từ task role, tự động hết hạn |
| SSH key dài hạn để deploy | SSM Session Manager, xác thực bằng IAM |
| Mật khẩu registry để tải image | ECR, xác thực bằng IAM role |

Đã kiểm chứng từ **trong container đang chạy**: không có access key nào
trong biến môi trường, không có file `.env` nào trên đĩa. Đây là khác biệt
căn bản với mô hình "credential dài hạn nằm sẵn đâu đó" — mọi thứ ở đây là
tạm thời và gắn với vòng đời của chính task.

**Vì sao Parameter Store mà không phải Secrets Manager?** Parameter Store
loại `SecureString` **miễn phí**; Secrets Manager tốn $0.40/bí mật/tháng.
Với 3 bí mật, khoảng cách là **$14/năm** cho một tính năng dự án không cần
tới — tự động luân chuyển (rotate) mật khẩu định kỳ. Đây là một quyết định
đánh đổi có ý thức, không phải bỏ quên.

## Quan sát hệ thống

- **CloudWatch Logs** — log của cả 4 container, giữ 3 ngày.
- **VPC Flow Logs** — ghi lại các kết nối **bị từ chối**. Đây là bằng chứng
  tầng mạng cho báo cáo bảo mật: nó cho thấy rule *thật sự* chặn, không chỉ
  là lời khẳng định "tôi gọi thử và không vào được". Mặc định tắt vì tốn
  phí theo lượng dữ liệu ingest.

Một quan sát đáng kể từ Flow Logs, đã nhắc sơ ở buổi 6: trong **một giờ**
đầu tiên hệ thống mở ra Internet, các máy quét tự động đã dò port 22
(SSH), 23 (Telnet), và 6379 (Redis) — **tất cả đều bị chặn**. Đây là bằng
chứng có giá trị nhất trong toàn báo cáo, chính vì nhóm **không** tạo ra
nó — nó là phản ứng thật của Internet công khai, không phải kịch bản demo
tự dựng.

## Điều khiển chi phí

NAT Gateway và ALB tính theo giờ, nên hệ thống được thiết kế để **tắt được
hoàn toàn** khi không dùng:

| Biến | Bật/tắt cái gì | Giá (ap-southeast-1) |
|---|---|---|
| `enable_nat` | NAT Gateway (số lượng theo `nat_gateway_count`) | $0.0590/giờ mỗi cái |
| `enable_alb` | ALB + target group + 2 ECS service | $0.0252/giờ |
| `instance_count` | 0 … `max_instance_count` EC2 | $0.0132/giờ mỗi cái |
| `enable_flow_logs` | VPC Flow Logs | phí ingest |
| `enable_deny_demo` | NACL rule 50 (buổi 7) | $0 |

RDS bật/tắt bằng lệnh riêng (`down.sh`/`up.sh`), không qua Terraform —
đúng vì stop/start một RDS instance không phải hành động khai báo hạ tầng,
mà là thay đổi trạng thái vận hành.

**Hai giá trị không phải công tắc, mà là kiến trúc — ghim cứng trong mã đã
commit:** `nat_gateway_count = 2` và `enable_multi_az = true`, đặt ở
`default` của `envs/prod/variables.tf`.

*(Luật ưu tiên của Terraform cần nhớ ở đây: giá trị khai trong
`terraform.tfvars` **đè lên** `default` viết trong `variables.tf`; không có
`.tfvars` hoặc không khai dòng đó thì Terraform tự dùng `default`. "Khai ở
tfvars" và "đặt default" nghe giống nhau nhưng khác hẳn về **độ bền**: một
cái theo máy của người chạy, một cái theo mã nguồn — sang máy khác/clone
lại là mất theo hoặc giữ theo tuỳ loại.)*

Cố ý **không** khai `nat_gateway_count`/`enable_multi_az` trong
`terraform.tfvars`, vì file đó bị `.gitignore` — giá trị khai ở đó không đi
theo khi clone sang máy khác hoặc chạy trên CI. Ghim ở `default` trong mã
đã commit đảm bảo một lần clone lại **không** làm hạ tầng âm thầm rơi về 1
NAT / không Multi-AZ, dù cả hai đặc tính đó đã được ghi thành cam kết trong
tài liệu này.

### Đọc bảng giá — hai loại số, đừng trộn

- 📏 **Số ĐO THẬT** — lấy từ hoá đơn/Cost Explorer của chính tài khoản này,
  sau khi hệ thống đã chạy. Đáng tin nhất, nhưng chỉ có cho cấu hình
  **cũ** (SQL Server).
- 🏷️ **Số NIÊM YẾT** — tra từ bảng giá công bố của AWS. Đúng về đơn giá,
  nhưng **chưa gồm** những khoản chỉ lộ ra khi chạy thật — CPU surplus là
  ví dụ đắt nhất.

Cấu hình PostgreSQL hiện tại **chưa có số đo thật nào** — mới chạy từ
2026-09-06. Mọi con số cho nó dưới đây đều là 🏷️.

**Con số phản trực giác đã đo được trên cấu hình cũ:** trước đợt 7, RDS là
khoản đắt nhất — 📏 **$0.098/giờ**, trong đó 📏 **$0.0674 là CPU credit
surplus** (số đo thật từ Cost Explorer sau 13,67 giờ chạy). Lý do:
`db.t3.micro` là máy *burstable*, chỉ miễn phí 10% CPU, mà SQL Server
Express **không tải gì** vẫn ngồi ~36% CPU — riêng phần vượt hạn mức đã đắt
xấp xỉ NAT + ALB cộng lại. Tệ hơn nữa: RDS **chỉ có** chế độ `unlimited`
(vay CPU credit rồi tính tiền phần vay), không có lựa chọn `standard`
(chạy chậm lại, không tính thêm) như EC2 có. Nên đòn bẩy duy nhất để giảm
khoản này là **đổi engine**, không phải chỉnh tham số.

Sau khi đổi sang PostgreSQL (idle dưới baseline, không vay credit):

| | Single-AZ | Multi-AZ | Loại số |
|---|---|---|---|
| `postgres` `db.t4g.micro` | $0.025/giờ | **$0.051/giờ** | 🏷️ niêm yết |
| `sqlserver-ex` `db.t3.micro` (chỉ phần instance) | $0.031/giờ | không tồn tại | 🏷️ niêm yết |
| `sqlserver-ex` — CPU surplus thực tế phải trả thêm | **+$0.067/giờ** | — | 📏 đo thật |

Cộng thêm storage (🏷️ ~$0.006/giờ), Multi-AZ PostgreSQL khoảng
**$0.057/giờ** — **vẫn rẻ hơn** $0.098 của SQL Server single-AZ trước đây.
Tức đổi engine vừa **tăng khả dụng** (Multi-AZ, thứ SQL Server Express
không hỗ trợ) **vừa giảm tiền** — đây là lý lẽ chính của việc chuyển sang
PostgreSQL ở đợt 7 (buổi 13 đã nói phần kỹ thuật, đây là phần chi phí bù
vào).

🔴 **Một khoản Multi-AZ mà `down.sh` không tắt được:** storage được cấp phát
ở **cả hai** AZ và AWS tính tiền cả hai **kể cả khi instance đã stopped**.
Sàn chi phí của dự án vì thế tăng khoảng **+$2.3/tháng chạy vĩnh viễn** — nhỏ,
nhưng là loại chi phí không có công tắc nào chạm tới, nên phải ghi vào bảng
chứ không nằm trong đầu ai đó.

⚠️ `HS_RATE_RDS_UP = 0.098` trong `lib.sh` **cố ý chưa sửa**: nó là số đo
thật trên SQL Server, nay chỉ còn là **cận trên** (ước lượng an toàn, không
phải số thật) cho PostgreSQL. Đổi bằng một số ước lượng khác sẽ biến bảng
chi phí từ "đã đo" thành "nghe hợp lý" mà không có gì đánh dấu sự khác biệt
đó — chỉ sửa khi có số thật sau 24–48h chạy.

## Bài tập đọc code

1. Tìm bốn IAM role trong [`infra/tf/modules/security/`](../infra/tf/modules/security/)
   hoặc module tương ứng — xác nhận đúng 4 role như bảng, và tìm statement
   **Deny** tường minh cho `ssm:GetParameter*`.
2. Tìm nơi khai `nat_gateway_count` và `enable_multi_az` — xác nhận
   `default` ở `envs/prod/variables.tf` là `2` và `true`, còn `default` ở
   cấp module (nếu có, dùng ở nơi khác) có thể khác.
3. Tìm `terraform.tfvars` trong `.gitignore` — xác nhận file đó thật sự bị
   loại khỏi git, giải thích lại bằng lời của bạn tại sao đó là lý do phải
   ghim `nat_gateway_count`/`enable_multi_az` ở `default` thay vì ở đó.

## Câu hỏi tự kiểm tra

1. Vì sao tách `task-execution-role` khỏi `task-app-role` khiến việc chiếm
   được container không giúp kẻ tấn công đọc thêm bí mật nào?
2. Deny tường minh khác implicit deny (không được cấp quyền) ở điểm nào,
   và vì sao Policy Simulator trả hai kết quả khác nhau là bằng chứng có
   giá trị?
3. Vì sao Multi-AZ PostgreSQL ($0.051/giờ niêm yết) vẫn "rẻ hơn" Single-AZ
   SQL Server Express ($0.098/giờ đo thật), dù đang so một bên Multi-AZ
   với một bên Single-AZ?
4. Vì sao `nat_gateway_count = 2` phải nằm ở `default` trong
   `variables.tf` thay vì ở `terraform.tfvars`?

<details>
<summary>Gợi ý đáp án</summary>

1. Vì quyền đọc bí mật (Parameter Store) chỉ được cấp cho
   `task-execution-role` — role này chỉ hoạt động ở giai đoạn ECS agent
   khởi động container (tải image, tiêm biến môi trường), **trước khi**
   code ứng dụng bắt đầu chạy. Khi container đã chạy, nó mang
   `task-app-role`, một role hoàn toàn khác không có quyền đọc Parameter
   Store. Chiếm quyền thực thi mã bên trong container chỉ chạm được vào
   `task-app-role`, không chạm được `task-execution-role`.
2. Implicit deny nghĩa là đơn giản chưa có statement nào cấp quyền đó —
   "im lặng = không được". Explicit deny là một statement `Deny` được viết
   ra tường minh, và theo luật IAM nó thắng **bất kể** Allow từ đâu tới,
   kể cả managed policy của AWS. Hai kết quả (`explicitDeny` vs
   `implicitDeny`) khác nhau chứng minh rằng statement Deny ta viết thật sự
   tồn tại và đang được đánh giá — nếu nó không hoạt động, quyền đọc bí mật
   (được `AmazonSSMManagedInstanceCore` cấp) sẽ trả về được phép, không
   phải bị cấm.
3. Vì hai bảng giá đang so sánh lệch cấp độ khả dụng — nhưng điểm mấu chốt
   là: SQL Server Express *thậm chí không hỗ trợ* Multi-AZ, nên $0.098 kia
   là "trần chi phí" duy nhất nó có thể đạt được (Single-AZ). PostgreSQL
   Multi-AZ ở $0.057 (gồm storage) vừa **vượt hơn** khả năng cao nhất của
   SQL Server Express (có Multi-AZ), vừa **rẻ hơn** khoản Single-AZ duy
   nhất mà SQL Server Express có thể cung cấp — vì phần lớn chi phí của
   SQL Server không nằm ở giá niêm yết mà ở CPU surplus không có cách tắt.
4. Vì `terraform.tfvars` bị `.gitignore`, nghĩa là nó chỉ tồn tại trên máy
   của người đang gõ lệnh — không đi theo khi clone sang máy khác hoặc lên
   CI. Nếu giá trị `2`/`true` chỉ khai ở đó, một lần clone lại repo (máy
   mới, CI mới) sẽ khiến Terraform lặng lẽ dùng `default` khác (ví dụ `1`
   NAT, không Multi-AZ) mà không ai nhận ra, vì không có lỗi nào được báo.
   Ghim ở `default` trong `variables.tf` (file có commit) đảm bảo giá trị
   đó đi theo mã nguồn, không theo máy.

</details>

Buổi tiếp theo: [15-tong-ket-doi-chieu-de-bai.md](15-tong-ket-doi-chieu-de-bai.md).
