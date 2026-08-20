# Nhật ký triển khai hệ thống — HushStore trên AWS

> **Tài liệu này kể lại việc đã làm, theo thứ tự, và vì sao mỗi bước phải làm
> như vậy.** Không có code. Mục đích là để người đọc hiểu *quy trình* — thứ mà
> đọc file cấu hình không thấy được — và biết trước những chỗ dễ sai.
>
> Đọc kèm [thiet-ke-he-thong-aws.md](thiet-ke-he-thong-aws.md) (giải thích *cái
> gì* và *vì sao thiết kế vậy*). Tài liệu này giải thích *làm thế nào*.

---

# Phần I — Doanh nghiệp triển khai hạ tầng như thế nào

Trước khi vào nhật ký, cần hiểu bối cảnh: vì sao một công ty không làm theo cách
đơn giản nhất.

## Cách "tự nhiên" và vì sao nó sụp đổ

Cách đầu tiên ai cũng nghĩ tới: mở console AWS, bấm tạo máy chủ, SSH vào, cài
phần mềm, copy code lên, chạy. Nhanh, và với một người thì hoạt động tốt.

Nó sụp đổ ở bốn điểm, và luôn sụp theo đúng thứ tự này:

**Tháng thứ nhất — không ai biết hệ thống có những gì.** Bạn đã bấm gì, mở port
nào, cấp quyền gì? Không có ghi chép. Người mới vào phải đi đọc từng trang
console để dựng lại bức tranh.

**Tháng thứ hai — không dựng lại được.** Máy chủ chết, hoặc cần thêm môi trường
staging. Bạn phải bấm lại từ đầu, và chắc chắn sẽ khác bản gốc ở đâu đó. Sự khác
biệt đó chính là nguồn của loại bug tệ nhất: "chạy được trên production nhưng
không chạy được trên staging".

**Tháng thứ ba — không dám sửa.** Không ai biết cái Security Group đó mở port
8080 để làm gì, nên không ai dám đóng. Rule cứ tích tụ, chỉ thêm không bớt. Đến
lúc chấm bảo mật thì hệ thống mở rộng hơn cần thiết rất nhiều — mà không ai cố
ý làm vậy.

**Tháng thứ tư — deploy thành nghi lễ.** Chỉ một người biết cách deploy. Người
đó nghỉ phép thì cả nhóm đứng.

## Cách doanh nghiệp làm, và vì sao

Bảy nguyên tắc dưới đây không phải để cho "chuẩn". Mỗi cái sinh ra để chữa đúng
một trong bốn cái chết trên.

**1. Hạ tầng viết thành code (IaC).** Toàn bộ hạ tầng nằm trong file văn bản
trong Git. Muốn biết hệ thống có gì thì đọc file, không phải đi soi console. Muốn
biết ai đổi gì thì xem `git log`. Muốn dựng lại thì chạy lại. Sửa thì mở pull
request và có người review — **rule mở quá rộng bị bắt trước khi nó tồn tại**,
không phải sau khi bị tấn công.

**2. Xem trước khi làm.** Terraform có `plan`: nó nói trước "sẽ tạo 3, sửa 1,
xoá 2" và cụ thể là những gì. Không bao giờ thay đổi hạ tầng mà chưa đọc plan.
Đây là điều console không cho bạn.

**3. State dùng chung và có lock.** Terraform ghi lại nó đã tạo gì. File đó phải
nằm ở chỗ chung (S3), không nằm trên laptop một người. Và phải có **lock**: hai
người apply cùng lúc thì người thứ hai bị chặn. Không có lock thì hai tiến trình
ghi lên cùng một state và hạ tầng lệch khỏi mô tả — trạng thái khó chữa nhất.

**4. Không có credential dài hạn.** Không SSH key, không access key ghi trong
file. Máy chủ và pipeline dùng **role** — danh tính không có mật khẩu, credential
được cấp tạm thời và tự hết hạn. Lý do đơn giản: bí mật dài hạn thì cuối cùng sẽ
bị commit vào Git, dán vào chat, hoặc để trong máy của người đã nghỉ.

**5. Image bất biến, gắn theo phiên bản.** Mỗi lần build ra một image gắn nhãn
bằng mã commit, và **không bao giờ ghi đè**. Nhãn `latest` bị cấm vì nó nói dối:
"latest" hôm nay và hôm qua là hai thứ khác nhau, nên rollback không có nghĩa gì.
Có nhãn cố định thì rollback là trỏ về nhãn cũ — một lệnh, chắc chắn đúng.

**6. Thay đổi database là cửa kiểm soát, không phải hiệu ứng phụ.** Cập nhật
schema chạy như một bước riêng, và **nếu nó thất bại thì không deploy** — bản cũ
vẫn phục vụ. Điều tuyệt đối không làm: để app tự cập nhật schema lúc khởi động
(phần "Issue #7" giải thích vì sao).

