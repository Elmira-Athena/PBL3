# Phase 3 — Lambda cost-guard tự tắt hằng đêm

> **Spec:** [2026-08-17-aws-terraform-ecs-infra-design.md](../specs/2026-08-17-aws-terraform-ecs-infra-design.md) — mục "Cost guard — module `costguard`"

**Goal:** Bịt rủi ro chi phí duy nhất trong hệ thống **không có trần trên**: một stack bị quên bật, hoặc một RDS đã stopped bị AWS tự khởi động lại sau 7 ngày.

**Architecture:** Một Lambda Python arm64 + EventBridge Scheduler cron 00:00 giờ Việt Nam. Lambda **chỉ gọi API scale/stop**, không xoá resource nào — xoá thứ Terraform quản lý sẽ gây state drift và làm hỏng lần `apply` sau. Những gì nó không tự tắt được (NAT Gateway, ALB) thì nó báo qua SNS kèm câu lệnh cần chạy.

**Tech Stack:** Terraform 1.15.8 · AWS provider ~> 6.0 · provider `archive` · Lambda Python 3.13 arm64 · EventBridge Scheduler · SNS

---

## Trạng thái Phase 3 trước plan này

Spec định nghĩa Phase 3 là "bước 11-12: module `costguard`, `up.sh`/`down.sh`/`nuke.sh`, 11 kịch bản tấn công, `security-validation-report.md`". Ba trong bốn đã xong:

| Việc | Trạng thái |
|---|---|
| `up.sh` / `down.sh` / `nuke.sh` / `status.sh` | ✅ xong, đã dùng thật nhiều lần |
| AWS Budgets + email cảnh báo | ✅ xong (dựng sớm hơn thứ tự spec, có chủ ý) |
| 11 kịch bản tấn công + báo cáo | ✅ xong 11/11 (kịch bản 11 khoá lại ngày 2026-08-23 sau khi Phase 2 apply) |
| **Lambda cost-guard + EventBridge Scheduler** | ❌ **việc duy nhất còn lại** |

---

## Vì sao việc này bắt buộc, không phải tuỳ chọn

Ba khoản rủi ro, và chúng khác nhau về **bản chất**, không chỉ về độ lớn:

**Rủi ro có trần: quên tắt một đêm.** NAT $0.0590/h + ALB $0.0252/h + EC2 $0.0132/h + RDS $0.098/h = **$0.1954/h**. Quên một đêm 10 tiếng là $1.95. Đau nhưng có trần, và `status.sh` cho thấy ngay hôm sau.

**Rủi ro KHÔNG có trần: RDS tự khởi động lại.** AWS **tự start** một RDS đã stopped sau **7 ngày**. Không có thông báo nào ngoài email cảnh báo ngân sách, mà email đó chỉ bắn khi đã tiêu tới 25% ngưỡng. Ở $0.098/h thì một RDS tự bật và chạy cả tuần là **$16.5/tuần**, tháng là **$71**. Đây là khoản duy nhất trong thiết kế có thể tiêu tiền **mà không ai từng bấm gì**, nên nó là lý do chính của plan này.

**Rủi ro tiềm ẩn: credit hết mà không biết.** Account đang chạy trên credit trả trước của gói free mới, không phải free tier 12 tháng. Hết credit thì mọi thứ chuyển sang thẻ thật. Một khoản $71/tháng không ai để ý là đúng cách để phát hiện điều đó bằng hoá đơn.

Lambda chạy mỗi đêm đưa rủi ro thứ hai từ **7 ngày** về **tối đa 24 giờ**: $16.5 → $2.35 cho trường hợp xấu nhất.

---

## Global Constraints

Chép nguyên văn, ràng buộc mọi task:

- **Không có gì được gửi tới `bach.huynhvan@smartdev.com`.** Mọi email đi tới `dacvinh2322006@gmail.com`.
- **Không SG rule nào mở port 22, ở bất kỳ đâu.**
- **Không nới `Deny ssm:GetParameter*` trên `/hushstore/*`** của `hushstore-container-instance-role`.
- `enable_nat` / `enable_alb` / `instance_count` giữ nguyên giá trị trong `terraform.tfvars` sau khi xong (mặc định tắt).
- Mọi thông báo hướng tới người dùng bằng **tiếng Việt có dấu**.
- **Lambda KHÔNG được xoá resource nào.** Nó chỉ `UpdateService`, `SetDesiredCapacity`, `StopDBInstance`. Xoá ALB/NAT ngoài Terraform là state drift.
- **Lambda KHÔNG được BẬT gì.** Không `StartDBInstance`, không `SetDesiredCapacity` với giá trị > 0. Một cost guard có quyền bật là một cost guard có thể gây ra đúng thứ nó tồn tại để chặn.
- 4 NuGet package có lỗ hổng chỉ nâng ở **cuối toàn bộ dự án**.
- Region `ap-southeast-1`. Cluster `hushstore`. ASG `hushstore-asg`. RDS `hushstore-db-tf`. Service `hushstore-web`, `hushstore-api`.

---

## Quyết định thiết kế

### 1. Lambda chỉ scale/stop — và điều đó KHÔNG đưa chi phí về $0

Phải nói thẳng con số, vì gọi nó là "cost guard" dễ làm người ta tưởng nó giải quyết hết:

| Khoản | Lambda tắt được? | Còn lại sau khi Lambda chạy |
|---|---|---|
| RDS instance ($0.098/h) | ✅ `StopDBInstance` | $0.004/h (storage gp2 20GB, tính cả khi stopped) |
| EC2 container instance ($0.0132/h) | ✅ ASG desired = 0, EBS root xoá cùng instance | $0 |
| ECS service | ✅ desired = 0 | $0 (không tính phí) |
| **NAT Gateway ($0.0590/h)** | ❌ **Terraform quản lý** | **$0.0590/h vẫn chạy** |
| **ALB ($0.0252/h)** | ❌ **Terraform quản lý** | **$0.0252/h vẫn chạy** |

Sau khi Lambda chạy: **$0.0882/h** thay vì $0.1954/h. Giảm 55%, không phải 100%.

Vì sao không cho Lambda xoá NAT/ALB: chúng nằm trong Terraform state. Xoá bằng API làm state lệch thực tế, và lần `terraform apply` sau sẽ hành xử sai — nhẹ thì tạo lại, nặng thì lỗi giữa apply và để hạ tầng nửa vời. Đây cũng chính là lý do bộ script dùng **toggle + apply** thay vì `destroy -target`.

Nên Lambda **báo** thay vì **làm**: nếu phát hiện NAT hoặc ALB còn tồn tại, nó gửi SNS kèm đúng câu lệnh. Con người đóng vòng lặp. Đánh đổi này được ghi vào runbook, không để ngầm.

### 2. Lambda không có quyền bật bất cứ thứ gì

`StartDBInstance` không có trong policy. `SetDesiredCapacity` bị chặn bằng condition để chỉ nhận giá trị 0 — nếu AWS không hỗ trợ condition trên tham số đó thì chấp nhận và ghi rõ giới hạn, nhưng vẫn không cấp `StartDBInstance`.

Lý do: một cost guard bị chiếm hoặc bị sửa sai mà có quyền bật là một cost guard có thể gây ra đúng thứ nó tồn tại để chặn.

### 3. EventBridge Scheduler, không phải EventBridge Rule

Scheduler nhận `schedule_expression_timezone = "Asia/Ho_Chi_Minh"` trực tiếp. Rule (`cron()`) chỉ hiểu UTC, nên phải tự trừ 7 giờ — và con số đó sai vào ngày ai đó đọc lại mà không biết nó đã bị trừ. Cả hai đều miễn phí ở quy mô này (14 triệu lượt/tháng free).

### 4. Idempotent và "đã tắt rồi" không phải lỗi

Chạy đêm thứ hai khi mọi thứ đã tắt phải là một lần chạy **thành công, im lặng** — không gửi SNS, không lỗi. Một cảnh báo bắn mỗi đêm là một cảnh báo bị bỏ qua. Chỉ gửi SNS khi (a) Lambda thực sự tắt cái gì, hoặc (b) phát hiện NAT/ALB còn sống, hoặc (c) có lỗi.

---

## Cấu trúc file

