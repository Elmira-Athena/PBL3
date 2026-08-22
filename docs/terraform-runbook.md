# Terraform Runbook — HushStore

Hạ tầng AWS của HushStore, dựng bằng Terraform. Tài liệu này là thứ cần đọc
trước khi chạm vào hệ thống, và mọi con số trong đây đều đo được từ lần chạy
thật chứ không phải ước lượng.

**Nguyên tắc chi phí:** NAT Gateway và ALB tính theo giờ và không có bậc free
tier. Mặc định cả hai đều **tắt**. Bật lên khi làm việc, tắt ngay khi xong.

## Yêu cầu

```bash
terraform -version          # >= 1.10
aws --version               # >= 2.x
aws sso login --profile hushstore    # mỗi ngày một lần, session 8 giờ
export ACCT=$(aws sts get-caller-identity --query Account --output text --profile hushstore)
```

Cần cài thêm nếu muốn dùng ECS Exec / Session Manager tương tác:

```bash
brew install --cask session-manager-plugin
```

Không có nó thì `aws ecs execute-command` và `aws ssm start-session` báo
`SessionManagerPlugin is not found`. `aws ssm send-command` vẫn chạy được vì nó
không cần plugin.

## Toggle chi phí

| Biến | Ý nghĩa | Chi phí khi bật |
|---|---|---|
| `enable_nat` | NAT Gateway cho egress (pull ECR, SSM, yum) | **$0.045/giờ** + $0.045/GB |
| `enable_alb` | Serving stack: ALB + 2 target group + 2 listener + 2 listener rule + 2 ECS service + waiter ACM | **$0.0225/giờ** |
| `instance_count` | 0 hoặc 1 EC2 container instance | **$0.0132/giờ** |
| `enable_flow_logs` | VPC Flow Logs (chỉ REJECT) | phí ingest CloudWatch |
| `enable_deny_demo` | NACL rule 50 DENY `my_ip` | $0 |

RDS bật/tắt bằng AWS CLI, không phải bằng Terraform — `aws rds stop-db-instance`
giữ nguyên instance và chỉ ngừng tính giờ compute. Storage 20GB vẫn tính
(~$2.76/tháng, chạy 24/7 kể cả khi stopped).

**RDS là khoản đắt nhất, không phải NAT hay ALB** — $0.098/giờ, trong đó
$0.0674 là CPU credit surplus. Xem mục "Chi phí" ở cuối.

Chi phí đo được từ hai cửa sổ thật, **giá niêm yết** (thực trả $0 khi credit
còn bù — xem mục "Chi phí" ở cuối):

| Việc | Thời gian | Bật những gì | Tốn |
|---|---|---|---|
| Task 15 — verify toàn bộ website | 25 phút | NAT + ALB + EC2 + RDS | ~$0.081 |
| Task 16 — chạy seed | 19 phút | NAT + EC2 + RDS (không ALB) | ~$0.054 |

Đơn giá tổng khi bật đủ là **$0.1954/giờ**, không phải $0.0675 như tưởng lúc
đầu. Hai nguyên nhân: RDS không miễn phí (CPU credit surplus là khoản lớn nhất
của cả account), và đơn giá cũ lấy theo us-east-1. Xem mục "Chi phí" ở cuối.

## Bật / tắt bằng script

Đây là đường dùng hằng ngày. Bốn script nằm ở `infra/tf/scripts/`, đều nhận
`--help`:

```bash
bash infra/tf/scripts/up.sh          # bật đủ để mở browser (~8-12 phút)
bash infra/tf/scripts/status.sh      # đang chạy gì, bao lâu rồi, tốn bao nhiêu
bash infra/tf/scripts/status.sh -w   # theo dõi liên tục, làm mới 15 giây
bash infra/tf/scripts/down.sh        # tắt sạch rồi tự kiểm chứng (~6-8 phút)
bash infra/tf/scripts/nuke.sh        # terraform destroy — hỏi xác nhận
```

Thêm `up.sh --no-alb` khi chỉ cần chạy migrate/seed: bỏ ALB thì website không
mở được nhưng tiết kiệm $0.0225/giờ và bớt ~3 phút chờ.

`status.sh` là thứ trả lời câu "bật rồi chưa". Nó in **ý muốn** (giá trị trong
`terraform.tfvars`) cạnh **thực tế** (đọc trực tiếp từ AWS API), nên hai cột lệch
nhau là dấu hiệu có apply chạy dở. Thời gian lấy từ timestamp của AWS
(`CreatedTime`, `LaunchTime`, `registeredAt`, RDS event stream) chứ không phải từ
đồng hồ của script — tắt máy rồi mở lại vẫn ra số đúng. Exit code dùng được
trong script khác: `0` đang bật, `10` đang tắt, `20` đang chuyển trạng thái.

