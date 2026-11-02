# Bỏ NACL tùy chỉnh, kiểm soát truy cập bằng Security Group

Thiết kế ngày **2026-10-26**, theo góp ý của giảng viên. **Prod đã hoàn tất hai mốc
ngày 02/11/2026**, xác minh association/SG; xem [biên bản apply](bien-ban-apply-sg-2026-11-02.md).
Tài liệu này là quy trình tái thực hiện, không phải biên bản kiểm thử runtime.
Các báo cáo cũ và `evidence/` vẫn mô tả đúng cấu hình tại thời điểm đã đo.

## Chính sách đích

Sáu subnet dùng **default NACL do AWS tạo**, giữ allow-all IPv4 hai chiều.
Terraform không adopt/quản lý rule của default NACL. Không còn NACL tùy chỉnh,
demo DENY theo IP hoặc luật ephemeral phải bảo trì. Default **Security Group**
vẫn được Terraform khóa ingress/egress rỗng; đây là resource khác default NACL.

| SG | Inbound TCP | Outbound TCP |
|---|---|---|
| ALB | 80/443 từ Internet | 80/8080 đến `sg-web` |
| Web/EC2 | 80/8080 từ `sg-alb` | 5432 đến `sg-rds`; 80/443 đến Internet |
| RDS | 5432 từ `sg-web` | Rỗng |
| ECR endpoint, khi bật | 443 từ **`sg-web`**, thay cho CIDR app | Rỗng |
| Default SG | Rỗng | Rỗng |

HTTP/HTTPS outbound giữ nguyên theo lựa chọn của chủ đồ án, phục vụ tương thích
bảo trì và các API ngoài. SG không lọc URL/domain. NAT và các công tắc endpoint
giữ nguyên; hai mốc NACL không thêm dịch vụ tính phí. Yêu cầu WAF mới được xử lý
**sau hai mốc này** theo [runbook Cloudflare Free](waf-cloudflare.md), giữ
`enable_cloudflare_origin_lockdown = false` trong cả hai plan migration.
Hai ECR endpoint không thay
thế toàn bộ phụ thuộc NAT của ECS, SSM, CloudWatch và các API bên ngoài.

SG là stateful, chỉ có allow: không cần mở dải ephemeral cho response và không
thể chặn riêng một IP trong khi vẫn allow `0.0.0.0/0`. Egress rỗng của RDS không
chặn dữ liệu trả về trên kết nối được phép. ECS dùng `bridge` và host port cố
định, nên `sg-web` bảo vệ **host**, không cô lập từng container trên cùng host.

