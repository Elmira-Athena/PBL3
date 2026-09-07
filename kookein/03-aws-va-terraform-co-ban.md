# Buổi 3 — Region/AZ, các dịch vụ AWS dùng trong hệ thống, Terraform

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần II (dòng 271–354).
> Mục tiêu buổi này: thuộc "bảng chú giải" các dịch vụ AWS sẽ gặp lại xuyên
> suốt các buổi sau, và hiểu vì sao dự án dùng Terraform thay vì bấm console.

## 1. Region và Availability Zone

**Region** là một vùng địa lý có trung tâm dữ liệu của AWS. HushStore dùng
`ap-southeast-1` (Singapore) — gần Việt Nam nhất, độ trễ thấp.

**Availability Zone (AZ)** là một trung tâm dữ liệu riêng biệt *trong* một
region — nguồn điện, mạng, làm mát độc lập với AZ khác. Một region có nhiều
AZ. Đặt hệ thống ở 2 AZ nghĩa là một trung tâm dữ liệu cháy/mất điện thì cái
kia vẫn sống.

HushStore dùng `ap-southeast-1a` và `ap-southeast-1b`. Lý do bắt buộc, không
phải tuỳ chọn: **Application Load Balancer đòi tối thiểu 2 subnet ở 2 AZ
khác nhau** — AWS không cho tạo ALB nếu chỉ có một AZ.

## 2. Bảng dịch vụ — thuộc trước khi vào Phần III

Bảng này sẽ được nhắc lại liên tục ở các buổi sau. Đọc lướt một lần cho có
khái niệm, không cần nhớ hết ngay — mỗi dòng sẽ được giải thích sâu ở đúng
buổi tương ứng.

| Dịch vụ | Là gì | Vai trò trong HushStore | Học sâu ở buổi |
|---|---|---|---|
| **VPC** | Mạng riêng ảo | Toàn bộ hệ thống nằm trong đây | 5 |
| **Subnet** | Dải IP con, gắn 1 AZ | 6 cái, chia 3 tier × 2 AZ | 5 |
| **Internet Gateway (IGW)** | Cửa ra/vào Internet của VPC | Gắn vào VPC, phục vụ public subnet | 5 |
| **NAT Gateway** | Cho private subnet đi ra Internet | App tier tải image, gọi API AWS | 5 |
| **Security Group (SG)** | Firewall stateful, gắn từng resource | 3 cái: alb, web, rds | 6 |
| **Network ACL (NACL)** | Firewall stateless, gắn từng subnet | 3 cái, mỗi tier một cái | 7, 8 |
| **Application Load Balancer (ALB)** | Bộ chia tải hiểu HTTP | Cửa vào duy nhất, bóc TLS, định tuyến theo tên miền | 9 |
| **Target Group** | Nhóm đích ALB gửi traffic tới | 2 cái: web (:80), api (:8080) | 9 |
| **EC2** | Máy chủ ảo | 1 máy `t3.micro` chạy container | 10 |
| **AMI** | Ảnh đĩa để tạo máy | Amazon Linux đã cài sẵn ECS agent | 10 |
| **Launch Template** | Khuôn mẫu tạo EC2 | Định nghĩa máy sẽ được tạo thế nào | 10 |
| **Auto Scaling Group (ASG)** | Quản lý số lượng EC2 | 0 = tắt, 1 = bật | 10 |
| **ECS** | Bộ điều phối container | Quyết định container chạy ở đâu | 10 |
| **Task Definition** | Mô tả một container chạy thế nào | 4 cái: web, api, migrator, seeder | 10 |
| **ECS Service** | Giữ container luôn chạy đủ số | 2 cái: web, api | 10 |
| **ECR** | Kho chứa container image | 4 repo | 10 |
| **RDS** | Database do AWS quản hộ | PostgreSQL 17, Multi-AZ | 13 |
| **IAM Role** | Danh tính có quyền, không mật khẩu dài hạn | 4 role, mỗi role một phạm vi | 14 |
| **SSM Parameter Store** | Kho lưu bí mật, mã hoá | Mật khẩu DB, connection string, JWT | 14 |
| **SSM Session Manager** | Vào máy chủ không cần SSH | Đường quản trị duy nhất | 12 |
| **CloudWatch Logs** | Nơi tập trung log | Log của mọi container | 15 |
| **VPC Flow Logs** | Ghi lại kết nối bị chặn/cho phép | Bằng chứng tầng mạng cho báo cáo | 15 |
| **ACM** | Cấp chứng chỉ TLS miễn phí, tự gia hạn | Chứng chỉ cho ALB | 2, 9 |
| **S3** | Lưu file | Ảnh sản phẩm, log, Terraform state | — |

## 3. Thứ tự lắp ráp — nối các mảnh lại