**7. Chi phí là một yêu cầu, không phải chuyện của kế toán.** Ai dựng hạ tầng
thì người đó chịu trách nhiệm về hoá đơn. Nên phải có cách tắt, có cảnh báo
ngân sách, và có người hiểu từng đồng đi đâu.

Bảy nguyên tắc này là khung của toàn bộ nhật ký dưới đây.

---

# Phần II — Nhật ký triển khai

Phase 1 gồm 17 việc, nhóm thành 10 giai đoạn. Mỗi giai đoạn ghi ba điều: **làm
gì**, **vì sao phải theo thứ tự đó**, và **chỗ dễ sai**.

## Giai đoạn 0 — Khảo sát hiện trạng và quyết định làm lại

**Làm gì.** Đọc lại hệ thống đang có: một bộ script bash gọi AWS CLI, 310 dòng,
11 bước, dựng một EC2 với nginx cài trực tiếp trên máy. Đối chiếu với yêu cầu đề
bài thì thiếu 3 trên 5 thành phần bắt buộc: **không có Network ACL, không có
Application Load Balancer, không có kịch bản kiểm thử**. Ngoài ra deploy đang
dùng SSH key dài hạn, và schema database được cập nhật ngầm lúc app khởi động.

**Quyết định: viết lại từ đầu bằng Terraform**, giữ bộ script cũ làm tài liệu
tham chiếu (đọc để biết hệ thống cũ làm gì, nhưng **không chạy lại** — nó tạo
resource nằm ngoài Terraform state, và nó mở port 22).

**Vì sao không sửa dần.** Vì thiếu tới 3/5 thành phần và cả cách deploy đều phải
đổi. Sửa dần trong trường hợp này tốn hơn làm lại, và nguy hiểm hơn: một hạ tầng
nửa-Terraform-nửa-thủ-công là thứ không ai dám chạm.

**Chỗ dễ sai.** Xoá hệ thống cũ trước khi hệ thống mới chạy được. Cách đúng: giữ
song song, dùng dải IP khác (`10.20.0.0/16` thay vì `10.0.0.0/16`) để hai mạng
không đụng nhau, và chỉ chuyển tên miền khi bản mới đã verify xong.

## Giai đoạn 1 — Nơi lưu state

**Làm gì.** Việc đầu tiên không phải tạo mạng, mà tạo chỗ lưu **state** của
Terraform: một S3 bucket, bật versioning, bật lock.

**Vì sao đây là việc đầu tiên.** Vì mọi thứ sau đó đều ghi vào state. Nếu state
nằm trên laptop thì chỉ một người dựng được hạ tầng; nếu nó mất thì Terraform
"quên" hết những gì đã tạo — và lần apply sau nó sẽ cố tạo lại tất cả, trong khi
resource cũ vẫn còn. Versioning là lưới an toàn: state hỏng thì quay lại bản
trước.

**Chỗ dễ sai.** Bỏ qua lock vì "chỉ mình mình dùng". Nhưng lock không chỉ chống
hai người — nó còn chống **một người apply hai lần** (mở hai terminal, hoặc
apply lại khi lần trước chưa xong). Đó là tình huống thật, và nó làm state lệch.

## Giai đoạn 2 — Mạng

**Làm gì.** VPC, 6 subnet, Internet Gateway, route table, NAT Gateway, 3 Network
ACL, S3 Gateway Endpoint. NAT Gateway được đặt sau một công tắc bật/tắt ngay từ
đầu.

**Vì sao mạng trước tiên.** Vì mọi thứ khác đều phải *nằm trong* một subnet nào
đó. Security Group cần biết VPC nào; RDS cần biết subnet nào; ALB cần biết subnet
nào. Đây là nền, không có nó thì không dựng được gì.

**Vì sao NAT có công tắc ngay từ ngày đầu, chứ không để cuối.** Vì NAT Gateway
là **khoản đắt nhất trong nhóm resource tính theo giờ** ($0.059/giờ, không có
bậc miễn phí). Nếu để việc "làm cách tắt" xuống cuối dự án, thì suốt thời gian
phát triển nó chạy 24/7. Kiến trúc phải *sinh ra đã tắt được*, không phải được
gắn thêm khả năng đó về sau.

**Chỗ dễ sai — và đây là chỗ đáng học nhất của cả giai đoạn.** Viết NACL mà không
nghĩ tới stateless. Người mới thường viết rule cho chiều vào rồi tưởng xong.
Kết quả: kết nối ra Internet không hoạt động, vì câu trả lời quay về bị chặn ở
chiều vào. Chữa thì phải mở dải ephemeral 1024–65535 — nhưng mở xong lại vô tình
mở luôn port 1433 và 8080 ra Internet, vì hai port đó nằm trong dải. Phải chặn
chúng bằng rule **số nhỏ hơn**. Toàn bộ chuỗi suy luận này được giải thích ở
Phần III của tài liệu thiết kế.

