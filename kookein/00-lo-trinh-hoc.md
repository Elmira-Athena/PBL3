# Lộ trình học thiết kế hệ thống HushStore trên AWS

> Tài liệu gốc: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> (1465 dòng). Bộ file trong `kookein/` này **chia nhỏ toàn bộ tài liệu đó**
> (Phần I → VI) ra thành các buổi học 45–90 phút, theo đúng thứ tự bạn nên
> đọc khi mới bắt đầu học AWS. Không thay thế tài liệu gốc — mỗi buổi đều trỏ
> lại đúng mục trong tài liệu gốc để bạn đọc bản đầy đủ sau khi đã nắm ý chính.

## Vì sao chia thế này

Tài liệu gốc viết cho người đọc liền một mạch, có sẵn nền tảng. Nếu bạn mới
học AWS, đọc một lần từ đầu đến cuối sẽ bị ngợp — đặc biệt là Phần III (thiết
kế hệ thống thật), dài gần 900 dòng. Bộ file này bẻ nhỏ, mỗi buổi:

1. Trả lời đúng **một** câu hỏi thiết kế.
2. Có ví dụ đời thường trước khi vào thuật ngữ AWS.
3. Có bài tập đọc code thật trong repo (không cần AWS account để làm hầu hết
   bài tập — chỉ cần đọc file `.tf`).
4. Có câu hỏi tự kiểm tra để biết đã hiểu chưa, kèm gợi ý đáp án.

## Danh sách buổi học

| Buổi | File | Chủ đề | Tương ứng tài liệu gốc | Thời lượng |
|---|---|---|---|---|
| 0 | (file này) | Lộ trình tổng | — | 5 phút |
| 1 | [01-kien-thuc-mang-nen.md](01-kien-thuc-mang-nen.md) | IP, CIDR, subnet, route table, port, stateful/stateless, NAT | Phần I, mục 1–6 | 45 phút |
| 2 | [02-tls-https-dns.md](02-tls-https-dns.md) | DNS, TLS/HTTPS, chứng chỉ, TLS termination | Phần I, mục 7–8 | 45 phút |
| 3 | [03-aws-va-terraform-co-ban.md](03-aws-va-terraform-co-ban.md) | Region/AZ, danh sách dịch vụ AWS dùng trong hệ thống, Terraform | Phần II | 45 phút |
| 4 | [04-duong-di-request.md](04-duong-di-request.md) | Toàn cảnh: request đi qua bao nhiêu lớp, Cloudflare | Phần III, "Đường đi của một request" | 30 phút |
| 5 | [05-vpc-va-subnet.md](05-vpc-va-subnet.md) | VPC, 6 subnet, 3 tier, vì sao 2 AZ, route table | Phần III, "Mạng — VPC và 6 subnet" | 60 phút |
| 6 | [06-security-group.md](06-security-group.md) | Security Group — firewall stateful | Phần III, "Security Group" | 45 phút |
| 7 | [07-nacl-phan-1-khai-niem.md](07-nacl-phan-1-khai-niem.md) | Network ACL — khái niệm, `nacl-public`, `nacl-db` | Phần III, "Network ACL" (nửa đầu) | 45 phút |
| 8 | [08-nacl-phan-2-bai-toan-ephemeral.md](08-nacl-phan-2-bai-toan-ephemeral.md) | NACL — bài toán rule 90/95/115, dải ephemeral | Phần III, "`nacl-app`" | 60 phút |
| 9 | [09-application-load-balancer.md](09-application-load-balancer.md) | ALB, listener, target group, allowlist Host | Phần III, "Application Load Balancer" | 45 phút |
| 10 | [10-ec2-ecs-container.md](10-ec2-ecs-container.md) | EC2 + ECS, vì sao container, bridge mode | Phần III, "EC2 và ECS" | 60 phút |
| 11 | [11-linux-phan-1-boot-va-tien-trinh.md](11-linux-phan-1-boot-va-tien-trinh.md) | Linux: 3 bản phân phối, boot sequence, deadlock đã gặp thật | Phần III, "Linux" (nửa đầu) | 60 phút |
| 12 | [12-linux-phan-2-tai-nguyen-va-quyen.md](12-linux-phan-2-tai-nguyen-va-quyen.md) | Linux: swap, namespace/cgroup, quyền file, SSM thay SSH | Phần III, "Linux" (nửa sau) | 60 phút |
| 13 | [13-rds-database.md](13-rds-database.md) | RDS PostgreSQL, Multi-AZ, read replica, VerifyFull | Phần III, "RDS" | 60 phút |
| 14 | [14-iam-va-bi-mat.md](14-iam-va-bi-mat.md) | IAM 4 role, Parameter Store, least privilege | Phần III, "IAM" + "Bí mật" | 45 phút |
| 15 | [15-tong-ket-doi-chieu-de-bai.md](15-tong-ket-doi-chieu-de-bai.md) | Đối chiếu đề bài, giới hạn đã biết, câu hỏi bảo vệ đồ án, lộ trình workshop AWS Study Group | Phần IV, V, VI | 60 phút |

Tổng: khoảng **13 giờ học**, chia làm 3 tuần (4–5 buổi/tuần) là hợp lý. Buổi 15
gợi ý cụ thể workshop nào ghép với buổi nào — đọc buổi đó trước khi lên
[cloudjourney.awsstudygroup.com](https://cloudjourney.awsstudygroup.com/vi/).

## Cách học mỗi buổi

1. Đọc phần "Ví dụ đời thường" trước — đừng nhảy thẳng vào thuật ngữ.
2. Đọc phần giải thích, đối chiếu với đoạn tương ứng trong tài liệu gốc (link
   có ở đầu mỗi buổi).
3. Làm bài tập đọc code — mở file `.tf` được chỉ ra và tự tìm dòng liên quan.
4. Tự trả lời câu hỏi kiểm tra ở cuối buổi **trước khi xem gợi ý đáp án**.
5. Nếu buổi nào có workshop AWS Study Group tương ứng (xem buổi 15), làm
   workshop đó *sau* khi đọc xong buổi học — thử trên console dễ nhớ hơn nếu
   đã biết trước vì sao.

## Trước khi bắt đầu buổi 1

Bạn không cần AWS account, không cần cài Terraform, không cần chạy được dự án
để học 15 buổi này — toàn bộ bài tập là **đọc** file `.tf` có sẵn trong repo.
Chỉ cần:

- Một trình soạn thảo mở được thư mục `f:\PBL3`.
- Đọc được tiếng Việt kỹ thuật (tài liệu gốc và bộ file này đều tiếng Việt).

Nếu muốn thực hành trên AWS thật (khuyến khích ở buổi 15), đọc
[`docs/terraform-runbook.md`](../docs/terraform-runbook.md) trước — đó là
runbook vận hành thật của dự án, không phải bài học.