| Bước lâu nhất | Thường mất |
|---|---|
| RDS `stopped` → `available` | **5-10 phút** — bước quyết định tổng thời gian |
| Tạo NAT Gateway | ~2 phút |
| Tạo ALB + target group + service | ~3 phút |
| Instance đăng ký vào ECS cluster | ~2 phút |
| Target group chuyển `healthy` | 1-3 phút sau khi task lên |
| Service drain khi tắt | ~2 phút 30 |

Ba điều các script làm mà chạy tay dễ quên:

**`up.sh` phát lệnh start RDS rồi apply NAT + ALB trong lúc RDS đang `starting`**,
thay vì chờ tuần tự như phần dưới viết. An toàn vì `instance_count = 0` nghĩa là
cluster không có capacity, task không xếp lên đâu được, nên không có gì
crash-loop. Cửa chặn thật — RDS `available` trước khi bật instance — vẫn nguyên.
Tiết kiệm ~4 phút mỗi lần bật.

**`down.sh` chạy một watcher nền giải phóng `ecs-managed-draining-termination-hook`**
ngay khi thấy instance vào `Terminating:Wait`, nên không phải chờ heartbeat và
apply không bị treo. Xem mục dưới để hiểu vì sao hook này luôn xuất hiện.

**`down.sh` reset luôn `enable_flow_logs` và `enable_deny_demo` về false.** Cái
đầu để quên thì tốn phí ingest CloudWatch. Cái sau tốn $0 nhưng tệ hơn: lần bật
sau nó chặn đúng IP của mình ở tầng NACL, và triệu chứng là browser timeout —
trông y như hạ tầng lỗi.

Log của mọi lần apply/destroy nằm ở `infra/tf/.local/logs/` (gitignored).

Phần dưới là các lệnh tay tương ứng, giữ lại vì chúng giải thích **vì sao** thứ
tự phải như vậy. Script chỉ là bản tự động của đúng những bước này.

## Bật hệ thống — THỨ TỰ LÀ RÀNG BUỘC, KHÔNG PHẢI KHUYẾN NGHỊ

```bash
cd infra/tf/envs/prod

# 1. RDS TRƯỚC TIÊN, và phải chờ tới `available`
aws rds start-db-instance --db-instance-identifier hushstore-db-tf \
  --profile hushstore --no-cli-pager
until [ "$(aws rds describe-db-instances --db-instance-identifier hushstore-db-tf \
        --query 'DBInstances[0].DBInstanceStatus' --output text --profile hushstore)" = "available" ]; do
  sleep 20
done

# 2. NAT + ALB, instance vẫn để 0
sed -i '' -e 's|^enable_nat = .*|enable_nat = true|' \
          -e 's|^enable_alb = .*|enable_alb = true|' terraform.tfvars
terraform apply

# 3. Instance — SAU khi NAT đã có đường ra
sed -i '' 's|^instance_count = .*|instance_count = 1|' terraform.tfvars
terraform apply

# 4. GATE: chờ instance thật sự đăng ký vào cluster
bash ../../scripts/wait-for-capacity.sh
```

**Vì sao RDS phải xong trước.** Health check của target group `tg-api` gọi
`/health/ready`, mà endpoint đó có `AddDbContextCheck` nên nó **mở kết nối tới
RDS**. Target group đặt `interval = 15`, `unhealthy_threshold = 3`, tức ALB kết
luận unhealthy sau 45 giây. `health_check_grace_period_seconds = 120` bảo vệ được
app khởi động chậm, nhưng RDS SQL Server Express mất **5–10 phút** để từ
`starting` sang nhận kết nối. Bật service khi RDS chưa lên thì task API bị giết
và replace liên tục — đo được ~8 phút crash-loop trong lần chạy thật. Không phải
lỗi cấu hình: readiness probe đang làm đúng việc của nó. Nhưng nó gây nhiễu và
có thể che mất một lỗi thật.

Không sửa bằng cách nâng grace period lên 600s: làm vậy thì một API **thật sự
chết** cũng mất 10 phút mới bị phát hiện. Grace period là sai công cụ cho vấn đề
thứ tự.

Chi tiết nhỏ nhưng hữu ích: `tg-api` chuyển healthy **trước** khi RDS báo
`available` — RDS nhận kết nối sớm hơn lúc status đổi.

**Vì sao phải chạy `wait-for-capacity.sh`.** `terraform apply` xanh **không** có
nghĩa là cluster dùng được. ASG dùng `health_check_type = "EC2"`, nên nó chỉ hỏi
"EC2 có running không", không hỏi "ECS agent có đăng ký không". Một instance boot
xong mà agent chết vẫn làm apply xanh **và** ASG báo Healthy. Đó là chế độ lỗi đã
xảy ra thật hai lần (deadlock `systemctl` trong `user_data`). Script này là thứ
duy nhất phân biệt "hạ tầng đã dựng" với "hạ tầng đã dựng và dùng được", và nó
kiểm cả `agentConnected` chứ không chỉ sự tồn tại của instance.

## Tắt hệ thống — THỨ TỰ CŨNG LÀ RÀNG BUỘC