```
Internet → ALB → Target Group → EC2 ← (ASG dựng ra, theo Launch Template, từ một AMI)
                                 └─ trên EC2: ECS chạy container theo Task Definition,
                                    ECS Service lo giữ đúng số bản đang chạy
                                                   ↓
                                                  RDS
```

Đọc sơ đồ này theo chiều: người dùng gõ tên miền → ALB nhận request → ALB
chuyển vào Target Group → Target Group chỉ tới EC2 đang chạy container → API
trong container gọi RDS. Vòng "ASG dựng EC2 từ Launch Template/AMI" và "ECS
quản container trên EC2" là chuyện xảy ra *trước*, để có máy sẵn sàng nhận
traffic.

## 4. Terraform — vì sao dùng nó thay vì bấm console

Hai cách dựng hạ tầng:

**Bấm chuột trên console.** Nhanh lúc đầu, nhưng: không ai biết bạn đã bấm
gì, không dựng lại được y hệt, không review được, và sau ba tháng không ai
còn nhớ Security Group đó mở port kia để làm gì.

**Infrastructure as Code (IaC).** Viết hạ tầng thành file văn bản, lưu Git.
Terraform đọc file đó và gọi API AWS để tạo đúng những gì được mô tả.

Cái được:
- **Xem lại được** — hạ tầng nằm trong Git, có history, có review (giống
  review code).
- **Dựng lại được** — xoá sạch rồi dựng lại y nguyên.
- **Xem trước được** — `terraform plan` cho biết sẽ thay đổi gì *trước khi*
  thay đổi thật.
- **Tự tài liệu hoá** — file cấu hình chính là tài liệu, không lệch với thực
  tế (khác với một trang wiki mà không ai cập nhật).

Ba khái niệm cần biết:

**State.** Terraform ghi lại "tôi đã tạo những gì" vào một file state. Nhờ
state, nó biết cái gì cần tạo mới, cái gì cần sửa, cái gì cần xoá khi bạn đổi
file cấu hình. State của HushStore nằm trên S3 (không nằm trên máy cá nhân
của ai) để cả nhóm dùng chung, và có **lock** — hai người `apply` cùng lúc
thì người thứ hai bị chặn, tránh hai bên ghi đè lẫn nhau làm hỏng hạ tầng.

**Module.** Nhóm các resource liên quan thành một khối tái dùng được.
HushStore có 8 module: `network`, `security`, `data`, `ecs`, `alb`, `storage`,
`cicd`, `costguard`.

**Provider.** Thư viện biết cách "nói chuyện" với một nền tảng cụ thể (ở đây
là AWS). HushStore dùng AWS provider bản 6.60.

## Bài tập đọc code

Mở [`infra/tf/modules/`](../infra/tf/modules/) trong file explorer và xác
nhận đúng 8 module kể trên tồn tại. Với mỗi module, đoán trước (không cần
đúng 100%) nó chứa dịch vụ AWS nào trong bảng ở mục 2, rồi mở một file `.tf`
bất kỳ trong đó để kiểm tra đoán của bạn — ví dụ mở
[`infra/tf/modules/ecs/`](../infra/tf/modules/ecs/) xem có đúng là chứa Task
Definition, ECS Service không.

## Câu hỏi tự kiểm tra

1. Vì sao ALB bắt buộc phải trải trên 2 AZ, không được chỉ 1?
2. Sự khác nhau giữa "bấm console" và "IaC" nằm ở đâu khi cần *xem lại* một
   quyết định hạ tầng đã làm 3 tháng trước?
3. Terraform state dùng để làm gì? Vì sao phải đặt trên S3 chung, không để
   trên máy cá nhân?
4. ECS Service và Task Definition khác nhau ở điểm nào? (Gợi ý: một cái là
   "bản thiết kế", một cái là "người giữ cho đúng số lượng luôn chạy".)

<details>
<summary>Gợi ý đáp án</summary>

1. Đây là yêu cầu kỹ thuật của AWS đối với ALB — không phải lựa chọn của
   HushStore. Lý do sâu hơn là tính sẵn sàng: ALB cần chạy được dù một AZ có
   sự cố.
2. Với IaC, quyết định đó nằm trong một file Git có commit message và có
   thể `git log`/`git blame` để biết ai, khi nào, vì sao. Với console, quyết
   định đó chỉ còn trong đầu người đã bấm (nếu người đó còn nhớ).
3. State ghi lại resource nào Terraform đã tạo, để lần sau biết cần sửa gì
   thay vì tạo lại từ đầu. Đặt trên S3 chung để tránh hai người có hai bản
   state khác nhau trên máy riêng, dẫn tới xung đột hoặc tạo trùng resource.
4. Task Definition là bản mô tả tĩnh (image nào, bao nhiêu RAM, port nào).
   ECS Service là tiến trình giám sát: giữ đúng số container đang chạy theo
   đúng Task Definition đó, tự dựng lại nếu container chết.

</details>

Buổi tiếp theo: [04-duong-di-request.md](04-duong-di-request.md).