```
infra/tf/modules/costguard/
├── main.tf              (có sẵn — Budgets) + SNS topic + subscription
├── lambda.tf            MỚI: IAM role, Lambda, log group, archive_file
├── schedule.tf          MỚI: EventBridge Scheduler + role của scheduler
├── src/cost_guard.py    MỚI: mã Lambda
├── variables.tf         (có sẵn) + cluster_name, asg_name, rds_identifier,
│                        service_names, enable_auto_stop, stop_cron
├── outputs.tf           (có sẵn) + lambda_name, sns_topic_arn
├── versions.tf          (có sẵn) + provider archive
└── tests/costguard.tftest.hcl   MỚI

infra/tf/envs/prod/main.tf        truyền tên resource vào module costguard
infra/tf/envs/prod/variables.tf   + enable_auto_stop, stop_cron
infra/tf/envs/prod/outputs.tf     + lambda_name

docs/terraform-runbook.md         mục "Tự tắt hằng đêm", bảng chi phí cập nhật
README.md                         một câu trong mục Deploy
```

---

## Task

### Task 1 — SNS topic + subscription

**Files:** modify `infra/tf/modules/costguard/main.tf`, `variables.tf`, `outputs.tf`

Một topic `hushstore-costguard-alerts`, một email subscription tới `var.alert_email`.

Hai điều phải ghi comment:

`aws_sns_topic_subscription` với `protocol = "email"` **luôn hiện `pending_confirmation` trong state** cho tới khi người nhận bấm link xác nhận trong mail. Đó không phải lỗi và không phải drift — nhưng nó là thứ làm người ta tưởng apply chưa xong.

SNS topic không dùng `subscriber_sns_topic_arns` của Budgets: Budgets đang gửi email trực tiếp và giữ nguyên như vậy. Hai đường cảnh báo độc lập là có chủ ý — Budgets cảnh báo về **tiền đã tiêu**, Lambda cảnh báo về **trạng thái hạ tầng**. Gộp lại thì một cái hỏng làm mất cả hai.

**Test:** subscription trỏ đúng `var.alert_email`; topic không cho phép publish từ principal ngoài account.

### Task 2 — Mã Lambda

**Files:** create `infra/tf/modules/costguard/src/cost_guard.py`

Python 3.13, chỉ dùng `boto3` (có sẵn trong runtime, không cần layer).

Luồng, đúng thứ tự này và thứ tự là ràng buộc:

1. **ECS service về 0 trước.** Nếu hạ ASG trước, ECS thấy task chết và liên tục thử xếp lại task lên capacity đang biến mất — đúng vòng lặp mà `down.sh` đã gặp thật.
2. **ASG desired về 0.** Chỉ khi `DescribeAutoScalingGroups` cho thấy desired > 0.
3. **Stop RDS.** Chỉ khi status là `available`. Nếu đang `stopping`/`stopped`/`starting` thì bỏ qua và ghi lại trạng thái — gọi `StopDBInstance` trên một instance không `available` trả lỗi `InvalidDBInstanceState`.
4. **Kiểm tra NAT + ALB.** Chỉ đọc. Có thì đưa vào phần cảnh báo.
5. **Tổng hợp.** Không làm gì và không phát hiện gì → return im lặng, không SNS.

Xử lý lỗi: mỗi bước bọc `try/except` riêng, một bước lỗi **không** được ngăn các bước sau — tắt được 3 trong 4 thứ vẫn tốt hơn tắt được 0. Gom lỗi lại rồi gửi SNS một lần ở cuối, và `raise` ở cuối nếu có lỗi để lần chạy hiện ra là thất bại trong CloudWatch.

Mọi chuỗi trong SNS message bằng tiếng Việt có dấu, và message phải chứa đúng câu lệnh cần chạy khi NAT/ALB còn sống:

```
bash infra/tf/scripts/down.sh
```

**Test:** không có test tự động cho Python trong dự án này. Thay vào đó Task 6 gọi Lambda thật bằng `aws lambda invoke` khi stack đang tắt và khi đang bật, rồi đọc log.

### Task 3 — IAM role + Lambda resource

**Files:** create `infra/tf/modules/costguard/lambda.tf`