```bash
cd infra/tf/envs/prod

# 1. Serving stack: service phải chết TRƯỚC ALB/target group
sed -i '' 's|^enable_alb = .*|enable_alb = false|' terraform.tfvars
terraform apply          # ~3 phút, service mất ~2m30s để drain

# 2. Instance về 0
sed -i '' 's|^instance_count = .*|instance_count = 0|' terraform.tfvars
terraform apply

# 3. CHỜ instance biến mất thật, và giải phóng lifecycle hook nếu bị treo
#    (xem mục dưới — việc này gần như LUÔN cần)

# 4. NAT + EIP
sed -i '' 's|^enable_nat = .*|enable_nat = false|' terraform.tfvars
terraform apply

# 5. RDS
aws rds stop-db-instance --db-instance-identifier hushstore-db-tf \
  --profile hushstore --no-cli-pager

# 6. Xác nhận độc lập — đừng chỉ tin terraform
for q in \
  "elbv2 describe-load-balancers|LoadBalancers" \
  "ec2 describe-nat-gateways|NatGateways" \
  "ec2 describe-addresses|Addresses" ; do
  echo "${q%%|*}: $(aws ${q%%|*} --query "length(${q##*|})" --output text \
    --profile hushstore --region ap-southeast-1 --no-cli-pager)"
done
terraform plan    # phải ra "No changes"
```

### Instance mắc `Terminating:Wait` — bình thường, không phải lỗi

Khi ASG về 0, instance gần như luôn dừng ở `Terminating:Wait`. Nguyên nhân: ECS
tự tạo lifecycle hook `ecs-managed-draining-termination-hook` (heartbeat 3600s)
trên ASG **ngay khi ASG được đăng ký làm capacity provider**, kể cả khi
`managed_termination_protection = DISABLED` — đó là hai tính năng khác nhau. Hook
này **không** do Terraform quản, và heartbeat **không** phải argument của
`aws_ecs_capacity_provider`, nên không sửa được bằng config. Cách duy nhất là xử
lý ở tầng vận hành:

```bash
IID=$(aws autoscaling describe-auto-scaling-groups \
  --auto-scaling-group-names hushstore-asg \
  --query 'AutoScalingGroups[0].Instances[0].InstanceId' \
  --output text --profile hushstore --no-cli-pager)

aws autoscaling complete-lifecycle-action \
  --lifecycle-hook-name ecs-managed-draining-termination-hook \
  --auto-scaling-group-name hushstore-asg \
  --instance-id "$IID" --lifecycle-action-result CONTINUE \
  --profile hushstore --no-cli-pager
```

Bỏ qua bước này thì instance vẫn tính giờ trong tối đa một tiếng, và bước destroy
NAT sau đó có thể vướng.

## DNS — và một hệ quả phải biết trước

**Tên DNS của ALB đổi mỗi lần ALB được tạo lại.** Nó có dạng
`hushstore-alb-<số ngẫu nhiên>.ap-southeast-1.elb.amazonaws.com`, và phần ngẫu
nhiên do AWS sinh lúc tạo. Vì `down.sh` destroy ALB để khỏi tốn $16/tháng, mỗi
lần bật lại là một tên mới.

Hệ quả: nếu trỏ thẳng `hushstore.io.vn` và `api.hushstore.io.vn` vào tên đó thì
**cả hai record chết sau mỗi lần tắt**, và phải sửa cả hai mỗi lần bật.

Cách giảm việc: thêm một lớp trung gian trong zone.

| Record | Type | Target | Proxy |
|---|---|---|---|
| `alb` | CNAME | `<ALB DNS hiện tại>` | **DNS only** (mây xám) |
| `@` | CNAME | `alb.hushstore.io.vn` | Proxied (mây vàng) |
| `api` | CNAME | `alb.hushstore.io.vn` | Proxied (mây vàng) |

Bật lại thì chỉ sửa **một** record `alb`, hai record kia không phải chạm. Lấy giá
trị mới bằng:

```bash
terraform output -raw alb_dns_name
```

SSL/TLS ở Cloudflare đặt **Full (strict)** — hợp lệ vì ALB có ACM cert đúng cho cả
hai hostname.

Giữa hai cửa sổ làm việc, domain **cố tình không hoạt động**. Đó là đánh đổi có ý
thức: ALB chạy 24/7 tốn $16.40/tháng cho một đồ án.

### Hai record validation của ACM — đừng xoá

```
_94b2f051288c970bd147266dff8a7045.hushstore.io.vn
_3816c7054c253353a2cf040f820af482.api.hushstore.io.vn
```

Cả hai phải để **DNS only**. Proxy mây vàng thì ACM không đọc được và cert đứng
mãi ở `PENDING_VALIDATION`.

