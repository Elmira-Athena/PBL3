# Tắt khẩn cấp — dừng mọi khoản tính phí theo giờ

Dùng file này khi bạn cần dừng chi phí AWS mà **không có ai hướng dẫn**: phiên làm
việc bị ngắt giữa lúc triển khai, bạn không rõ hạ tầng đang ở trạng thái nào, hoặc
bạn chỉ muốn tắt đi và tính sau.

Đọc mục **"Cách 1"** là đủ. Mọi thứ khác là giải thích.

---

## Chỉ có 3 khoản tính phí theo giờ

Toàn bộ phần còn lại của hệ thống miễn phí hoặc nằm trong free tier. Chỉ ba thứ
này đốt tiền khi bật:

| Khoản | Giá | Nếu để 1 tháng |
|---|---|---|
| **NAT Gateway** | $0.045/giờ + $0.045/GB | ~$33 |
| **Application Load Balancer** | $0.0225/giờ | ~$16 |
| **Elastic IP chưa gắn vào đâu** | ~$0.005/giờ | ~$3.6 |

EC2 `t3.micro`, EBS 30GB, RDS `db.t3.micro` + 20GB storage đều trong free tier.
ECS cluster, Auto Scaling Group, Security Group, NACL, VPC, subnet, ECR (dưới
500MB), S3 (gần như rỗng), SSM Parameter Store, CloudWatch log: **$0**.

Elastic IP dễ bị bỏ sót: nó chỉ tính phí **khi KHÔNG gắn vào gì**. Xoá NAT Gateway
mà quên release EIP thì nó bắt đầu tính tiền, nên bước đó nằm trong quy trình dưới.

---

## Cách 1 — Tắt bằng Terraform (nên dùng nếu chạy được)

Cách này giữ Terraform state khớp với thực tế, nên lần sau bật lại chỉ là đổi
ngược lại.

```bash
cd ~/Athena/Code/PBL3/infra/tf/envs/prod
aws sso login --profile hushstore          # nếu token hết hạn

# Hạ toàn bộ toggle
sed -i '' 's|^enable_alb = .*|enable_alb = false|'     terraform.tfvars
sed -i '' 's|^instance_count = .*|instance_count = 0|' terraform.tfvars
sed -i '' 's|^enable_nat = .*|enable_nat = false|'     terraform.tfvars

terraform apply -auto-approve

# RDS không có toggle, stop riêng (storage vẫn free tier)
aws rds stop-db-instance --db-instance-identifier hushstore-db-tf --profile hushstore
```

Nếu `terraform apply` báo lỗi hoặc treo, **đừng cố sửa** — nhảy sang Cách 2.

---

## Cách 2 — Tắt bằng AWS CLI (khi Terraform không chạy được)

Cách này tạo drift với Terraform state. **Không sao cả** — drift sửa được sau, còn
$49/tháng thì không lấy lại được. Ưu tiên dừng tiền.

Chạy nguyên khối này, nó tự tìm tài nguyên chứ không cần bạn biết ID:

```bash
a() { command aws --profile hushstore --no-cli-pager --region ap-southeast-1 "$@"; }

# ── 1. ECS service về 0 TRƯỚC (nếu không ECS sẽ liên tục dựng lại task) ──
for s in hushstore-web hushstore-api; do
  a ecs update-service --cluster hushstore --service "$s" --desired-count 0 2>/dev/null \
    && echo "service $s -> 0"
done

# ── 2. ASG về 0 (instance bị terminate, EBS xoá theo) ──
a autoscaling update-auto-scaling-group --auto-scaling-group-name hushstore-asg \
  --min-size 0 --desired-capacity 0 2>/dev/null && echo "ASG -> 0"

# ── 3. Xoá ALB ($0.0225/giờ) ──
for arn in $(a elbv2 describe-load-balancers --query 'LoadBalancers[].LoadBalancerArn' --output text); do
  a elbv2 delete-load-balancer --load-balancer-arn "$arn" && echo "da xoa ALB"
done

# ── 3b. ĐỢI instance terminate THẬT rồi mới xoá NAT ──
# Gắn ASG vào ECS capacity provider khiến ECS tạo lifecycle hook
# `ecs-managed-draining-termination-hook` (timeout 3600s) — dù
# managed_termination_protection đã DISABLED, vì đây là tính năng managed
# draining khác. Hạ desired về 0 KHÔNG terminate ngay: instance vào trạng thái
# `Terminating:Wait` và chờ ECS drain xong.
# Nếu xoá NAT trước khi instance drain, ECS agent mất đường ra control plane và
# ECS không bao giờ drain -> instance treo ở Terminating:Wait tới 1 GIỜ.
echo "doi instance terminate (co the mat 1-2 phut)..."
for i in $(seq 1 12); do
  ST=$(a ec2 describe-instances --filters "Name=tag:Name,Values=hushstore-container-instance" \
       --query 'Reservations[].Instances[?State.Name!=`terminated`].State.Name' --output text)
  [ -z "$ST" ] && echo "instance da terminate" && break
  echo "  con: $ST"; sleep 15
done

# Neu treo o Terminating:Wait, giai phong hook thu cong:
for iid in $(a autoscaling describe-auto-scaling-groups \
      --auto-scaling-group-names hushstore-asg \
      --query 'AutoScalingGroups[0].Instances[?LifecycleState==`Terminating:Wait`].InstanceId' \
      --output text); do
  echo "giai phong lifecycle hook cho $iid"
  a autoscaling complete-lifecycle-action \
    --auto-scaling-group-name hushstore-asg \
    --lifecycle-hook-name ecs-managed-draining-termination-hook \
    --lifecycle-action-result CONTINUE --instance-id "$iid"
done

# ── 4. Xoá NAT Gateway ($0.045/giờ) ──
for nat in $(a ec2 describe-nat-gateways \
      --filter "Name=state,Values=available,pending" \
      --query 'NatGateways[].NatGatewayId' --output text); do
  a ec2 delete-nat-gateway --nat-gateway-id "$nat" && echo "da xoa NAT $nat"
done

# ── 5. Release EIP — BẮT BUỘC, EIP không gắn vào đâu vẫn tính phí ──
echo "doi NAT xoa xong roi moi release EIP (mat ~1-2 phut)..."
sleep 120
for alloc in $(a ec2 describe-addresses \
      --query 'Addresses[?AssociationId==null].AllocationId' --output text); do
  a ec2 release-address --allocation-id "$alloc" && echo "da release EIP $alloc"
done

# ── 6. Stop RDS (storage vẫn free tier) ──
a rds stop-db-instance --db-instance-identifier hushstore-db-tf >/dev/null 2>&1 \
  && echo "RDS -> stopping"
```