Role của Lambda, đúng những quyền này và không hơn:

| Action | Resource | Vì sao hẹp được đến đây |
|---|---|---|
| `ecs:UpdateService` | 2 service ARN dựng từ cluster + tên | Không phải `*` — Lambda không chạm service nào khác |
| `ecs:DescribeServices` | 2 service ARN | |
| `autoscaling:SetDesiredCapacity`, `DescribeAutoScalingGroups` | ARN của `hushstore-asg` | Describe không hỗ trợ resource-level → `*`, ghi rõ |
| `rds:StopDBInstance`, `rds:DescribeDBInstances` | ARN instance; Describe → `*` | **KHÔNG có `rds:StartDBInstance`** |
| `ec2:DescribeNatGateways` | `*` (không hỗ trợ resource-level) | chỉ đọc |
| `elasticloadbalancing:DescribeLoadBalancers` | `*` (không hỗ trợ resource-level) | chỉ đọc |
| `sns:Publish` | ARN topic | Đúng một topic |
| `logs:CreateLogStream`, `PutLogEvents` | log group của chính nó | Không dùng `AWSLambdaBasicExecutionRole` vì managed policy đó cấp trên `*` |

Lambda: `runtime = "python3.13"`, `architecture = ["arm64"]` (rẻ hơn x86 ~20% và ở quy mô này là $0), `timeout = 120`, `memory_size = 256`, `reserved_concurrent_executions = 1`.

`reserved_concurrent_executions = 1` là bảo hiểm chống chạy chồng: hai lần chạy song song sẽ cùng gọi `StopDBInstance` và cái thứ hai nhận `InvalidDBInstanceState` — một lỗi giả.

Log group tạo **bằng Terraform** với `retention_in_days = 3`, không để Lambda tự tạo: log group do Lambda tạo không có retention, tức giữ vĩnh viễn và trả tiền vĩnh viễn. Cùng lập luận đã dùng cho 4 log group của ECS ở Phase 1.

`data "archive_file"` zip thư mục `src/`. Thêm provider `archive` vào `versions.tf` của module **và** của `envs/prod`.

**Test:** policy của Lambda **không** chứa `rds:StartDBInstance`; không statement nào có `Resource = "*"` ngoài danh sách Describe đã biết; `sns:Publish` giới hạn đúng một ARN.

### Task 4 — EventBridge Scheduler

**Files:** create `infra/tf/modules/costguard/schedule.tf`

`aws_scheduler_schedule` với `schedule_expression = "cron(0 0 * * ? *)"` và `schedule_expression_timezone = "Asia/Ho_Chi_Minh"`, target là Lambda, `retry_policy` với `maximum_retry_attempts = 2`.

Scheduler cần **role riêng của chính nó** (`scheduler.amazonaws.com` assume) chỉ có `lambda:InvokeFunction` trên đúng function này. Đây không phải role của Lambda — trộn hai cái là cấp cho Lambda quyền tự gọi mình.

Gate bằng `var.enable_auto_stop` (default `true`). Đặt `false` khi cần để stack chạy qua đêm có chủ ý — và runbook phải nói rõ rằng lúc đó **không còn lưới an toàn nào**.

**Test:** timezone đúng `Asia/Ho_Chi_Minh` (không phải UTC — một cron UTC ở đây nghĩa là tắt lúc 7 giờ sáng); role của scheduler chỉ có `lambda:InvokeFunction`; `enable_auto_stop = false` thì không tạo schedule nào.

### Task 5 — Wire vào envs/prod

**Files:** modify `infra/tf/envs/prod/{main,variables,outputs}.tf`

Truyền `cluster_name`, `asg_name`, `rds_identifier`, `service_names` từ output của module `ecs` và `data`. Dùng **chuỗi** cho `service_names` như module `cicd` đã làm — service bị `enable_alb` gate nên tham chiếu output của nó sẽ làm policy đổi nội dung theo trạng thái bật/tắt.

### Task 6 — Kiểm chứng thật

Không phải `terraform test` — gọi Lambda thật:

```bash
# 1. Khi stack ĐANG TẮT: phải return im lặng, không gửi SNS
aws lambda invoke --function-name hushstore-cost-guard \
  --profile hushstore /dev/stdout
aws logs tail /aws/lambda/hushstore-cost-guard --since 5m --profile hushstore

# 2. Khi stack ĐANG BẬT (chạy up.sh trước, tốn ~$0.20 cho 1 tiếng):
#    phải tắt service + ASG + RDS, và cảnh báo NAT/ALB còn sống
aws lambda invoke --function-name hushstore-cost-guard \
  --profile hushstore /dev/stdout

# 3. Xác nhận bằng AWS API, không chỉ tin log của Lambda
bash infra/tf/scripts/status.sh
```

Phép thử số 2 là phép thử thật sự — nó là lần duy nhất chứng minh thứ tự "ECS trước ASG" hoạt động. Chi phí ~$0.20 và đáng, vì đường thay thế là phát hiện lỗi vào một đêm nào đó khi không ai xem.

Dán output vào runbook.

### Task 7 — Tài liệu

`docs/terraform-runbook.md`: mục "Tự tắt hằng đêm" nói rõ Lambda tắt gì và **không** tắt gì, kèm bảng $0.0882/h còn lại. Cập nhật mục chi phí. Thêm dòng troubleshooting: "email SNS ở `pending_confirmation`" và "Lambda chạy nhưng NAT vẫn còn".

`README.md`: một câu trong mục Deploy lên AWS.

---

## Verification

```bash
terraform -chdir=infra/tf fmt -check -recursive
terraform -chdir=infra/tf/modules/costguard init -backend=false && terraform -chdir=infra/tf/modules/costguard test
terraform -chdir=infra/tf/envs/prod validate && terraform -chdir=infra/tf/envs/prod plan
```

- `plan` phải cho thấy **chỉ** Lambda, IAM, log group, SNS, schedule được thêm. Không NAT, không ALB, không instance, không đổi RDS.
- Sau `apply`: `plan -detailed-exitcode` trả về 0.
- `aws scheduler get-schedule --name hushstore-nightly-stop` cho `Timezone: Asia/Ho_Chi_Minh`.
- `aws iam simulate-principal-policy` với action `rds:StartDBInstance` trên role của Lambda → **implicitDeny**. Đây là phép thử đáng đưa vào báo cáo bảo mật: cost guard không bật được gì.
- Task 6 chạy đủ cả 3 phép thử, output dán vào runbook.

---

## Rủi ro và đánh đổi

- **Lambda không đưa về $0.** NAT + ALB còn $0.0882/h. Nó bịt rủi ro *không có trần* (RDS tự start sau 7 ngày), không thay thế `down.sh`. Phải nói rõ trong runbook, nếu không nó tạo cảm giác an toàn sai.
- **`aws_sns_topic_subscription` email luôn `pending_confirmation`** cho tới khi bấm link. Nếu không ai bấm thì cảnh báo im lặng biến mất — mà "im lặng" cũng là trạng thái bình thường của Lambda, nên không phân biệt được. Task 6 phải xác nhận đã nhận được mail thật.
- **`SetDesiredCapacity` có thể không siết được theo giá trị.** Nếu AWS không hỗ trợ condition trên tham số đó thì Lambda về lý thuyết scale lên được. Bù bằng việc mã Lambda chỉ truyền hằng số 0 và không có `rds:StartDBInstance` — nhưng đây là giới hạn thật, ghi vào báo cáo.
- **Múi giờ.** 00:00 `Asia/Ho_Chi_Minh` là 17:00 UTC hôm trước. Ai đọc log CloudWatch (giờ UTC) sẽ thấy giờ "sai" — ghi vào runbook để khỏi mất thời gian.
- **Lambda chạy lúc đang deploy** sẽ hạ service về 0 giữa lúc pipeline đang `wait services-stable`, làm pipeline đỏ và để hệ thống tắt. Cửa sổ hẹp (deploy ~12 phút, Lambda chạy 1 lần/ngày lúc nửa đêm) nhưng không bằng 0. Không xử ở plan này; ghi nhận, và nếu xảy ra thì đường xử là để Lambda bỏ qua khi có deployment đang `IN_PROGRESS`.