ACM dùng lại chúng để **tự động gia hạn** cert. Xoá đi thì cert hết hạn và HTTPS
chết âm thầm sau 13 tháng. Lưu ý: cert chỉ chuyển
`RenewalEligibility: ELIGIBLE` khi **đang được một service AWS sử dụng** — trước
đó nó là `INELIGIBLE`, nên đừng lấy trạng thái đó làm dấu hiệu có gì sai.

## Deploy phiên bản mới

Từ Phase 2, deploy là **push vào `main`**. Không còn bước tay nào trong đường
bình thường.

```
push main
  │
  ├── build ×4 (song song)   api · web · migrator · seeder, tag = git SHA
  ├── migration-script       migrate-<sha>.sql -> S3 artifacts
  └── preflight              đọc RDS + container instance + service
        │
        ├── hạ tầng TẮT  -> dừng ở đây, job VẪN XANH, summary nói rõ
        │
        └── hạ tầng BẬT  -> snapshot -> migrate -> GATE exit code
                              │
                              ├── exit ≠ 0 -> in log CloudWatch, fail,
                              │               KHÔNG deploy, bản cũ vẫn phục vụ
                              │
                              └── exit = 0 -> register 2 revision
                                              -> update-service ×2
                                              -> wait services-stable
                                              -> (rollback nếu fail)
```

Ba điều phải biết trước khi trông chờ vào nó:

**Pipeline KHÔNG tự bật hạ tầng.** Mỗi giờ bật tốn $0.1954, nên một pipeline tự
bật là chi phí không có trần. Push khi stack đang tắt vẫn **xanh** và vẫn push
đủ 4 image lên ECR — nhưng nó **chưa deploy**, và summary của job nói thẳng điều
đó kèm câu lệnh cần chạy. Ràng buộc này nằm ở IAM (role không có
`autoscaling:SetDesiredCapacity`, không có `rds:StartDBInstance`), không chỉ nằm
ở comment trong workflow.

**`image_tag` trong `terraform.tfvars` đổi nghĩa.** Nó không còn là "image đang
chạy" — nó là **image dùng khi dựng lại từ đầu**. Revision đang chạy do CI đăng
ký, và `aws_ecs_service` có `ignore_changes = [task_definition]` để `terraform
apply` không kéo service về revision của Terraform (nếu thiếu dòng đó, mỗi lần
apply là một lần **rollback ngầm**). `up.sh` cảnh báo khi ECR có tag mới hơn
tfvars.

**Migration là gate, không phải một bước trong danh sách.** Exit code khác 0 thì
job dừng, log của task in thẳng vào output của Actions, và service **không** được
cập nhật — bản cũ tiếp tục phục vụ.

### Việc tay một lần: cấu hình GitHub

```bash
terraform -chdir=infra/tf/envs/prod output github_deploy_role_arn
terraform -chdir=infra/tf/envs/prod output github_plan_role_arn
```

Đặt hai giá trị đó thành **repository variable** (Settings → Secrets and
variables → Actions → Variables), tên `AWS_DEPLOY_ROLE_ARN` và
`AWS_PLAN_ROLE_ARN`. Chúng là *variable* chứ không phải *secret* vì ARN của role
không phải bí mật: không có OIDC token do GitHub ký cho đúng repo và đúng nhánh
thì biết ARN cũng vô dụng.

Rồi **xoá** ba secret của pipeline cũ: `EC2_SSH_KEY`, `EC2_HOST`, và GHCR token
nếu còn. Sau bước này trong toàn hệ thống không còn credential dài hạn nào.

### Deploy tay (đường dự phòng)

Vẫn giữ vì có lúc cần: CI đang lỗi, hoặc muốn deploy một commit không nằm trên
`main`.


```bash
cd "$(git rev-parse --show-toplevel)"
SHA=$(git rev-parse HEAD)
REG="${ACCT}.dkr.ecr.ap-southeast-1.amazonaws.com"

aws ecr get-login-password --region ap-southeast-1 --profile hushstore \
  | docker login --username AWS --password-stdin "$REG"

for img in api web migrator; do
  case $img in
    api)      F=Dockerfile ;;
    web)      F=src/Client/Dockerfile ;;
    migrator) F=src/Infrastructure/Dockerfile.migrator ;;
  esac
  docker build --platform=linux/amd64 -f "$F" -t "${REG}/hushstore-${img}:${SHA}" .
  docker push "${REG}/hushstore-${img}:${SHA}"
done

# Migration TRƯỚC, và chỉ deploy khi exit code = 0
TASK=$(aws ecs run-task --cluster hushstore --task-definition hushstore-migrator \
  --capacity-provider-strategy capacityProvider=hushstore-cp,weight=1 \
  --query 'tasks[0].taskArn' --output text --profile hushstore --no-cli-pager)
aws ecs wait tasks-stopped --cluster hushstore --tasks "$TASK" --profile hushstore
aws ecs describe-tasks --cluster hushstore --tasks "$TASK" \
  --query 'tasks[0].containers[0].exitCode' --output text --profile hushstore
# exit code khác 0 -> DỪNG. App cũ vẫn đang phục vụ.

# Chỉ khi migration pass:
sed -i '' "s|^image_tag = .*|image_tag = \"$SHA\"|" infra/tf/envs/prod/terraform.tfvars
cd infra/tf/envs/prod && terraform apply
```

