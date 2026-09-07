# Buổi 15 — Đối chiếu đề bài, giới hạn đã biết, và đi tiếp sau khoá học này

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần IV, V, VI và Từ điển thuật ngữ (dòng 1209–1466) — phần cuối cùng của
> tài liệu.
> Mười bốn buổi trước đi qua từng thành phần một. Buổi này lùi lại nhìn
> toàn cảnh: hệ thống có đáp ứng đúng đề bài không, nó thiếu gì mà nhóm biết
> rõ và chấp nhận, và nếu muốn học sâu hơn AWS thì đi đường nào tiếp.

## Phần IV — đối chiếu với đề bài, dòng theo dòng

Đề bài đòi ba mảng kiến thức (Linux, AWS, Terraform), 5 thành phần hạ tầng
và 2 kết quả. Sau 14 buổi, bảng đối chiếu này giờ đọc được đầy đủ — mỗi
dòng bên trái đã có một buổi học tương ứng bên phải:

| Đề bài yêu cầu | Ở đâu trong hệ thống | Buổi liên quan |
|---|---|---|
| Tìm hiểu HĐH Linux + xây website trên đó | 3 bản phân phối, cloud-init/systemd, swap, namespace/cgroup, quyền file, không SSH | 11, 12 |
| Tìm hiểu AWS Cloud + Terraform | 8 module, 105 test tự động, không resource nào bấm tay | 3 |
| VPC | `10.20.0.0/16`, 6 subnet, 3 tier, 2 AZ | 5 |
| Security Group | 3 cái, rule tham chiếu SG, không có port 22 | 6 |
| **Network ACL** | 3 cái, mỗi tier một cái, có rule DENY và thứ tự có ý nghĩa | 7, 8 |
| **Application Load Balancer** | ALB + ACM + 2 target group + allowlist Host | 9 |
| EC2 Instance chạy website | 1 `t3.micro` chạy container nginx + .NET | 10 |
| Máy tấn công verify rule | Laptop (`my_ip`), 12 kịch bản, bằng chứng lưu trong `docs/evidence/` | — |
| Rule mở theo nguyên tắc tối thiểu | Xem bảng dưới | — |

**Nguyên tắc tối thiểu — chứng minh cụ thể, không phải khẩu hiệu.** Đây là
bảng quan trọng nhất để trả lời câu hỏi bảo vệ "least-privilege nghĩa là gì
trong hệ thống của bạn, cho ví dụ cụ thể":

| Điều đã KHÔNG mở | Hệ quả |
|---|---|
| Không port 22 ở bất kỳ SG nào | Không có bề mặt SSH; máy quét tự động dò 22 đều bị chặn |
| App tier không có IP public | Không thể gọi trực tiếp từ Internet |
| Db tier không có route ra Internet | Không tồn tại đường đi tới database |
| `sg-rds` egress rỗng | Chiếm được DB cũng không rút được dữ liệu ra |
| `sg-web` ingress chỉ 2 port, chỉ từ `sg-alb` | Không ai ngoài ALB gọi được container |
| ALB default action = 403 | Tên miền lạ không lọt vào hệ thống |
| Role máy EC2 bị Deny tường minh đọc bí mật | Vào được máy cũng không đọc được mật khẩu DB |
| Role app chỉ có quyền S3 | Chiếm được container không chạm được RDS qua API |
| Hai tập bí mật giao nhau bằng rỗng | Một task bị chiếm không làm lộ bí mật của task khác |

Nhìn kỹ bảng này sẽ thấy một mẫu lặp lại xuyên suốt 14 buổi: **mỗi lớp
phòng thủ không chặn được "kẻ tấn công vào", nó chặn "kẻ tấn công đã vào
rồi làm được gì tiếp"** — đó chính là ý nghĩa của "defense in depth" nhắc ở
buổi 4.

## Phần V — giới hạn đã biết

Đây là phần hay bị hỏi nhất khi bảo vệ đồ án, và cũng là phần dễ mất điểm
nhất nếu trả lời kiểu "không có giới hạn nào" — người hỏi sẽ không tin.
Trung thực về cái hệ thống **không** làm được, kèm lý do chấp nhận, mạnh
hơn nhiều so với im lặng:

| Giới hạn | Vì sao chấp nhận |
|---|---|
| `nacl-app` buộc mở dải 1024–65535 ra Internet | Bản chất của NACL stateless (buổi 8). Đã bù bằng DENY 5432/8080 số nhỏ hơn, và bằng SG ở lớp trong |
| NAT Gateway không gắn được Security Group | Khác NAT instance. Kiểm soát egress dồn vào `sg-web` và `nacl-app` |
| Không lọc egress theo tên miền | Cần AWS Network Firewall (~$300/tháng), không khả thi cho một đồ án |
| Chỉ 1 EC2, deploy có downtime 20–40s | Đổi lấy bề mặt SG nhỏ hơn hàng nghìn lần (buổi 10). Lựa chọn có ý thức |
| Rate limiter đếm trong RAM | 2 instance sẽ thành 2× hạn mức. Đây là lý do `max_instance_count` vẫn ghim 1 |
| NAT thứ hai chỉ cứu egress, không cứu đường phục vụ | ALB → EC2 → RDS đi hoàn toàn trong VPC (buổi 5). Mất AZ chứa NAT thì instance còn lại vẫn phục vụ, chỉ mất deploy/ECR/SSM |
| Read replica dựng được nhưng chưa ai dùng | 0 tham chiếu trong code (buổi 13). Bật lên là thêm tiền mà primary không giảm tải |
| Đọc từ replica có hai chỗ tuyệt đối không được dùng | "Ghi rồi đọc lại để map DTO" và mọi thứ liên quan `IsActive`/quyền — replication bất đồng bộ khiến cả hai nguy hiểm theo cách khác nhau |
| Không scale ngang được dù đã có ASG + ALB | Chốt cứng nhất: host port tĩnh 80/8080 (buổi 10) — hai task không cùng bind một port trên một máy |
| Default security group của VPC | AWS tạo sẵn, không xoá được. ✅ Đã bịt bằng rule rỗng (apply 2026-09-06) |
| Không có CloudTrail trail riêng | Chỉ Event History mặc định — 90 ngày, không lưu S3 (buổi 12) |
| Không có alarm nào | Lambda cost guard lỗi thì im lặng. Đã cân nhắc, hoãn |
| Không WAF | ALB có allowlist Host (buổi 9) nhưng không lọc SQL injection ở tầng mạng — phòng thủ nằm ở tầng ứng dụng |
| Chưa đo chi phí thật sau khi đổi engine | Giá đang dùng là niêm yết + số đo cũ trên SQL Server giữ làm cận trên (buổi 14) |

Điểm chung của cả bảng: mỗi giới hạn đều có một **lý do cụ thể** đi kèm,
không phải "chưa kịp làm". Khi trình bày, nói được cả hai nửa — giới hạn
*và* lý do chấp nhận nó — mới là điều thể hiện hiểu hệ thống, không chỉ
biết liệt kê.

## Phần VI — học tiếp ở đâu sau 15 buổi này

Tài liệu gốc gợi ý dùng **AWS Study Group** (`cloudjourney.awsstudygroup.com/vi`)
— bộ workshop tiếng Việt, làm trực tiếp trên console AWS thật. Cách dùng
hiệu quả nhất theo gợi ý của tài liệu: **làm workshop trước** để có cảm
giác bấm tay trên resource thật, **rồi quay lại đọc buổi tương ứng** trong
15 buổi này để hiểu vì sao HushStore cấu hình khác.

### Lộ trình 3 tuần đề nghị

**Tuần 1 — nền tảng** (làm đúng thứ tự, mỗi cái là điều kiện của cái sau):
IAM cơ bản → VPC (ưu tiên chương "Tường lửa trong VPC") → EC2 → RDS. Sau đó
đọc lại buổi 5, 6, 7, 8 — lúc này bảng rule NACL sẽ có nghĩa hơn nhiều so
với đọc lần đầu.