> Nếu bước 5 báo `AuthFailure` hoặc EIP vẫn còn gắn, đợi thêm 2 phút rồi chạy lại
> riêng bước đó. NAT Gateway mất một lúc mới nhả EIP.
>
> **Đã gặp thật:** lần đầu tôi xoá NAT chỉ 40 giây sau khi instance launch, nên ECS
> agent chưa kịp đăng ký vào cluster. ECS không biết instance tồn tại nên không
> drain, và nó treo ở `Terminating:Wait`. Đó là lý do bước 3b tồn tại và phải đứng
> TRƯỚC bước 4. Instance treo không tốn tiền (t3.micro + EBS 30GB đều free tier)
> nhưng gây hoang mang khi kiểm tra.

---

## Cách 3 — Bằng Console (khi không có CLI)

Đăng nhập qua **AWS access portal** (`https://d-9667aefba2.awsapps.com/start`),
region **Singapore / ap-southeast-1**, rồi làm đúng thứ tự:

1. **ECS** → Clusters → `hushstore` → Services → mỗi service → Update → Desired
   tasks = `0` → Update
2. **EC2** → Auto Scaling groups → `hushstore-asg` → Edit → Desired/Min = `0`
3. **EC2** → Load balancers → chọn ALB → Actions → **Delete**
4. **VPC** → NAT gateways → chọn → Actions → **Delete NAT gateway**
5. **EC2** → Elastic IPs → cái nào cột *Associated instance* trống → Actions →
   **Release Elastic IP addresses**
6. **RDS** → Databases → `hushstore-db-tf` → Actions → **Stop temporarily**

---

## Kiểm tra đã tắt sạch

```bash
a() { command aws --profile hushstore --no-cli-pager --region ap-southeast-1 "$@"; }
echo "NAT Gateway (phai 0) : $(a ec2 describe-nat-gateways --filter 'Name=state,Values=available,pending' --query 'length(NatGateways)' --output text)"
echo "ALB         (phai 0) : $(a elbv2 describe-load-balancers --query 'length(LoadBalancers)' --output text)"
echo "EIP         (phai 0) : $(a ec2 describe-addresses --query 'length(Addresses)' --output text)"
echo "EC2 running (phai 0) : $(a ec2 describe-instances --filters 'Name=instance-state-name,Values=running' --query 'length(Reservations[].Instances[])' --output text)"
echo "RDS status           : $(a rds describe-db-instances --query 'DBInstances[].DBInstanceStatus' --output text)"
```

Bốn số đầu **phải là 0**. RDS nên là `stopped` (hoặc `stopping`).

Chi phí thực tế tháng này:

```bash
aws ce get-cost-and-usage \
  --time-period Start=$(date -u +%Y-%m-01),End=$(date -u +%Y-%m-%d) \
  --granularity MONTHLY --metrics UnblendedCost \
  --query 'ResultsByTime[0].Total.UnblendedCost.Amount' --output text \
  --profile hushstore --no-cli-pager
```

---

## Xoá sạch hoàn toàn (không chỉ tắt)

Chỉ làm khi bạn thật sự muốn bỏ hết:

```bash
cd ~/Athena/Code/PBL3/infra/tf/envs/prod
terraform destroy
```

Hai thứ sẽ chặn `destroy` một cách **có chủ ý**:

- **Bucket ảnh sản phẩm** — `force_destroy = false`, nên `destroy` fail nếu còn
  object. Đó là lưới an toàn. Muốn xoá thật thì xoá object trước:
  `aws s3 rm s3://hushstore-public-assets-551897327153 --recursive --profile hushstore`
- **State bucket** trong `infra/tf/bootstrap` — có `prevent_destroy = true`. Phải
  bỏ dòng đó rồi apply mới xoá được.

---

## Cảnh báo chi phí đã bật sẵn

AWS Budgets tên `hushstore-monthly-spend`, ngưỡng $20/tháng, gửi email tới địa chỉ
đã cấu hình. Bốn mốc: đã tiêu thật 25% ($5), 50% ($10), 100% ($20), và **dự báo**
vượt 100%.

Mốc **dự báo** là mốc hữu ích nhất với đúng tình huống này: nếu để NAT + ALB chạy
24/7, AWS sẽ dự báo vượt $20 chỉ sau khoảng **10 tiếng** và gửi mail ngay lúc đó,
chứ không đợi tới cuối tháng.

Nếu chưa bấm xác nhận đăng ký trong mail AWS gửi thì cảnh báo **sẽ không tới** —
kiểm tra cả hộp spam.