ECR bật **IMMUTABLE tag**, nên không bao giờ ghi đè được một SHA đã push. Đó là
điều kiện để rollback có nghĩa.

Waiter của AWS CLI có thể chạy lâu hơn giới hạn timeout của terminal/tool đang
dùng. Nếu bị cắt giữa `aws ecs wait`, task **vẫn chạy bình thường** phía server —
chỉ cần gọi lại `describe-tasks` để xem kết quả.

## Rollback

```bash
# Task definition giữ skip_destroy = true nên các revision cũ vẫn ACTIVE
aws ecs list-task-definitions --family-prefix hushstore-api --sort DESC \
  --profile hushstore --no-cli-pager

# Trỏ service về revision trước
aws ecs update-service --cluster hushstore --service hushstore-api \
  --task-definition hushstore-api:<revision-1> --profile hushstore --no-cli-pager
```

Pipeline **tự rollback** khi `wait services-stable` fail: nó ghi lại revision
đang chạy trước khi update, rồi trỏ về đó. Nên hai lệnh trên chỉ cần khi muốn
rollback một bản đã deploy THÀNH CÔNG (bug lộ ra muộn hơn).

Rollback code KHÔNG rollback migration. DB là **forward-only**: không dùng
down-migration. Điểm quay về cho dữ liệu là snapshot `pre-migrate-<sha8>-<run>`
mà pipeline tạo trước mỗi lần migrate; nó giữ 3 cái mới nhất và tự xoá cái cũ
hơn (quyền xoá bị ghim theo ARN pattern `snapshot:pre-migrate-*` nên pipeline
không chạm được snapshot người tạo tay).

Hệ quả bắt buộc của forward-only: migration phải viết theo hướng **tương thích
ngược** — thêm column nullable trước, backfill, siết constraint ở lần sau — để
bản app cũ không chết trong khoảng thời gian schema mới đã lên mà code cũ vẫn
đang chạy. Khôi phục snapshot là quyết định của người, không phải của pipeline.

## Vào hệ thống để chẩn đoán

**Không có SSH.** Không có key pair nào, và không có Security Group rule nào mở
port 22 ở bất kỳ đâu.

```bash
IID=$(aws ec2 describe-instances \
  --filters Name=tag:Project,Values=hushstore Name=instance-state-name,Values=running \
  --query 'Reservations[0].Instances[0].InstanceId' --output text \
  --profile hushstore --no-cli-pager)

# Vào host (cần session-manager-plugin)
aws ssm start-session --target "$IID" --profile hushstore

# Hoặc chạy lệnh không cần plugin
aws ssm send-command --instance-ids "$IID" --document-name AWS-RunShellScript \
  --parameters 'commands=["cloud-init status","systemctl is-active ecs","free -m"]' \
  --profile hushstore --no-cli-pager

# Vào trong container API
TASK=$(aws ecs list-tasks --cluster hushstore --service-name hushstore-api \
  --query 'taskArns[0]' --output text --profile hushstore --no-cli-pager)
aws ecs execute-command --cluster hushstore --task "$TASK" --container api \
  --interactive --command /bin/sh --profile hushstore

# Log
aws logs tail /ecs/hushstore-api    --since 15m --follow --profile hushstore
aws logs tail /ecs/hushstore-seeder --since 15m --profile hushstore
```

## Seed lại dữ liệu

Seed chạy bằng **một one-off ECS task**, không phải trên host. Mật khẩu đi theo
đường `secrets` của ECS nên nó không bao giờ chạm host và không nằm trong shell
history.

```bash
TASK=$(aws ecs run-task --cluster hushstore --task-definition hushstore-seeder \
  --capacity-provider-strategy capacityProvider=hushstore-cp,weight=1 \
  --query 'tasks[0].taskArn' --output text --profile hushstore --no-cli-pager)
aws ecs wait tasks-stopped --cluster hushstore --tasks "$TASK" --profile hushstore
aws ecs describe-tasks --cluster hushstore --tasks "$TASK" \
  --query 'tasks[0].containers[0].exitCode' --output text --profile hushstore
aws logs tail /ecs/hushstore-seeder --since 10m --profile hushstore
```

Ba điều về seeder:

**Nó KHÔNG chạy được trên host, có chủ ý.** Role của container instance bị Deny
tường minh `ssm:GetParameter*` trên `/hushstore/*`. Nếu thấy `AccessDenied` khi
thử đọc secret từ host thì đó là thiết kế đang hoạt động, không phải lỗi.