## Giai đoạn 3 — Security Group

**Làm gì.** Ba SG tham chiếu lẫn nhau: `sg-alb` → `sg-web` → `sg-rds`.

**Vì sao tham chiếu SG chứ không ghi dải IP.** Vì IP của EC2 đổi mỗi lần máy được
tạo lại, còn SG thì không. Và nó chặt hơn: không có cách nào giả mạo được việc
"tôi đang gắn SG kia".

**Chỗ dễ sai.** Tham chiếu vòng. `sg-alb` cần trỏ tới `sg-web`, và `sg-web` cần
trỏ tới `sg-alb`. Nếu khai rule *bên trong* định nghĩa SG thì Terraform gặp vòng
lặp và không dựng được. Cách đúng: tạo ba SG rỗng trước, rồi khai rule thành
resource riêng bên ngoài.

## Giai đoạn 4 — Kho chứa: ECR và S3

**Làm gì.** 4 ECR repository (api, web, migrator, seeder) và 3 S3 bucket (ảnh sản
phẩm, artifact, log của ALB).

**Vì sao ECR chứ không phải một registry công cộng.** Vì với ECR, việc xác thực
đi bằng IAM role — **không cần mật khẩu registry nào**. Dùng registry ngoài thì
phải lưu một token dài hạn ở đâu đó, và đó đúng là thứ nguyên tắc số 4 muốn loại
bỏ.

**Bật IMMUTABLE tag.** Một nhãn image đã push thì **không thể ghi đè**. Ý nghĩa:
nó biến rollback từ "hy vọng" thành "chắc chắn". Nếu ghi đè được thì nhãn cũ có
thể đã bị thay nội dung, và trỏ về nó không đảm bảo lấy lại đúng bản đang chạy
tốt hôm qua.

**Bucket ảnh sản phẩm là ngoại lệ duy nhất.** Nó đã tồn tại và **đang chứa ảnh
thật** — URL của những ảnh đó đang nằm trong database. Tạo lại bucket là làm chết
toàn bộ ảnh sản phẩm. Nên bucket này được *nhập* vào Terraform quản lý thay vì
tạo mới, và nó được đặt cờ **không cho phép xoá tự động**: lệnh destroy sẽ *thất
bại* ở bucket đó nếu nó còn file. Đó là lưới an toàn có chủ ý.

**Chỗ dễ sai.** Chạy `terraform destroy` để "dọn cho sạch" mà không biết nó sẽ
kéo theo bucket dữ liệu thật.

## Giai đoạn 5 — Database và bí mật

**Làm gì.** RDS SQL Server Express trong db subnet, không public. Mật khẩu sinh
tự động, lưu vào SSM Parameter Store dạng mã hoá cùng với connection string và
khoá JWT. Database được tạo **rỗng** — không có schema, không có dữ liệu.

**Vì sao tạo rỗng.** Vì schema là việc của migration, và dữ liệu là việc của
seeder. Cả hai đều phải chạy được **nhiều lần cho cùng kết quả** và phải nằm
trong Git. Nếu dựng database bằng cách khôi phục một bản backup nào đó, thì không
ai biết chính xác trong đó có gì, và không dựng lại được từ đầu.

**Vì sao mật khẩu sinh tự động và không ai biết.** Vì con người không cần biết
nó. App lấy connection string từ Parameter Store; seeder lấy mật khẩu từ
Parameter Store. Mật khẩu nào có người biết thì mật khẩu đó sẽ xuất hiện trong
chat, trong file ghi chú, trong shell history.

**Chỗ dễ sai.** Đặt `publicly_accessible = true` cho tiện kết nối từ máy cá nhân
lúc phát triển. Đó là lỗ hổng lớn nhất mà người mới hay tạo ra: database mở ra
Internet, chỉ còn mật khẩu bảo vệ. Cách đúng là vào bằng SSM, hoặc chạy việc cần
làm như một task bên trong VPC.

## Giai đoạn 6 — Đóng gói ứng dụng và sửa 4 điểm trong code

**Làm gì.** Đóng gói 4 image, và sửa 4 chỗ trong ứng dụng:

| Sửa gì | Vì sao |
|---|---|
| Thêm health check có kiểm tra database | Endpoint cũ trả 200 mà không chạm DB → ALB báo healthy dù database đã chết |
| Đọc header `X-Forwarded-*` | ALB bóc TLS rồi chuyển HTTP vào; không đọc header này thì app tưởng request là HTTP và có thể tạo vòng lặp redirect |
| Bỏ access key ghi cứng, dùng role | Loại bỏ credential dài hạn cuối cùng còn lại trong code |
| **Bỏ cập nhật schema lúc app khởi động** | Xem Issue #7 — quan trọng nhất trong bốn cái |

