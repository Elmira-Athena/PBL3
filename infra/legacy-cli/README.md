# Legacy — hạ tầng dựng bằng AWS CLI (đã ngừng dùng)

Các script trong thư mục này dựng hạ tầng HushStore bằng AWS CLI imperative,
được dùng từ 2026-05 tới 2026-08 trên AWS account `408194747451`. Chúng đã bị
thay thế bởi stack Terraform ở `infra/tf/`.

Toàn bộ tài nguyên do các script này tạo ra **đã bị xoá** sau báo cáo kỳ trước,
và account đó cũng đã hết free tier. Kỳ này dự án chạy trên một AWS account
khác, truy cập bằng IAM Identity Center (SSO).

**Giữ lại làm gì:** đây là spec tham chiếu cho stack Terraform mới — mọi resource
trong `setup.sh` đều có bản Terraform tương ứng. Xem
`docs/superpowers/specs/2026-08-17-aws-terraform-ecs-infra-design.md` để đối
chiếu.

| File | Vai trò cũ | Thay thế bởi |
|------|-----------|--------------|
| `setup.sh` | Tạo VPC, subnet, SG, RDS, EC2, budget alert | `infra/tf/modules/{network,security,data}` |
| `teardown.sh` | Xoá toàn bộ resource | `terraform destroy` |
| `start.sh` / `stop.sh` | Bật/tắt EC2 + RDS tiết kiệm chi phí | `infra/tf/scripts/{up,down}.sh` (Phase 3) |
| `config.example.json` | Config đầu vào | `infra/tf/envs/prod/terraform.tfvars` |
| `hushstore.conf` | nginx trên host: SSL + serve WASM + proxy API | `src/Client/nginx.conf` (nằm trong image) + ALB |
| `deploy.sh` | Deploy tay trên EC2 | `.github/workflows/deploy.yml` (Phase 2) |

## Đừng chạy lại

Ba lý do:

1. Chúng tạo resource nằm **ngoài Terraform state** → gây drift, lần
   `terraform apply` sau sẽ xử lý sai.
2. Chúng tạo **SSH key pair** và mở **port 22** ra internet. Thiết kế mới cố
   tình không có key pair nào và không có Security Group rule nào mở 22 — admin
   access đi qua SSM Session Manager và ECS Exec.
3. Chúng hardcode account `408194747451` và profile AWS CLI dùng access key
   tĩnh, cả hai đều không còn đúng.

## Vài điểm khác biệt so với thiết kế mới

| | Cũ (thư mục này) | Mới (`infra/tf/`) |
|---|---|---|
| Cách dựng | Bash + AWS CLI imperative, state trong `resources.env` | Terraform, state trên S3 có versioning |
| Network ACL | Không có (dùng default allow-all) | 3 NACL theo tier, rule tối thiểu |
| Load balancer | Không có, nginx nghe thẳng public IP của EC2 | ALB + ACM, 2 target group route theo Host |
| Vị trí EC2 | Public subnet, có public IP | Private subnet, không public IP |
| Truy cập admin | SSH port 22 + file `.pem` | SSM Session Manager + ECS Exec, không port 22 |
| Chạy ứng dụng | `docker compose` trên host + nginx cài trên host | ECS EC2 launch type, 2 container từ ECR |
| Migration DB | `MigrateAsync()` lúc app khởi động | One-off ECS task chạy EF Core migration bundle, là gate của pipeline |
| Credential của app | Static IAM access key trong `.env` | ECS task role, credential tạm thời |