**Nó dùng execution role riêng.** `hushstore-task-execution-seeder-role` đọc đúng
một parameter là `db-password`, và **không** đọc được `connection-string` hay
`jwt-secret`. Ngược lại `hushstore-task-execution-role` (api/web/migrator) đọc hai
cái kia và **không** đọc được `db-password`. Hai tập giao nhau bằng rỗng.

**Chạy lại được, nhưng không hoàn toàn vô hại.** `seed_data.sql` idempotent
(`IF NOT EXISTS`). `seed_product_data.sql` thì **xoá rồi tạo lại** phần dữ liệu
thuộc phạm vi nó quản (có `WHERE` giới hạn, không xoá trắng bảng). Trên DB đã có
order tham chiếu tới product thì `DELETE` sẽ vướng khoá ngoại.

## Sự cố thường gặp

| Hiện tượng | Nguyên nhân | Xử lý |
|---|---|---|
| `terraform apply` xanh nhưng không có task nào chạy | Instance không đăng ký vào cluster; ASG vẫn báo Healthy | `bash infra/tf/scripts/wait-for-capacity.sh` rồi theo hướng dẫn chẩn đoán nó in ra |
| `cloud-init status` = `running` + `systemctl` treo dưới cloud-init | `user_data` gọi `systemctl start ecs` → deadlock với `cloud-final.service` | `pkill -f "systemctl enable --now ecs"` để gỡ tạm; sửa hẳn là **bỏ dòng đó** khỏi `user_data.sh.tftpl` |
| `tg-api` unhealthy, task bị replace liên tục | RDS chưa `available`; `/health/ready` chạm DbContext | Chờ RDS available rồi mới bật service — xem mục thứ tự bật |
| ECS task `RESOURCE:MEMORY` | t3.micro 1GB hết RAM | Giảm `api_memory_hard` xuống 448, hoặc `instance_type = "t3.small"` ($0.0264/giờ, gấp đôi micro) |
| `CannotPullContainerError` | `image_tag` không có trên ECR, hoặc `enable_nat = false` | `aws ecr describe-images`; bật NAT |
| SSM `TargetNotConnected` | NAT tắt, hoặc SSM Agent chưa đăng ký | Bật NAT, đợi ~2 phút |
| `SessionManagerPlugin is not found` | Thiếu plugin trên máy | `brew install --cask session-manager-plugin`; hoặc dùng `ssm send-command` |
| ACM cert đứng ở `PENDING_VALIDATION` | CNAME sai tên (thiếu `.api`), hoặc còn bật proxy mây vàng | `dig +short CNAME <record>`; đặt DNS only |
| Gọi ALB bằng tên DNS thô trả **403** | Đúng như thiết kế — allowlist Host header chặn | Gửi kèm `-H "Host: hushstore.io.vn"` |
| Gọi ALB bằng tên DNS thô trả **503** thay vì 403 | Allowlist Host **chưa có hiệu lực** — default action vẫn forward | Đây là lỗi. Kiểm `aws elbv2 describe-rules` |
| `terraform apply` treo 30 phút rồi fail ở ACM | Record validation chưa tồn tại | Thêm CNAME vào Cloudflare trước; waiter đã được gate theo `enable_alb` nên chỉ ảnh hưởng khi bật serving stack |
| State lock treo, `OperationTypeApply` | Một tiến trình apply bị kill giữa đường | Kiểm không còn process terraform nào, đối chiếu thời điểm ghi state cuối trên S3, rồi `terraform force-unlock <ID>` |
| Browser timeout với MỌI URL, nhưng `status.sh` báo tất cả healthy | `enable_deny_demo = true` — NACL rule 50 đang chặn `my_ip` ở tầng mạng | `status.sh` in cảnh báo này ở đầu bảng. Đặt `enable_deny_demo = false` rồi apply. `down.sh` tự reset |
| Browser không mở được ngay sau `up.sh`, curl trả `000` | Record `alb` ở Cloudflare còn trỏ vào ALB của cửa sổ trước — tên DNS của ALB đổi mỗi lần tạo lại | `up.sh` đã so sánh và in giá trị mới cần dán. Sửa đúng một record `alb`, chờ ~1-2 phút |
| Deploy có downtime ~20–40s | Static host port + 1 instance, đúng như thiết kế | Xem mục đánh đổi trong spec |

## Kiểm thử bảo mật — lệnh đã dùng, kết quả đã đo

```bash
ALB=$(terraform output -raw alb_dns_name)

# Kịch bản 1 + 4: chỉ 80 và 443 mở
for p in 22 80 443 1433 8080; do
  printf "%-5s " $p; nc -z -G 4 -w 4 "$ALB" $p && echo OPEN || echo "chặn"
done
# đo được: 80 OPEN, 443 OPEN, còn 22/1433/8080 chặn

# Kịch bản 3: RDS không tới được từ ngoài
nc -z -G 4 -w 4 "$(terraform output -raw rds_endpoint)" 1433 || echo "chặn"

# Kịch bản 7: Host lạ không lọt sang tg-api
curl -sk -o /dev/null -w "%{http_code}\n" -H "Host: evil.com" "https://${ALB}/health/ready"
# đo được: 403

# Kịch bản 10: blast radius của task role
for a in s3:PutObject rds:DescribeDBInstances ssm:GetParameter iam:CreateUser; do
  printf "%-26s " "$a"
  aws iam simulate-principal-policy \
    --policy-source-arn "arn:aws:iam::${ACCT}:role/hushstore-task-app-role" \
    --action-names "$a" --resource-arns "*" \
    --query 'EvaluationResults[0].EvalDecision' --output text --profile hushstore
done
# đo được: s3:PutObject allowed, còn lại implicitDeny
```