**Vì sao đóng gói Blazor vào image thay vì copy file lên máy chủ.** Vì bản build
frontend được **nướng sẵn vào image**. Nghĩa là không còn bước "đồng bộ file lên
server" — bước dễ sai và dễ bị bỏ quên nhất. Image nào chạy thì frontend đúng
phiên bản đó, không thể lệch.

**Chỗ dễ sai.** Build image trên máy Mac dùng chip ARM rồi push lên, trong khi
EC2 là x86 — container sẽ không chạy với lỗi rất khó hiểu. Phải chỉ định rõ kiến
trúc đích khi build.

## Giai đoạn 7 — IAM và cluster

**Làm gì.** Bốn IAM role, launch template, Auto Scaling Group, ECS cluster,
capacity provider.

**Vì sao tách bốn role thay vì một.** Chi tiết ở tài liệu thiết kế. Ý chính: mỗi
role ứng với một *thời điểm* và một *chủ thể* khác nhau — bản thân máy chủ, ECS
agent lúc khởi động container, code trong container lúc chạy, và ECS agent khi
chạy seeder. Gộp lại thành một role thì mọi chủ thể đều có mọi quyền, và một chỗ
bị chiếm là mất tất cả.

**Vì sao ASG có `min = 0`.** Để tắt được thật. Đặt về 0 thì máy bị xoá **cùng ổ
đĩa**, chi phí về 0. Điều này chỉ an toàn vì **không có state nào nằm trên máy** —
mọi thứ nằm trong image và Parameter Store. Đặt về 1 là một máy mới hoàn toàn
được dựng lại và tự hoạt động. Việc đó đã được kiểm chứng, và nó là bằng chứng
cho tính chất "hạ tầng bất biến".

**Chỗ dễ sai.** Bật chế độ tự động điều chỉnh số máy của capacity provider. Nếu
bật, nó sẽ tự tạo một chính sách scaling và **tranh quyền với Terraform** về số
lượng máy: Terraform đặt 0, chính sách đó đặt lại 1, và cứ thế. Ta tắt nó và để
số máy do Terraform quyết định — một nguồn sự thật duy nhất.

## Giai đoạn 8 — Task definition, ALB, và service

**Làm gì.** 4 task definition, ALB với chứng chỉ ACM, 2 target group, listener
rule allowlist Host, và 2 ECS service.

**Vì sao service phải dựng sau ALB.** Vì service cần đăng ký container vào target
group, và AWS **từ chối tạo service nếu target group chưa gắn vào một load
balancer nào**. Đây là ràng buộc cứng của AWS, không phải lựa chọn — nên hai thứ
này được bật/tắt cùng nhau bằng một công tắc.

**Chứng chỉ TLS.** ACM cấp miễn phí, tự động gia hạn. Nhưng để cấp, ACM đòi bạn
chứng minh sở hữu tên miền bằng cách thêm một bản ghi DNS. Bản ghi đó **phải giữ
mãi** — ACM đọc lại nó mỗi lần gia hạn. Xoá đi thì HTTPS chết âm thầm sau 13
tháng.

**Chỗ dễ sai.** Xem Issue #4 và #5 — cả hai đều ở giai đoạn này và cả hai đều
làm mất nhiều giờ.

## Giai đoạn 9 — Dựng schema, nạp dữ liệu, và verify

**Làm gì.** Chạy migration như một task một lần → kiểm tra mã thoát → chạy seeder
→ kiểm tra dữ liệu → verify ba tiêu chí nghiệm thu.

**Ba tiêu chí, và vì sao chọn đúng ba cái này:**

1. **Website mở được bằng HTTPS trên tên miền thật.** Chứng minh chuỗi
   DNS → Cloudflare → ALB → target group → container hoạt động hoàn chỉnh.
2. **Đăng nhập lấy được JWT.** Chứng minh API nói chuyện được với database, và
   xác thực hoạt động. Kiểm luôn nội dung token: không có mật khẩu, không có dữ
   liệu nhạy cảm — vì ai cũng giải mã được phần thân của JWT.
3. **Upload ảnh lên S3 trả về URL thật, tải lại được.** Đây là tiêu chí **quan
   trọng nhất và dễ bị bỏ sót nhất**: nó là bằng chứng duy nhất cho **đường
   ghi** — rằng IAM role của container thật sự thay thế được access key ghi cứng.
   Hai tiêu chí đầu chỉ chứng minh đường đọc. Nếu không test cái này, có thể tới
   lúc bảo vệ mới phát hiện việc bỏ access key đã làm hỏng upload.

**Vì sao seed phải chạy như một task trong VPC, không phải từ laptop.** Vì
database không có đường ra Internet, nên laptop không kết nối tới được — đúng
như thiết kế. Chạy seed như một task bên trong VPC là cách duy nhất, và nó có
lợi ích kèm theo: mật khẩu đi qua đường bí mật của ECS, **không bao giờ xuất hiện
trên terminal hay trong shell history**.