**Tuần 2 — container và load balancer:** Docker/ECR → Auto Scaling Group
(Launch Template + ALB) → Amazon ECS (workshop quan trọng nhất, sát kiến
trúc HushStore nhất). Sau đó đọc lại buổi 9 và 10 để hiểu vì sao chọn
`bridge` + port cố định thay vì port động, và cái giá phải trả.

**Tuần 3 — vận hành và bảo mật:** IAM Role cho ứng dụng → Session Manager →
CloudWatch → Trực quan hoá chi phí. Sau đó đọc lại buổi 14 — đủ nền để hiểu
vì sao bốn role tách rời là điểm least-privilege mạnh nhất của thiết kế.

Nếu chỉ có thời gian cho ba workshop: **VPC → ECS → IAM Role cho ứng
dụng**. Ba cái này phủ phần lớn những gì hay bị hỏi khi bảo vệ đồ án.

### Ba khoảng trống mà AWS Study Group không dạy

Tài liệu gốc chỉ ra ba thành phần đề bài nhấn mạnh mà bộ workshop đó **không
phủ tới** — nghĩa là 15 buổi này (đặc biệt buổi 7, 8, 9) chính là nguồn
chính cho ba chỗ này, không phải phần bổ trợ:

1. **Terraform** — workshop IaC duy nhất trên site dạy CloudFormation, không
   phải Terraform. Tài liệu gốc liệt kê 6 nguồn thay thế (ưu tiên tiếng
   Việt: chuỗi bài "Terraform Series" trên Viblo, đặc biệt Bài 5 và Bài 6
   về module — gần cấu trúc `infra/tf/modules/` của HushStore nhất), cộng
   với `terraform-runbook.md` của chính dự án.
2. **Network ACL** — chỉ là một chương phụ trong workshop VPC, mà đây lại
   là thành phần đề bài nhấn mạnh nhất. Buổi 7 và 8 của khoá học này đã đi
   sâu hơn workshop, kể cả bài toán ephemeral-port mà không nguồn tiếng
   Việt nào tìm được có đề cập.
3. **Application Load Balancer** — nằm lẫn trong workshop ASG và ECS,
   không có workshop riêng. Buổi 9 đã bù bốn thứ không nguồn nào dạy:
   allowlist Host, `drop_invalid_header_fields`, redirect 301+ACM, và
   `deregistration_delay` phối hợp với `stopTimeout`/`ShutdownTimeout`.

### Ba chỗ workshop dạy khác — không phải HushStore làm sai

Khi đối chiếu, sẽ thấy vài điểm HushStore làm khác hẳn workshop chuẩn. Đừng
đọc nhầm thành lỗi — mỗi khác biệt có lý do đã nói ở các buổi trước:

| Workshop dạy | HushStore làm | Vì sao (buổi liên quan) |
|---|---|---|
| Key pair + SSH vào EC2 | Không key pair, vào bằng SSM | Buổi 12 — SSH là bề mặt tấn công lớn nhất |
| ECS Fargate, `awsvpc` | ECS EC2 launch type, `bridge` | Buổi 10 — đề bài yêu cầu EC2 Instance thật |
| Secrets Manager | Parameter Store SecureString | Buổi 14 — miễn phí, không cần tự luân chuyển mật khẩu |
| Multi-AZ RDS (nếu engine hỗ trợ) | Cũng bật Multi-AZ | Buổi 13 — PostgreSQL vẫn `stop` được khi Multi-AZ, SQL Server thì không |
| Một NAT Gateway cho cả VPC | Hai — mỗi AZ một cái | Buổi 5 — một route table chỉ chứa một dòng `0.0.0.0/0` |

## Từ điển thuật ngữ nhanh

Bảng tra cứu nhanh cho các thuật ngữ đã gặp xuyên suốt 15 buổi:

| Thuật ngữ | Nghĩa ngắn |
|---|---|
| AZ | Trung tâm dữ liệu độc lập trong một region |
| CIDR | Cách viết một dải IP, ví dụ `10.20.0.0/16` |
| Egress / Ingress | Chiều ra / chiều vào |
| Ephemeral port | Cổng tạm 1024–65535 mà bên gọi mở để nhận trả lời |
| Health check | ALB gọi thử một URL để biết target còn sống |
| IaC | Hạ tầng viết thành code, lưu trong Git |
| Idempotent | Chạy nhiều lần cho cùng một kết quả |
| IGW | Cửa Internet của VPC |
| Least privilege | Chỉ cấp đúng quyền tối thiểu cần thiết |
| NAT | Cho máy private đi ra Internet mà không mở đường vào |
| Stateful | Firewall ghi nhớ kết nối, tự cho chiều trả lời |
| Stateless | Không ghi nhớ, phải viết rule cả hai chiều |
| State (Terraform) | File ghi lại Terraform đã tạo những gì |
| Target group | Nhóm đích mà ALB gửi traffic tới |
| TLS termination | Chỗ mã hoá HTTPS được bóc ra |

## Bài tập tổng kết

Không phải bài đọc code lần này — hãy thử tự trả lời không nhìn tài liệu:

1. Vẽ lại (trên giấy hoặc bằng lời) toàn bộ đường đi một request từ trình
   duyệt tới database, gọi tên mọi lớp phòng thủ nó đi qua.
2. Chọn 3 dòng trong bảng "nguyên tắc tối thiểu" ở Phần IV, giải thích mỗi
   dòng bằng ví dụ cụ thể một kẻ tấn công sẽ bị chặn ở đâu.
3. Chọn 2 giới hạn trong bảng Phần V, giải thích vì sao nhóm chấp nhận nó
   thay vì sửa ngay.

## Câu hỏi tự kiểm tra cuối cùng

1. Vì sao "least-privilege" trong hệ thống này không chỉ là một khẩu hiệu —
   nêu một ví dụ cụ thể chứng minh nó có tác dụng thật khi một lớp bị chọc
   thủng?
2. Nếu người phản biện hỏi "hệ thống có scale ngang được không", câu trả
   lời đúng là gì, và vì sao?
3. Vì sao đọc một giới hạn trong Phần V mà không đọc kèm lý do chấp nhận nó
   là chưa đủ để trả lời tốt khi bảo vệ?

<details>
<summary>Gợi ý đáp án</summary>

1. Ví dụ: nếu kẻ tấn công chiếm được quyền thực thi mã trong container API
   (giả sử qua một lỗ hổng ứng dụng), role gắn với nó lúc đó (`task-app-role`)
   chỉ có quyền S3 — không đọc được bí mật DB, không gọi được RDS qua API
   AWS. Một lớp (ứng dụng) bị chọc thủng nhưng thiệt hại không lan sang lớp
   khác (dữ liệu), vì quyền đã được tách từ trước, không phải chặn được nhờ
   phát hiện tấn công.
2. Không — bị chặn cứng bởi host port tĩnh (80/8080 trên `bridge` mode):
   hai task không cùng bind được một port trên một máy, và `max_size = 1`
   + `managed_scaling = DISABLED` cũng ghim cứng ở một instance. Đây là
   một đánh đổi có chủ ý để giữ SG tối thiểu (2 port cố định) thay vì mở
   dải port động — đúng nguyên tắc tối thiểu mà đề bài đang chấm.
3. Vì bản thân giới hạn chỉ là một sự thật (hệ thống chưa làm được X); lý
   do chấp nhận mới là phần thể hiện **hiểu vấn đề đủ sâu để cân đối được
   chi phí/lợi ích**, thay vì chỉ liệt kê thiếu sót. Người phản biện thường
   hỏi tiếp "vậy tại sao không sửa" — nếu không có lý do sẵn, câu trả lời sẽ
   là "chưa kịp làm", nghe như thiếu sót ngoài ý muốn hơn là một quyết định.

</details>

---

Đến đây là hết 15 buổi. Lộ trình đầy đủ nằm ở
[00-lo-trinh-hoc.md](00-lo-trinh-hoc.md) nếu cần quay lại buổi nào để ôn.