Lưu ý khi viết lệnh trong zsh: `-H "Host: ..."` phải viết **tường minh trong từng
lệnh**. Gán vào biến rồi truyền không quote (`H='-H Host:x'; curl $H`) không hoạt
động — zsh không word-split như bash nên nó thành **một** argument và header bị
hỏng, làm mọi request trả 403 trông như lỗi hạ tầng. Tương tự, `"...${ACCT}:role/..."`
phải bọc ngoặc nhọn: `$ACCT:role` bị zsh hiểu `:r` là modifier và ăn mất ký tự.

## Việc đã biết còn nợ

| Việc | Vì sao chưa làm | Điều kiện làm |
|---|---|---|
| Test module `ecs` và `cicd` cần credential AWS | Cả hai đọc data source thật (`aws_ssm_parameter` lấy AMI ECS-optimized; `aws_caller_identity` dựng ARN) | Thêm `override_data` trong file test nếu muốn chạy hoàn toàn offline. Trên CI thì không phải vấn đề — `ci.yml` assume role plan rồi mới chạy test |
| `seeder_image_tag` còn ghim tay trong tfvars | Image seeder trên ECR hiện chỉ tồn tại ở một SHA khác `image_tag`; bỏ ghim ngay sẽ trỏ task seeder vào tag không tồn tại và lỗi chỉ hiện lúc `run-task` | Xoá dòng đó sau lần chạy CI đầu tiên thành công — lúc đó cả 4 image đã cùng một SHA và biến tự lấy giá trị của `image_tag` |
| IAM user `athena232` vẫn tồn tại | Quyết định giữ nguyên (2026-08-19) | `AdministratorAccess` gắn trực tiếp, có console password, **không MFA**, không có access key. Dùng đúng một lần lúc setup (2026-08-18T01:27:21Z) rồi không dùng lại. Đây là một đường admin đứng sẵn nằm **ngoài** SSO, tức nó ngược với tuyên bố "danh tính là IAM Identity Center" trong spec. Root đã có MFA nên đã đủ làm break-glass. Nếu đổi ý: bật MFA cho user này, hoặc xoá nó (`delete-login-profile` → `detach-user-policy` → `delete-user`) |
| 4 package NuGet có CVE | Đã quyết định để sau khi xong hạ tầng | `AutoMapper` 16.0.0→16.1.1, `Microsoft.OpenApi` 2.4.1→2.7.5, `System.Security.Cryptography.Xml` 9.0.0→9.0.18 và 10.0.0→10.0.10. Cả 4 là DoS qua đệ quy không kiểm soát, CVSS 7.5, đánh giá là không tới được trong codebase này |

## Chi phí

### RDS không miễn phí — CPU credit surplus là khoản lớn nhất

Đo bằng Cost Explorer ngày 2026-08-20 sau 13.67 giờ uptime thật, tách theo
`RECORD_TYPE` để thấy phần usage trước khi credit bù:

| Usage type | Giá niêm yết | Quy ra $/giờ uptime |
|---|---|---|
| `APS1-CPUCredits:db.t3` | **$0.9208** | **$0.0674** |
| `APS1-InstanceUsage:db.t3.micro` | $0.4237 | $0.0310 |
| `APS1-RDS:GP2-Storage` | $0.0583 | tính cả khi stopped |
| **Tổng RDS** | **$1.4028** | **~$0.098/giờ** |

Tức riêng phần CPU credit đã **xấp xỉ đúng bằng NAT + ALB cộng lại**
($0.0675/giờ) — hai thứ mà cả bộ script này tồn tại để tắt đi. Mô hình "RDS
nằm trong free tier nên chỉ NAT với ALB tốn tiền" là **sai**.

Nguyên nhân, đo bằng CloudWatch trong cửa sổ 00:15–01:44 UTC:

| Metric | Giá trị |
|---|---|
| `CPUUtilization` | trung bình **38.8%**, max 74% — baseline `db.t3.micro` là **10%** |
| `CPUCreditBalance` | **0 phẳng** cả cửa sổ — không bao giờ tích được credit nào |
| `CPUSurplusCreditBalance` | leo lên **44.9** — đang vay |
| `CPUSurplusCreditsCharged` | 0 trong cửa sổ — được quyết toán lúc stop |