**Chỗ dễ sai.** Verify trên database rỗng rồi kết luận "hệ thống chạy được". Task
verify lần đầu chạy khi chưa seed, nên nó chỉ chứng minh app không crash. Phải
verify **lại** sau khi có dữ liệu thật — đó mới là lúc biết seed, app và database
đồng ý với nhau.

## Giai đoạn 10 — Kiểm thử bảo mật và viết báo cáo

**Làm gì.** 11 kịch bản tấn công từ laptop, lưu lệnh và output thật vào một thư
mục bằng chứng, rồi viết báo cáo **chỉ trích số từ đó** — không viết tay số nào.

**Vì sao lưu output thô.** Vì báo cáo không có bằng chứng thì chỉ là lời khai.
Lưu nguyên lệnh và nguyên kết quả thì người chấm kiểm lại được, và bản thân nhóm
cũng chạy lại được sau này để biết có gì thay đổi.

**Vì sao thu bằng chứng ngay trong cùng cửa sổ đã bật.** Vì hạ tầng đang tính
tiền theo giờ. Mở một cửa sổ riêng chỉ để test là trả tiền hai lần cho cùng một
việc.

**Một mẹo đáng nhớ.** Để chứng minh rule chặn-theo-IP hoạt động, cần *hai* điểm
quan sát: một máy bị chặn và một máy không bị chặn. Nhưng không cần dựng máy thứ
hai — **proxy của Cloudflare chính là điểm quan sát thứ hai**. Gọi trực tiếp vào
ALB thì nguồn là IP laptop (bị chặn → timeout); gọi qua tên miền thì nguồn là IP
của Cloudflare (không bị chặn → 200). Cùng một lệnh, khác biệt duy nhất là địa
chỉ nguồn.

---

# Phần III — Các issue chính

Đây là những lỗi **cấu trúc** — loại mà bất cứ ai làm hệ thống tương tự cũng sẽ
gặp. Mỗi cái ghi: hiện tượng, nguyên nhân thật, và bài học.

## Issue #1 — `terraform apply` báo thành công nhưng hệ thống không hoạt động

**Hiện tượng.** Apply xanh, Auto Scaling Group báo máy Healthy, nhưng ECS cluster
**không có máy nào** và không container nào chạy.

**Nguyên nhân.** ASG kiểm tra sức khoẻ ở mức "EC2 có đang running không". Nó
**không** kiểm tra "ECS agent trên máy đó có đăng ký vào cluster chưa". Một máy
boot lên mà agent chết vẫn làm apply xanh **và** ASG báo Healthy. Không có tín
hiệu nào ở tầng Terraform cho biết điều đó.

**Bài học — và đây là bài học lớn nhất của cả dự án.** *Apply thành công không
đồng nghĩa với hệ thống dùng được.* Hai chuyện khác nhau: Terraform chỉ đảm bảo
"AWS đã nhận lệnh tạo resource"; nó không biết gì về việc phần mềm bên trong
resource đó có hoạt động không. Với mọi hạ tầng, phải có một **bước kiểm tra sức
khoẻ thật sự** sau khi apply. Ta viết một script riêng cho việc đó, và nó kiểm cả
trạng thái kết nối của agent chứ không chỉ sự tồn tại của máy.

## Issue #2 — Đoạn khởi tạo máy tự treo chính nó

**Hiện tượng.** Máy boot xong nhưng không bao giờ tham gia cluster. Vào kiểm thì
thấy tiến trình khởi tạo vẫn đang "running" sau 20 phút.

**Nguyên nhân.** Script khởi tạo máy có một dòng ra lệnh khởi động dịch vụ ECS
ngay lập tức. Nhưng script đó *đang được chạy bởi* một dịch vụ hệ thống khác, và
lệnh khởi động kia lại chờ dịch vụ đó xong. Hai bên chờ nhau — **deadlock**.

**Bài học.** Trong script khởi tạo máy, chỉ nên **ghi cấu hình**, không nên ra
lệnh khởi động dịch vụ. Hệ thống sẽ tự khởi động đúng thứ tự sau đó. Đây là lỗi
kinh điển và rất khó chẩn đoán, vì hiện tượng là "không có gì xảy ra" chứ không
phải một thông báo lỗi.

## Issue #3 — Bật database và ứng dụng cùng lúc gây vòng lặp chết

**Hiện tượng.** Container API bị giết và tạo lại liên tục trong khoảng 8 phút,
log đầy lỗi kết nối. Sau đó tự nhiên bình thường.

**Nguyên nhân.** Health check của API **có mở kết nối tới database** (đúng thiết
kế — một API không nối được DB thì chưa thực sự sẵn sàng). ALB kết luận unhealthy
sau 45 giây và yêu cầu ECS thay container. Nhưng RDS SQL Server mất **5–10 phút**
để chuyển từ "starting" sang nhận được kết nối. Nên trong 5–10 phút đó, mọi
container API đều bị giết ngay khi vừa lên.