Tham khảo: [AWS SG rules](https://docs.aws.amazon.com/vpc/latest/userguide/security-group-rules.html),
[AWS default NACL](https://docs.aws.amazon.com/vpc/latest/userguide/default-network-acl.html),
[HashiCorp default ACL caveats](https://registry.terraform.io/providers/hashicorp/aws/latest/docs/resources/default_network_acl).

## Hai mốc triển khai riêng

**Không apply thẳng cấu hình cuối vào stack còn dùng custom NACL.** Terraform
có thể xóa standalone rule trước khi tháo association, gây mất kết nối tạm thời.

`up.sh`, `down.sh` và hàm chung `hs_apply` hiện đọc `terraform state list` rồi
dừng nếu còn bất kỳ custom `aws_network_acl` nào, kể cả chỉ còn một tier sau
migration dở dang. Lỗi đọc state cũng làm dừng trước thay đổi AWS/tfvars.
Hoàn tất hai mốc bằng các cây riêng bên dưới; không bỏ guard để chạy script.
Guard này không chặn được lệnh `terraform apply` gõ tay ngoài các script.

Giữ cờ origin lockdown false: task definition API không thêm biến proxy ở chế
độ này, tránh phát sinh revision ngoài allowlist của hai plan NACL. Việc đổi
IP visitor, backend Cloudflare và WAF chỉ được triển khai ở đợt tiếp theo.

Mốc 1 giữ nguyên địa chỉ ba ACL và các rule cũ, chỉ đặt `subnet_ids = []` để trả
sáu subnet về default ACL. Rule endpoint từ `sg-web` được thêm song song với
rule CIDR cũ. Mốc 2 mới xóa các ACL/rule cũ và rule endpoint theo CIDR.

Hai cây Terraform độc lập được tạo bằng script Python chuẩn, không cần thư viện
ngoài. Script chỉ sao chép cấu hình cục bộ, **không chạy AWS/Terraform**, không
sao chép `.terraform`, state hay tfvars. Template `legacy-network.tf.in` không
được Terraform nạp trong cấu hình cuối. Không đổi tên template thành `.tf` trong
module chính. Dev vẫn dùng symlink `main.tf`/`outputs.tf` đến prod.

Các lệnh dưới đây dành cho **phiên triển khai được lên lịch sau**, không phải
các thao tác đã thực hiện. Chạy tại gốc repo. Dùng đúng môi trường và cùng
tfvars của stack hiện tại; không dùng file example vì có thể đổi công tắc chi phí.

```bash
REPO_ROOT=$(pwd)
MIGRATION_TOOL="$REPO_ROOT/infra/tf/migrations/nacl-to-sg/migrate.py"
STACK_ENV=prod
STACK_VARS="$REPO_ROOT/infra/tf/envs/$STACK_ENV/terraform.tfvars"
MIGRATION_DIR=$(mktemp -d /tmp/hushstore-nacl-migration.XXXXXX)
chmod 700 "$MIGRATION_DIR"
python3 "$MIGRATION_TOOL" prepare "$MIGRATION_DIR/stage1"
python3 "$MIGRATION_TOOL" prepare "$MIGRATION_DIR/stage2" --stage 2
```

Giữ tfvars gốc cho mốc 1, kể cả `my_ip`/`enable_deny_demo` nếu đang có, để không
vô tình đổi demo rule trước khi tháo subnet. Backend bucket/key/profile được
giữ nguyên trong hai cây. Kiểm tra đúng account/profile/region và backend key
trước init. Không dùng `-migrate-state`, không tạo state mới, không `state rm`.
Plan JSON/state có thể chứa secret: chỉ lưu trong thư mục riêng ở trên,
không commit hoặc đưa vào bằng chứng công khai.

### 1. Kiểm tra default ACL trước khi chuyển

Lấy VPC ID của stack hiện tại và đọc ACL/subnet. Các API này chỉ đọc:

```bash
VPC_ID=$(terraform -chdir="infra/tf/envs/$STACK_ENV" output -raw vpc_id)
aws --profile hushstore --region ap-southeast-1 ec2 describe-network-acls \
  --filters "Name=vpc-id,Values=$VPC_ID" > "$MIGRATION_DIR/acls-before.json"
aws --profile hushstore --region ap-southeast-1 ec2 describe-subnets \
  --filters "Name=vpc-id,Values=$VPC_ID" > "$MIGRATION_DIR/subnets.json"
python3 "$MIGRATION_TOOL" check-acls \
  "$MIGRATION_DIR/acls-before.json" "$MIGRATION_DIR/subnets.json"
```

Chốt kiểm yêu cầu đúng sáu subnet cùng VPC, một default ACL, rule 100 `ALLOW`
protocol `-1`, `0.0.0.0/0` cho cả inbound/outbound; ngoài rule cuối `*` không có
rule IPv4 tùy chỉnh khác. Có rule DENY số thấp hoặc thiếu ALLOW thì **dừng**.
Đối chiếu cấu hình thực tế và xử lý sai lệch riêng, không bỏ qua chốt kiểm.
Kiến trúc hiện tại chỉ dùng IPv4; công cụ này không chứng nhận policy IPv6.

### 2. Plan và triển khai mốc 1: tháo association

```bash
terraform -chdir="$MIGRATION_DIR/stage1/envs/$STACK_ENV" init -input=false
terraform -chdir="$MIGRATION_DIR/stage1/envs/$STACK_ENV" plan \
  -var-file="$STACK_VARS" -out="$MIGRATION_DIR/stage1.tfplan"
terraform -chdir="$MIGRATION_DIR/stage1/envs/$STACK_ENV" show -json \
  "$MIGRATION_DIR/stage1.tfplan" > "$MIGRATION_DIR/stage1-plan.json"
python3 "$MIGRATION_TOOL" check-plan 1 "$MIGRATION_DIR/stage1-plan.json"
```

Đọc plan trước khi triển khai. Chỉ chấp nhận thay đổi association của ba ACL
về danh sách rỗng và thêm rule SG endpoint nếu endpoint đang bật. Không xóa
rule/ACL ở mốc này; không bật NAT/ALB/EC2, thay thế SG/RDS/subnet/VPC hay sửa
resource ngoài phạm vi. Công cụ kiểm plan từ chối drift và thay đổi ngoài danh
sách cho phép; xử lý chúng bằng một thay đổi được review riêng.

Sau khi review plan đạt, người vận hành triển khai đúng saved plan:

```bash
terraform -chdir="$MIGRATION_DIR/stage1/envs/$STACK_ENV" apply "$MIGRATION_DIR/stage1.tfplan"
aws --profile hushstore --region ap-southeast-1 ec2 describe-network-acls \
  --filters "Name=vpc-id,Values=$VPC_ID" > "$MIGRATION_DIR/acls-detached.json"
python3 "$MIGRATION_TOOL" check-acls \
  "$MIGRATION_DIR/acls-detached.json" "$MIGRATION_DIR/subnets.json" --detached
```

Chốt sau apply phải xác nhận **cả sáu subnet** gắn default ACL. Nếu thiếu một
subnet hoặc apply lỗi giữa chừng: dừng mốc 2, đọc state/AWS thực tế và plan lại
mốc 1. Giữ nguyên rule ACL cũ cho đến khi chốt association đạt.

### 3. Mốc 2: xóa tài nguyên và cấu hình cũ

Tạo bản sao tfvars riêng cho mốc 2 rồi bỏ **chỉ** hai assignment `my_ip` và
`enable_deny_demo`. Giữ nguyên mọi công tắc/secret khác; không in nội dung file:

```bash
cp "$STACK_VARS" "$MIGRATION_DIR/stage2.tfvars"
chmod 600 "$MIGRATION_DIR/stage2.tfvars"
# Mở stage2.tfvars bằng trình soạn thảo và bỏ my_ip, enable_deny_demo.
terraform -chdir="$MIGRATION_DIR/stage2/envs/$STACK_ENV" init -input=false
terraform -chdir="$MIGRATION_DIR/stage2/envs/$STACK_ENV" plan \
  -var-file="$MIGRATION_DIR/stage2.tfvars" -out="$MIGRATION_DIR/stage2.tfplan"
terraform -chdir="$MIGRATION_DIR/stage2/envs/$STACK_ENV" show -json \
  "$MIGRATION_DIR/stage2.tfplan" > "$MIGRATION_DIR/stage2-plan.json"
python3 "$MIGRATION_TOOL" check-plan 2 "$MIGRATION_DIR/stage2-plan.json"
```

Mốc 2 chỉ được xóa ba custom ACL, các rule của chúng và rule endpoint CIDR cũ
nếu tồn tại. Không replacement hay tài nguyên tính phí mới. Kiểm tra lại
association ngay trước khi apply nếu có khoảng cách thời gian hoặc thao tác
Console xen giữa. Sau khi review đạt:

```bash
terraform -chdir="$MIGRATION_DIR/stage2/envs/$STACK_ENV" apply "$MIGRATION_DIR/stage2.tfplan"
terraform -chdir="$MIGRATION_DIR/stage2/envs/$STACK_ENV" plan \
  -var-file="$MIGRATION_DIR/stage2.tfvars" -detailed-exitcode
```

Plan cuối kỳ vọng exit **0 / No changes**; exit 2 là còn thay đổi, exit 1 là lỗi.
Đọc lại ACL, SG và trạng thái các resource tính phí. Cập nhật tfvars cục bộ gốc
bằng cách bỏ hai biến cũ để lần vận hành tiếp theo không có cảnh báo biến thừa.
Đừng gọi `down.sh` hay `up.sh` chỉ để áp dụng thay đổi mạng này.

### Khôi phục

Mốc 1 vẫn giữ các rule ACL cũ nên nếu cần quay về, tạo bản cấu hình mốc 1 có
`subnet_ids` gắn lại đúng hai subnet mỗi tier; review plan chỉ đổi association
rồi mới triển khai. Không chỉ checkout mã cũ và apply toàn bộ vì có thể đổi
các công tắc khác. Nếu mốc 2 đã xóa ACL, việc khôi phục cần tạo lại ACL/rule đầy
đủ **trước** khi chuyển association; phải lập plan riêng. Không hứa rollback
không gián đoạn hoặc dùng state backup để ghi đè trạng thái AWS đang tồn tại.

## Kiểm thử cấu hình và kiểm thử runtime

Kiểm tra cục bộ:

```bash
python3 -m unittest discover -s infra/tf/migrations/nacl-to-sg -p 'test_*.py' -v
terraform -chdir=infra/tf/modules/network test
terraform -chdir=infra/tf/modules/security test
```

Network/security dùng mock provider; chúng kiểm tra policy dự kiến, không chứng
minh AWS đã thay đổi. Các module khác còn dùng provider thật cho STS/data
source, nên toàn bộ suite không được mô tả là offline. Nếu cache thiếu checksum
macOS, `terraform init -backend=false` tải provider đã ký và bổ sung checksum
đúng nền tảng, giữ nguyên phiên bản khóa; không xóa lock hoặc tắt xác minh.

Trong **cửa sổ bật hạ tầng riêng**, ghi bản đo mới với thời điểm/account/VPC,
không sửa các bằng chứng cũ:

| Kịch bản | Kết quả cần kiểm chứng |
|---|---|
| ALB/web/API | 80 chuyển HTTPS; 443 đúng Host hoạt động, target healthy |
| Web → DB | Truy vấn PostgreSQL qua 5432 thành công từ host `sg-web` |
| EC2/SSM/ECS | Host đăng ký ECS, SSM hoạt động; task mới pull ECR và gửi CloudWatch log |
| Truy cập trực tiếp từ Internet | Không có public IP trên app/DB; 22/5432/8080 không mở công khai |
| Nguồn nội bộ sai SG | Probe trong VPC có route/egress hợp lệ nhưng không mang `sg-alb`/`sg-web` không mở được kết nối mới đến app/DB tương ứng |
| ECR endpoint, nếu đang bật | Host `sg-web` kết nối 443 được; probe cùng subnet nhưng sai SG không kết nối được |
| Default SG và default ACL | Default SG không rule; sáu subnet ở default ACL allow-all |
| Chi phí/trạng thái | Không bật thêm NAT/ALB/EC2/endpoint ngoài cấu hình được duyệt |

Probe âm phải có ca đối chứng dương và ghi rõ nguồn/đích, route, SG và thời
điểm; timeout từ Internet vào private IP không tự chứng minh SG đã chặn.
Kiểm tra **kết nối mới**, không tái sử dụng kết nối đang được connection tracking
cho phép. Nếu đã bật Flow Logs thì đối chiếu REJECT với probe; không tự bật để
tránh phát sinh phí ngoài phạm vi. Không tháo SG trên tài nguyên đang phục vụ
chỉ để làm ca đối chứng. Probe tạm có thể tốn phí, cần nằm trong cửa sổ đo riêng.

DNS công khai/Cloudflare 530 và CI/NuGet là tồn tại riêng. `curl --resolve` chỉ
chứng minh đường ALB/app khi bỏ qua DNS, không được ghi thành kết quả DNS đạt.
Không tái sử dụng kết quả KB8 NACL DENY hay tổng điểm cũ cho kiến trúc mới.

## Kiểm tra cục bộ đã chạy (2026-10-28)

20 test Python của công cụ migration đạt, gồm từ chối xóa ACL còn subnet,
thay đổi chưa biết trong plan và sao chép file ngoài manifest nguồn. Hai cây
stage1/stage2 đã init không backend và validate thành công ở cả prod/dev.
Template stage1 giữ rule cũ và địa chỉ resource; stage2 không có custom ACL.
Guard plan kiểm bằng fixture JSON, **chưa có live plan hai mốc hoặc apply**;
association thực tế và runtime phải kiểm trong phiên triển khai.

Checksum cache được xử lý bằng tải lại package provider ký hợp lệ và bổ sung hash
nền tảng vào lockfile; AWS provider vẫn 6.60.0, không tắt kiểm checksum.
Kết quả test cấu hình toàn bộ và bổ sung WAF nằm ở [trạng thái xác minh](waf-cloudflare.md#trạng-thái-xác-minh).