SQL Server Express **không tải** vẫn ngồi ở ~36% CPU liên tục. Đồ thị theo thời
gian cho thấy nó không phải hiện tượng lúc khởi động: spike 63% trong lúc
recovery, rồi phẳng 36% suốt 70 phút còn lại. Nên "bật/tắt ít lần hơn" không
giúp gì — surplus tỉ lệ thuận với uptime.

**Không tắt được unlimited mode.** Khác EC2 (chọn được standard/unlimited), RDS
T3 không có tham số nào cho việc này: cả `create-db-instance` lẫn
`modify-db-instance` đều không có option credit. Đã kiểm bằng `aws rds ... help`.

**Đổi instance class cũng không rẻ hơn.** `db.t3.small` có baseline 20% (gấp
đôi) nên surplus giảm ~40%, nhưng instance hours gấp đôi và rơi ra khỏi free
tier — tổng ra xấp xỉ bằng. Đường duy nhất thoát hẳn là bỏ burstable
(`db.m5.large`), đắt hơn nhiều lần.

Kết luận: đòn bẩy duy nhất là **uptime**, tức đúng việc `down.sh` đang làm. Chỉ
cần sửa lại con số trong đầu: mỗi giờ bật là **$0.166**, không phải $0.0675.

**Trừ vào credit, không phải free tier.** Toàn bộ $1.5102 usage của account bị
credit bù đúng bằng $1.5102, net còn $0.0000000015 — nhưng đó là **credit trả
trước hữu hạn** (số dư $200 tính tới 2026-08-20), không phải free tier 12 tháng.
Không có API công khai nào đọc được số dư nên không tự cảnh báo được; theo dõi
bằng AWS Budgets (đã có, ngưỡng $20) và xem số dư ở console
**Billing → Credits**.

### Account này KHÔNG có free tier

Account tạo 2026-08-18, thuộc mô hình **free plan mới** (credit trả trước) chứ
không phải free tier 12 tháng. Bằng chứng đo được, không phải suy luận:
`APS1-InstanceUsage:db.t3.micro` nằm ở `RECORD_TYPE = Usage` với đúng $0.031/giờ
giá niêm yết — nếu còn 750h free tier thì dòng đó phải là $0.

Hệ quả: **EC2 và RDS cũng tính tiền**, và mọi thứ trừ vào credit. Mọi chỗ nào
trong tài liệu này còn nói "free tier" đều là sai và đã được sửa.

### Đơn giá — dùng giá ap-southeast-1, không phải us-east-1

Sai sót đã sửa: bảng cũ dùng $0.045 cho NAT và $0.0225 cho ALB, đó là giá
us-east-1. Giá APS1 lấy từ Pricing API (miễn phí, khác Cost Explorer $0.01/lần):

| | us-east-1 (sai) | ap-southeast-1 (đúng) |
|---|---|---|
| NAT Gateway | $0.045 | **$0.0590** |
| ALB | $0.0225 | **$0.0252** |
| EC2 t3.micro | — | $0.0132 |

### Tổng

| | $/giờ | 24/7 | Bật ~3h/ngày |
|---|---|---|---|
| **RDS CPU credit surplus** | **$0.0674** | $49.20/mo | ~$6.10/mo |
| NAT Gateway | $0.0590 | $43.10/mo | ~$5.40/mo |
| RDS instance | $0.0310 | $22.60/mo | ~$2.80/mo |
| ALB | $0.0252 | $18.40/mo | ~$2.30/mo |
| EC2 t3.micro | $0.0132 | $9.60/mo | ~$1.20/mo |
| RDS storage 20GB (tính cả khi stopped) | $0.0038 | ~$2.76/mo | ~$2.76/mo |
| EBS + ECR + S3 + CloudWatch | — | ~$4.50/mo | ~$4.00/mo |
| **Tổng** | **$0.1954** | **~$150/mo** | **~$24.5/mo** |

Với $200 credit và nhịp ~3h/ngày thì còn khoảng **8 tháng**. Rủi ro lớn nhất
không phải đơn giá mà là **để quên bật**: AWS tự start lại RDS sau 7 ngày stop,
và ở $0.098/giờ thì một tuần không ai để ý là **$16.5** bay âm thầm. Đó là lý do
Lambda cost-guard của Phase 3 là bắt buộc, không phải tuỳ chọn.

Bài học từ lần trước, đáng nhắc: tôi từng để RDS chạy qua đêm vì nghĩ "free tier
nên không sao", trong khi vẫn cẩn thận tắt NAT. Kết quả thật là **RDS 9.71
instance-hour so với NAT 0.083 giờ** — lệch 117 lần. Nguyên tắc đúng là tắt *mọi
thứ* khi không dùng, không chỉ tắt thứ đắt nhất.

## Tắt khẩn cấp

Xem `docs/emergency-shutdown.md` — ba đường tắt (Terraform toggle, AWS CLI,
Console) cho trường hợp phiên làm việc bị ngắt giữa lúc đang triển khai.