**Cách chữa sai.** Nâng thời gian ân hạn lên 10 phút. Làm vậy thì một API **thật
sự chết** cũng mất 10 phút mới bị phát hiện. Thời gian ân hạn là công cụ sai cho
vấn đề thứ tự.

**Cách chữa đúng.** Bắt buộc thứ tự: database phải nhận được kết nối **trước
khi** có chỗ cho container chạy. Trong quy trình bật, đây là một cửa chặn thật
sự, không phải khuyến nghị.

**Bài học.** Health check chạm database là quyết định đúng, nhưng nó tạo ra một
ràng buộc thứ tự khởi động. Ràng buộc đó phải được viết vào quy trình, vì nó
không hiển hiện ở bất cứ đâu trong cấu hình.

## Issue #4 — Tên của load balancer đổi mỗi lần dựng lại

**Hiện tượng.** Tắt hệ thống rồi bật lại, tên miền không hoạt động nữa dù mọi
thứ đều xanh.

**Nguyên nhân.** Tên DNS của ALB có một phần **ngẫu nhiên do AWS sinh lúc tạo**.
Vì ta xoá ALB mỗi lần tắt (để không tốn $18/tháng), mỗi lần bật lại là một tên
mới. Hai bản ghi DNS trỏ vào tên cũ đều chết.

**Cách chữa.** Thêm một **lớp trung gian** trong DNS: một bản ghi phụ trỏ vào
ALB, và hai bản ghi chính trỏ vào bản ghi phụ. Bật lại thì chỉ sửa **một** chỗ.

**Bài học.** Khi một thứ do nhà cung cấp sinh ra và có thể đổi, đừng để nhiều nơi
trỏ trực tiếp vào nó. Thêm một lớp gián tiếp mà bạn kiểm soát được.

## Issue #5 — Chứng chỉ TLS đứng mãi ở trạng thái chờ

**Hiện tượng.** Terraform treo 30 phút rồi báo lỗi timeout khi chờ chứng chỉ.

**Nguyên nhân.** ACM cấp chứng chỉ sau khi đọc được một bản ghi DNS xác thực.
Nhưng bản ghi đó đang bật chế độ proxy của Cloudflare — nghĩa là Cloudflare trả
về giá trị của *nó* thay vì giá trị thật. ACM không đọc được, nên chờ vô hạn.

**Cách chữa.** Bản ghi xác thực phải để chế độ **DNS thuần** (không proxy).

**Bài học.** Khi đặt một CDN/proxy trước hạ tầng, cần biết bản ghi nào được phép
proxy và bản ghi nào không. Bản ghi phục vụ *người dùng* thì proxy được; bản ghi
phục vụ *máy móc xác thực* thì không.

## Issue #6 — Máy không chịu chết khi tắt hệ thống

**Hiện tượng.** Hạ số máy về 0, nhưng máy nằm mãi ở trạng thái "đang chờ kết
thúc" và vẫn tính tiền.

**Nguyên nhân.** ECS tự tạo một *hook* trên Auto Scaling Group để có thời gian
chuyển việc khỏi máy trước khi máy chết. Thời gian chờ mặc định là **một tiếng**.
Hook này **không do Terraform quản lý** và thời gian chờ không phải tham số cấu
hình được, nên không sửa được bằng code.

**Cách chữa.** Xử lý ở tầng vận hành: khi thấy máy vào trạng thái chờ thì gửi
tín hiệu "xong rồi, cho chết đi". Việc này đã được tự động hoá trong script tắt.

**Bài học.** Không phải mọi thứ trong hạ tầng đều do IaC quản. Có những thứ dịch
vụ tự tạo ra, và phải biết chúng tồn tại. Bỏ qua cái này thì mỗi lần tắt vẫn tốn
thêm một tiếng tiền máy.

## Issue #7 — App tự cập nhật schema lúc khởi động

**Hiện tượng.** Không có hiện tượng — cho tới lúc có sự cố, và khi đó nó rất tệ.

**Nguyên nhân.** Ứng dụng ban đầu tự chạy cập nhật schema khi khởi động. Ba vấn
đề, theo thứ tự tăng dần độ nghiêm trọng:

1. **Không ai chặn được.** Cập nhật lỗi thì app crash, khởi động lại, lỗi lại —
   vòng lặp, và website chết trong suốt thời gian đó.
2. **Không biết nó thất bại.** Lỗi nằm lẫn trong log khởi động, không có ai báo.
3. **Tranh chấp.** Hai container API cùng lên sẽ cùng cố sửa schema một lúc.

**Cách chữa.** Bỏ hẳn khỏi app. Cập nhật schema đóng gói thành một chương trình
riêng, chạy như một task **một lần**, và **mã thoát của nó là điều kiện để deploy
tiếp tục**. Thất bại thì dừng — bản cũ vẫn đang phục vụ bình thường.

**Bài học.** Đây là mẫu thiết kế mà mọi hệ thống có database nên theo: **thay đổi
schema là một bước có cửa kiểm soát, không phải một hiệu ứng phụ của việc khởi
động ứng dụng.** Và chính sách phải là *chỉ tiến, không lùi*: không dùng
migration ngược; điểm quay về là bản snapshot chụp trước khi cập nhật.

## Issue #8 — Gọi load balancer nhận về 403 và tưởng là lỗi

**Hiện tượng.** Gọi ALB bằng tên DNS thô → HTTP 403. Trông như cấu hình sai.

**Nguyên nhân.** Đó là **đúng thiết kế**. ALB có danh sách tên miền cho phép, và
hành động mặc định cho tên miền lạ là trả 403. Gọi bằng tên thô của ALB thì tên
đó không nằm trong danh sách.

**Điểm cần phân biệt, và nó quan trọng:**
- Trả **403** = allowlist đang hoạt động → **đúng**
- Trả **503** = allowlist *chưa* có hiệu lực, request vẫn được chuyển tiếp và
  thất bại ở phía sau → **đây mới là lỗi**

**Bài học.** Khi thiết kế một rule từ chối, phải ghi lại rõ *phản hồi đúng khi
rule hoạt động là gì*. Không ghi thì lần sau chính nhóm mình sẽ nhìn thấy nó và
đi sửa một thứ không hỏng.

## Issue #9 — Dò cổng bị timeout và tưởng công cụ lỗi

**Hiện tượng.** Quét toàn bộ 65535 cổng của ALB, chạy 15 phút không ra kết quả
nào rồi bị cắt.

**Nguyên nhân.** Security Group **im lặng bỏ gói tin** thay vì trả lời "cổng
đóng". Nên với mỗi cổng đóng, công cụ quét phải chờ hết thời gian timeout rồi mới
kết luận. 65533 cổng × vài giây = không bao giờ xong.

**Cách nhìn lại.** Việc quét không xong **chính là bằng chứng**, không phải thất
bại: nếu firewall trả lời "cổng đóng" thì cùng lệnh đó xong trong vài giây. Việc
nó không xong chứng minh cấu hình đang che giấu hệ thống — kẻ tấn công không phân
biệt được "cổng đóng" với "máy không tồn tại".

**Bài học.** Trong kiểm thử bảo mật, phải hiểu *ý nghĩa* của kết quả, không chỉ
ghi lại kết quả. Và khi quét thì giới hạn phạm vi (danh sách cổng nguy hiểm +
1000 cổng phổ biến) thay vì quét tất cả.

## Issue #10 — Managed policy của AWS rộng hơn mình nghĩ

**Hiện tượng.** Không có hiện tượng. Phát hiện khi ngồi rà lại quyền.

**Nguyên nhân.** Role của máy EC2 dùng một managed policy sẵn của AWS để có thể
vào máy bằng SSM. Nhưng policy đó **kèm theo quyền đọc SSM Parameter**. Nghĩa là
ai vào được máy sẽ đọc được mật khẩu database — dù ta chưa bao giờ cấp quyền đó.

**Cách chữa.** Thêm một statement **Deny tường minh** cho quyền đọc bí mật trên
đường dẫn của dự án. Trong IAM, Deny luôn thắng Allow.

**Bài học.** Managed policy tiện nhưng là **hộp đen**. Phải đọc xem nó thật sự
cấp những gì. Và khi chỉ cần một phần, cách sạch là dùng managed policy rồi
**Deny lại phần không muốn** — vì Deny thắng, kết quả chắc chắn đúng bất kể AWS
sau này có thêm quyền gì vào policy đó.

## Issue #11 — Hiểu sai chi phí: tưởng cái gì miễn phí thì nó miễn phí

**Hiện tượng.** Suốt dự án, nhóm cẩn thận tắt NAT Gateway và ALB vì "chúng đắt",
nhưng để database chạy thoải mái vì "nó nằm trong free tier". Đến lúc soi hoá đơn
thì **database là khoản đắt nhất** — và riêng một dòng phí trong đó đã xấp xỉ
bằng cả NAT + ALB cộng lại.

**Nguyên nhân.** Hai sai lầm chồng lên nhau. Thứ nhất, `db.t3.micro` là loại máy
*burstable*: chỉ được dùng miễn phí 10% CPU, vượt lên thì **bị tính tiền phần
vượt**. SQL Server Express không tải vẫn ngồi ở ~36% CPU, nên nó vượt hạn mức
liên tục. Thứ hai, account này không hề có free tier — nó thuộc mô hình credit
trả trước, nên mọi thứ đều tính tiền, kể cả EC2.

**Bài học — hai cái, cả hai đều tổng quát:**

1. **Đừng suy diễn chi phí, hãy đo.** Bảng giá không cho biết bạn *thật sự* đang
   dùng cái gì. Phải bóc hoá đơn theo từng loại sử dụng. Trong trường hợp này,
   dòng phí lớn nhất không có tên nào trong sơ đồ kiến trúc.
2. **Đừng tin nhãn "miễn phí".** Nó luôn kèm điều kiện, và điều kiện thường là
   một hạn mức mà bạn không hề đo. Ở đây điều kiện là "10% CPU".

## Issue #12 — Bật rule demo rồi để quên

**Hiện tượng.** Bật lại hệ thống, mọi thứ báo healthy, nhưng browser timeout với
mọi URL. Trông y như hạ tầng bị lỗi nặng.

**Nguyên nhân.** Rule NACL chặn theo IP (bật lúc demo) vẫn còn bật, và nó đang
chặn đúng IP của máy mình ở tầng mạng. Hệ thống hoạt động hoàn hảo — chỉ riêng
mình không vào được.

**Cách chữa.** Script tắt tự đặt lại rule đó về false, và bảng trạng thái in cảnh
báo riêng nếu thấy nó đang bật.

**Bài học.** Bất cứ công tắc nào tạo ra hành vi "hệ thống trông như bị hỏng" đều
phải **tự động reset** khi kết thúc phiên làm việc, và phải **hiển thị rõ ràng**
khi đang bật. Không dựa vào việc con người nhớ.

---

# Phần IV — Vận hành hằng ngày

Sau khi hạ tầng đã xong, công việc thường ngày chỉ còn ba lệnh:

| Lệnh | Làm gì | Mất bao lâu |
|---|---|---|
| `up.sh` | Bật đủ để mở browser | 8–12 phút |
| `status.sh` | Xem đang chạy gì, bao lâu, tốn bao nhiêu | ngay |
| `down.sh` | Tắt sạch rồi tự kiểm chứng | 6–8 phút |

**Vì sao cần một bảng trạng thái riêng.** Vì bật/tắt mất nhiều phút, và như
Issue #1 đã chỉ ra, "apply xanh" không có nghĩa là dùng được. Bảng trạng thái in
**ý muốn** (giá trị trong cấu hình) cạnh **thực tế** (đọc trực tiếp từ AWS) — hai
cột lệch nhau là dấu hiệu có việc chạy dở. Thời gian lấy từ dấu thời gian của
AWS chứ không phải đồng hồ của script, nên tắt máy rồi mở lại vẫn đúng.

**Vì sao script tắt tự kiểm chứng lại bằng AWS.** Vì tin Terraform là chưa đủ:
nếu apply thất bại giữa đường, state có thể nói "đã xoá" trong khi resource vẫn
sống và vẫn tính tiền. Script gọi thẳng AWS đếm số load balancer, NAT gateway,
địa chỉ IP, máy chủ — và chỉ báo thành công khi tất cả bằng 0.

**Rủi ro lớn nhất còn lại, và nó không phải đơn giá.** AWS **tự khởi động lại**
một database đang dừng sau 7 ngày. Ở giá $0.098/giờ, một tuần không ai để ý là
$16.5 bay âm thầm. Đơn giá thì có chặn trên; cái này thì không. Đó là lý do việc
còn lại trong kế hoạch — một tác vụ tự tắt hằng đêm — là **bắt buộc**, không phải
tuỳ chọn.

---

# Phần V — Nếu làm lại từ đầu

Năm điều sẽ làm khác, xếp theo giá trị:

1. **Viết cách tắt trước khi viết cách bật.** Ta làm gần đúng (NAT có công tắc từ
   ngày đầu) nhưng bộ script tắt/bật hoàn chỉnh thì tới cuối mới có. Trong lúc
   đó, mọi lần tắt là một chuỗi lệnh tay, và mỗi lần là một cơ hội để quên một
   bước.

2. **Đo chi phí ngay tuần đầu, đừng đợi thấy hoá đơn.** Bóc hoá đơn theo loại sử
   dụng ngay sau phiên làm việc đầu tiên. Nếu làm vậy, ta đã biết database là
   khoản đắt nhất từ tuần một chứ không phải tuần cuối — và có thể đã chọn hệ
   quản trị nhẹ hơn ngay từ đầu, thay vì phải đổi về sau.

3. **Coi "apply xanh" là chưa xong.** Mỗi giai đoạn nên có một bước kiểm tra sức
   khoẻ thật sự ngay từ khi viết, không phải thêm vào sau khi bị lừa hai lần.

4. **Ghi lại phản hồi đúng của mỗi rule từ chối.** Issue #8 (403 đúng, 503 sai)
   là loại kiến thức bị mất ngay khi người viết rule quên nó.

5. **Test đường ghi sớm hơn.** Tiêu chí upload ảnh là tiêu chí duy nhất chứng
   minh IAM role thay được access key, mà nó lại được kiểm ở việc gần cuối cùng.
   Nếu nó thất bại, ta sẽ phát hiện quá muộn.
