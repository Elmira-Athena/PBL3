# CI/CD cho người chưa biết gì

Tài liệu này giả định bạn **chưa từng dùng GitHub Actions**, chưa biết CI/CD là
gì, và chưa biết Docker image là gì. Nó không giả định bạn biết AWS — chỗ nào
cần AWS thì có liên kết sang tài liệu tương ứng.

Đọc hết mất khoảng **45 phút**. Nếu chỉ có 5 phút, đọc mục
[§7 — Đọc một lần chạy](#7-đọc-một-lần-chạy-nhìn-vào-đâu-để-biết-có-deploy-hay-không):
đó là thứ bạn cần khi vừa push code lên và muốn biết chuyện gì đang xảy ra.

Sơ đồ trực quan của toàn bộ luồng: mở
[diagrams/hushstore-aws-2026.drawio](diagrams/hushstore-aws-2026.drawio),
**trang 3 — Luồng CI/CD**.

---

## 1. CI/CD là gì, và nó giải vấn đề nào

Trước khi có CI/CD, đưa code lên máy chủ là một chuỗi việc tay:

```
sửa code → build → copy file lên server → restart service → mở web xem có chết không
```

Chuỗi đó có ba vấn đề, và cả ba đều **không phải vấn đề về tốc độ**:

| Vấn đề | Vì sao nó nghiêm trọng |
|---|---|
| **Không lặp lại được** | Người A làm 6 bước, người B làm 5 bước và quên bước thứ 6. Không ai biết bước nào bị quên tới khi hệ thống chết. |
| **Không biết production đang chạy code nào** | Copy file lên rồi sửa thêm một dòng trực tiếp trên server — từ giờ code trên máy chủ và code trong Git là hai thứ khác nhau. |
| **Không có đường quay lại** | Bản mới lỗi thì "quay về bản cũ" nghĩa là build lại từ một commit mà bạn phải đoán. |

**CI** (Continuous Integration — tích hợp liên tục) = mỗi lần có code mới thì tự
động **kiểm tra** nó: build được không, test có xanh không, format có đúng không.

**CD** (Continuous Deployment — triển khai liên tục) = sau khi kiểm tra xanh thì
tự động **đưa lên máy chủ**.

Điều quan trọng: CI/CD **không phải là tự động hoá cho nhanh**. Nó là biến một
quy trình nằm trong đầu người thành một quy trình nằm trong file — để nó lặp lại
được, và để khi nó sai thì có log đọc.

---

## 2. Bảy từ vựng bắt buộc

Toàn bộ phần còn lại của tài liệu dùng bảy từ này. Không nhớ chúng thì đọc file
YAML sẽ như đọc mật mã.

| Từ | Nghĩa | Trong dự án này |
|---|---|---|
| **workflow** | Một file `.yml` mô tả "khi X xảy ra thì làm Y". | Có 2 file: `ci.yml` và `deploy.yml` |
| **trigger** (`on:`) | Điều kiện để workflow chạy. | `ci.yml` chạy khi có pull request. `deploy.yml` chạy khi push vào `main`. |
| **job** | Một khối việc, chạy trên **một máy riêng**. Các job mặc định chạy **song song**. | `deploy.yml` có 5 job: `build`, `migration-script`, `preflight`, `deploy`, `summary` |
| **step** | Một bước trong job. Các step chạy **tuần tự** trên cùng máy. | "Assume role qua OIDC", "Đăng nhập ECR", "Build và push"… |
| **runner** | Máy ảo GitHub cấp cho job. Ở đây là `ubuntu-latest`. **Nó bị xoá sạch sau khi job xong.** | Đây là lý do mọi job đều phải tự `checkout` và tự lấy credential lại. |
| **matrix** | Cách nói "chạy job này nhiều lần với tham số khác nhau". | `build` chạy 4 lần: `api`, `web`, `migrator`, `seeder` |
| **artifact** | File do một job tạo ra và giữ lại để tải về xem. | `migrate-<sha>.sql` — câu SQL sẽ chạy lên database |

### Hai từ nữa, và đây là chỗ người mới hay nhầm

**`secrets` vs `vars`.** GitHub có hai kho giá trị:

- `secrets.X` — bị **che** trong log, dùng cho thứ bí mật.
- `vars.X` — hiện nguyên văn trong log, dùng cho cấu hình không bí mật.

Dự án này dùng `vars.AWS_DEPLOY_ROLE_ARN`, **không** phải `secrets`. Lý do đáng
nhớ: **ARN của một IAM role không phải bí mật.** Nó chỉ là một cái tên. Biết tên
role không cho bạn quyền dùng role đó — quyền nằm ở *trust policy* của role, mà
trust policy chỉ chấp nhận token OIDC của đúng repo này, đúng branch này.

Nhét một giá trị không bí mật vào `secrets` là có hại: log bị che mất thông tin
mà bạn cần khi debug, để đổi lấy một sự bảo vệ không tồn tại.

---

## 3. Hai workflow của dự án này

| | [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) | [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) |
|---|---|---|
| Chạy khi nào | Mọi pull request, và push vào `main` | Push vào `main`, hoặc bấm tay |
| Câu hỏi nó trả lời | *Code này có đúng không?* | *Đưa code này lên chạy.* |
| Tốn tiền AWS không | Không — `terraform test` chạy ở chế độ `plan`, không tạo resource thật | Không, nếu hạ tầng đang tắt. Nó **không được phép bật hạ tầng** |
| Số dòng | 239 | 771 |

Hai file này dài, nhưng phần lớn là **comment tiếng Việt giải thích vì sao**. Đọc
file gốc là cách học tốt nhất — tài liệu này chỉ dựng khung để bạn đọc file gốc
không bị choáng.

---

## 4. `ci.yml` — cổng chất lượng

Ba job, chạy song song:

```
                    ┌─ terraform-static ── fmt -check + validate mọi module
pull request ───────┼─ terraform-test ──── 105 test trên 8 module
                    └─ dotnet-build ────── build toàn solution
```

**`terraform-static`** — kiểm định dạng và cú pháp. Không cần credential AWS
(dùng `init -backend=false`, bỏ qua hoàn toàn cấu hình backend S3). Sai format là
đỏ. Nghe khắt khe vô ích, nhưng nó giữ `git diff` sạch, nên khi review bạn thấy
được **thay đổi thật** chứ không phải nhiễu do khoảng trắng.

**`terraform-test`** — 105 test tự động trên 8 module. Đây là lưới an toàn chính
của hạ tầng. Chúng chạy ở `command = plan`: Terraform tính ra *sẽ* tạo gì rồi
kiểm khẳng định trên kết quả đó, **không tạo resource thật** → $0.

Ví dụ một test thật, ở
[`modules/network/tests/vpc.tftest.hcl`](../infra/tf/modules/network/tests/vpc.tftest.hcl):
"khi `enable_nat = false` thì phải có **0** NAT Gateway". Nghe hiển nhiên. Nhưng
nó bắt được đúng loại lỗi mà mắt người bỏ qua: ai đó sửa một biến ở chỗ khác làm
NAT Gateway được tạo dù cờ đang tắt — và NAT Gateway tốn $0,045/giờ, im lặng.

**`dotnet-build`** — build toàn solution. Nói thẳng giới hạn: dự án **không có
test tự động cho phần .NET**. Build xanh chỉ chứng minh code biên dịch được, không
chứng minh nó chạy đúng.

> **Không có `terraform apply` ở bất kỳ đâu, trong cả hai workflow.** Đây là quyết
> định, không phải thiếu sót. Hạ tầng tốn tiền theo giờ; một workflow tự `apply`
> nghĩa là mỗi lần merge là một lần bắt đầu tính tiền, và không ai nhận ra tới
> lúc đọc hoá đơn. Rủi ro đó **không có trần trên**.

---

## 5. `deploy.yml` — từng bước một

### 5.1. Ba job đầu chạy song song

```
push vào main
     │
     ├─ build (matrix ×4)   →  4 image lên ECR, tag = git SHA
     ├─ migration-script    →  migrate-<sha>.sql lên S3 + artifact
     └─ preflight           →  ĐỌC trạng thái thật trên AWS
```

**`build`** dựng 4 Docker image và đẩy lên **ECR** (kho image của AWS):

| Image | Là gì |
|---|---|
| `api` | Backend .NET 10, nghe port 8080 |
| `web` | Bundle Blazor WebAssembly, phục vụ bằng nginx port 80 |
| `migrator` | Một chương trình chạy-rồi-thoát, chứa toàn bộ migration database |
| `seeder` | Chạy-rồi-thoát, nạp dữ liệu mẫu |

Hai chi tiết ở job này đáng biết:

- **`fail-fast: true`** — bốn image phải cùng một SHA mới deploy được. Một cái
  fail thì ba cái kia là rác, nên dừng luôn cho khỏi tốn phút runner.
- **Bước "Image đã tồn tại chưa"** — ECR bật `IMMUTABLE`, nghĩa là push lại cùng
  một tag bị **từ chối**, không phải ghi đè. Hệ quả thực tế: bấm *Re-run jobs*
  trên một commit đã build xong sẽ fail ở bước push nếu không kiểm tra trước.
  Nên job kiểm tra trước rồi bỏ qua build.

**`migration-script`** sinh ra file SQL mà migration *sẽ* chạy, để người review
đọc được **trước khi** nó chạy lên database thật. Nó cũng là một phép kiểm: sinh
script fail nghĩa là tập migration không nhất quán — và biết điều đó ở đây rẻ hơn
nhiều so với biết lúc `migrator` đã bắt đầu sửa schema.

**`preflight`** không thay đổi gì cả. Nó chỉ **đọc** ba thứ trên AWS:

| Đọc gì | Cần giá trị nào để deploy được |
|---|---|
| Trạng thái RDS | `available` |
| Số container instance ACTIVE trong cluster | `≥ 1` |
| Số ECS service ACTIVE | đúng `2` |

Thiếu bất kỳ điều kiện nào → job `deploy` **bị bỏ qua**, và **workflow vẫn xanh**.

### 5.2. Nhánh "chưa deploy" — và vì sao nó xanh

Đây là chỗ dễ nhầm nhất của cả dự án, nên nói rõ:

**Hạ tầng của dự án này mặc định TẮT.** `enable_alb = false`, `enable_nat = false`,
`instance_count = 0`, RDS `stopped`. Bật lên tốn **$0,1954/giờ** (số đo thật, xem
[terraform-runbook.md](terraform-runbook.md)).

Nên khi bạn push code mà hạ tầng đang tắt:

- 4 image **được build và đẩy lên ECR** thành công (ECR là endpoint công khai,
  không cần hạ tầng bật — chỉ việc *pull* từ trong private subnet mới cần NAT).
- Job `deploy` bị bỏ qua.
- Workflow **xanh**.
- Website **không đổi gì**.

Đó là trạng thái **bình thường**, không phải lỗi. Nhưng "Actions xanh mà web chưa
đổi" đúng là loại nhầm lẫn ăn hết một buổi debug — nên job `summary` in ra rõ:

```
### CHƯA DEPLOY — chỉ build và push image
Hạ tầng đang tắt. Đây là trạng thái bình thường của dự án này, không phải lỗi:
> Không có container instance nào ACTIVE (instance_count = 0). Chỉ có 0/2 ECS service ACTIVE (enable_alb = false).
```

kèm luôn hai lệnh để đưa image đó lên chạy.

### 5.3. Job `deploy` — tám bước

Chỉ chạy khi `preflight` nói `ready == true`.

| # | Bước | Vì sao có bước này |
|---|---|---|
| ① | Ghi lại revision **đang** chạy | Không có bước này thì rollback là **đoán**. Rollback nghĩa là "trỏ về cái vừa rồi", nên phải ghi lại "cái vừa rồi" là gì. |
| ② | Đăng ký 3 task definition revision mới, trỏ image tag `:SHA` | Xem [§6.3](#63-cái-bẫy-ở-bước-④--đáng-đọc-kỹ) — đây là bước chống một lỗi im lặng rất khó thấy. |
| ③ | Snapshot RDS `pre-migrate-<sha>` | Đường quay lại của **dữ liệu**. Chính sách là forward-only, không dùng down-migration. |
| ④ | `run-task` migrator, chờ nó `STOPPED`, đọc exit code | **Đây là cổng.** |
| ⑤ | `update-service` ×2 (web, api) | Chỉ chạy khi ④ exit 0. |
| ⑥ | `wait services-stable` | Tín hiệu sức khoẻ **có thẩm quyền**: target group của ALB báo healthy. |
| ⑦ | `curl` qua tên miền công khai | **Chỉ thông báo, không phải cổng.** Xem [§6.4](#64-vì-sao-bước-⑦-không-phải-cổng). |
| ⑧ | Dọn snapshot cũ, giữ 3 cái mới nhất | Giữ hoá đơn storage không phình lên vô hạn. |

Nếu bất kỳ bước nào từ ⑤ tới ⑥ fail → **rollback**: trỏ hai service về revision
đã ghi ở bước ①.

**Migration KHÔNG được rollback.** Code quay về được, schema thì không — đường về
của dữ liệu là snapshot ở bước ③, và dùng nó là **quyết định của người**, không
phải của pipeline.

---

## 6. Bốn quyết định thiết kế, và lý do

### 6.1. OIDC, không phải secret

Pipeline **trước** đây giữ một private key SSH trong GitHub Secrets
(`EC2_SSH_KEY`), rồi `rsync` file lên host và `docker compose up`.

Vấn đề của cách đó không phải là chậm. Là: **key đó không hết hạn.** Ai đọc được
secret là vào được máy chủ, **mãi mãi**.

OIDC đổi hẳn mô hình:

```
GitHub cấp cho job một token sống vài phút, ký bởi GitHub
        │
        ▼
AWS kiểm token đó với trust policy của role
   ("sub phải là repo:<owner>/PBL3:ref:refs/heads/main")
        │
        ▼
AWS đổi lấy credential TẠM THỜI, hết hạn sau ~1 giờ
```

Kết quả: **không còn gì để trộm, vì không còn gì được lưu.** Trong toàn hệ thống
hiện không còn credential dài hạn nào — CI đã OIDC, SSH đã bỏ, registry đã dùng
IAM thay vì token.

Đã kiểm chứng bằng cách lấy ARN của role rồi thử `assume-role-with-web-identity`
từ máy ngoài → `AccessDenied`.

### 6.2. Tag bằng git SHA, không phải `:latest`

`:latest` không trả lời được câu hỏi **"production đang chạy commit nào?"**

Với tag = SHA, câu trả lời là một commit cụ thể. Và rollback là **trỏ lại một
revision đã tồn tại**, không phải build lại từ một commit bạn phải tìm.

Điều làm cho việc "trỏ lại" đáng tin là ECR bật `IMMUTABLE`: một tag đã push
không ghi đè được. Nghĩa là `hushstore-api:abc1234` hôm nay và tháng sau là
**cùng một image**, không phải "cùng một cái tên".

### 6.3. Cái bẫy ở bước ④ — đáng đọc kỹ

Đây là loại lỗi mà pipeline vẫn xanh, mọi bước vẫn pass, và hệ thống vẫn hỏng.

Gọi `run-task --task-definition hushstore-migrator` (chỉ **tên family**) thì ECS
tự chọn **revision ACTIVE mới nhất**. Revision đó do `terraform apply` đăng ký, mà
image của nó là `image_tag` **ghim tay** trong `terraform.tfvars` — **không phải**
commit vừa push.

Chuỗi hậu quả:

```
migrator chạy image CŨ
   → efbundle cũ không chứa migration mới
   → nó exit 0
   → cổng PASS
   → api/web bản MỚI được deploy lên một schema THIẾU migration
   → và không có tín hiệu nào ở đâu cả
```

Vì sao không có tín hiệu: `/health/ready` chỉ kiểm **kết nối** tới database, không
kiểm **schema**. App khởi động bình thường, health check xanh, ALB đưa traffic vào
— rồi chết ở request đầu tiên chạm vào cột chưa tồn tại.

Cách chặn: **đăng ký revision TRƯỚC** (bước ②), rồi `run-task` bằng **ARN có số
revision**, không phải tên family.

### 6.4. Vì sao bước ⑦ không phải cổng

Bước ⑦ gọi `https://api.hushstore.io.vn/health/ready` — tức đi **qua Cloudflare**.

Nên nó đỏ cả khi DNS chưa trỏ đúng, hoặc proxy sai chế độ — trong lúc container
hoàn toàn khoẻ. **Rollback vì lỗi DNS là rollback sai.**

Nên bước này đặt `continue-on-error: true` và chỉ ghi một warning nói rõ: nghi vấn
ở tầng DNS/Cloudflare, **không** ở tầng ứng dụng. Tín hiệu có thẩm quyền là target
group, đã kiểm ở bước ⑥.

Nguyên tắc chung rút ra: **một phép kiểm chỉ được làm cổng nếu nó fail đúng vì
cái nó đang đo.** Phép kiểm đi qua nhiều tầng ngoài tầm kiểm soát thì làm tín
hiệu, không làm cổng.

---

## 7. Đọc một lần chạy: nhìn vào đâu để biết có deploy hay không

Vào tab **Actions** trên GitHub → chọn lần chạy → xem phần **Summary**.

Job `summary` luôn chạy (`if: always()`), kể cả khi có job đỏ. Nó in một bảng và
**một trong bốn kết luận**:

| Kết luận trong summary | Nghĩa | Việc cần làm |
|---|---|---|
| **Đã deploy.** Hai service đang chạy image tag `<sha>` | Xong. | Không |
| **CHƯA DEPLOY — chỉ build và push image** | Hạ tầng đang tắt. Bình thường. | Nếu muốn deploy: sửa `image_tag` trong `terraform.tfvars` rồi `bash infra/tf/scripts/up.sh` |
| **CHƯA BIẾT TRẠNG THÁI HẠ TẦNG — preflight không chạy xong** | `preflight` fail nên không kết luận được gì | Xem log job `preflight`. Ba nguyên nhân thường gặp: thiếu repository variable `AWS_DEPLOY_ROLE_ARN`, OIDC không đổi được token, AWS trả lỗi tạm |
| **Deploy KHÔNG thành công** | Đã thử deploy và fail | Xem log job `deploy`. Rollback đã tự chạy nếu bước ① kịp ghi revision |

Nhánh thứ ba tồn tại vì một lý do cụ thể: nếu chỉ rẽ theo `ready == 'true'` thì
mọi thất bại của `preflight` sẽ bị đẩy vào nhánh "hạ tầng đang tắt" — in ra "đây
là trạng thái bình thường" trong khi bảng ngay phía trên nói
`| Preflight | failure |`. **Một summary tự mâu thuẫn còn tệ hơn không có
summary.**

---

## 8. Khi nó đỏ — triệu chứng và nguyên nhân

| Log nói gì | Nguyên nhân | Sửa |
|---|---|---|
| `Could not load credentials` ở bước assume role | Thiếu `permissions: id-token: write`, hoặc thiếu `vars.AWS_DEPLOY_ROLE_ARN` | Cả hai phải có. Không có `id-token: write` thì job **không xin được** OIDC token, và thông báo lỗi không nói vì sao |
| `NETSDK1004: Assets file ... project.assets.json not found` | `dotnet ef` hỏi metadata project bằng MSBuild target chạy **trước** bước build, và target đó **không tự restore** | Phải `dotnet restore` trước. Máy local không gặp vì `obj/` đã có sẵn từ lần build trước |
| `ImageTagAlreadyExistsException` ở bước push | Re-run một commit đã build xong. ECR `IMMUTABLE` từ chối push lại | Không cần sửa gì — bước "Image đã tồn tại chưa" đã xử lý; nếu vẫn gặp thì bước đó bị bỏ qua |
| `run-task` nằm ở `PROVISIONING` rồi timeout | Không có container instance nào ACTIVE. Migration là task `bridge` trên EC2 launch type nên **bắt buộc** cần một host | Chạy `up.sh`. Bình thường `preflight` đã chặn trước, nên gặp lỗi này là dấu hiệu hạ tầng vừa bị hạ giữa lúc chạy |
| Migrator exit code ≠ 0 | Migration thật sự lỗi | **Đây là pipeline làm đúng việc.** App bản cũ vẫn đang phục vụ. Đọc log CloudWatch của task, in ngay trong output |
| `wait services-stable` timeout | Task mới không lên được: OOM, thiếu secret, image sai kiến trúc | `aws ecs describe-tasks` xem `stoppedReason`. Xem [terraform-runbook.md](terraform-runbook.md) mục sự cố |
| Warning "Không gọi được qua domain" nhưng job xanh | Container khoẻ, DNS/Cloudflare có vấn đề | Kiểm CNAME của ALB và chế độ proxy. **Không** rollback |

---

## 9. Những gì pipeline này CỐ Ý không làm

Liệt kê ra vì mỗi cái là một câu hỏi hội đồng có thể hỏi, và câu trả lời "chưa
làm" khác hẳn "cố ý không làm".

| Không làm | Lý do |
|---|---|
| **Không `terraform apply`** | Hạ tầng tốn tiền theo giờ. Pipeline tự apply = chi phí không có trần. |
| **Không tự bật hạ tầng** | Cùng lý do. Và nó được canh ở **tầng quyền**: IAM role của pipeline không có `autoscaling:SetDesiredCapacity`, không có `rds:StartDBInstance`. Sửa file workflow cũng không lách được. |
| **Không rollback migration** | Forward-only. Migration phải viết tương thích ngược (thêm column nullable → backfill → siết constraint ở lần sau) để app bản cũ không chết trong lúc deploy. |
| **Không zero-downtime** | Static host port + 1 instance nên không chạy 2 bản song song được. Downtime ~20–40s, có ý thức. Đường zero-downtime là dynamic port mapping + 2 instance, nhưng phải mở dải `32768-65535` cho `sg-web` và tốn thêm một instance — đánh đổi không xứng ở quy mô này. |
| **Không có test .NET** | Thật sự chưa có. Đây là **thiếu sót**, không phải quyết định. |

> Một quy tắc chỉ nằm trong comment thì chỉ tồn tại tới lần sửa file tiếp theo.
> Đó là lý do hai quy tắc đầu được canh bằng IAM, không bằng comment.

---

## 10. Tự tay làm lại (khi pipeline không dùng được)

Đôi khi cần deploy tay: đang debug, hoặc pipeline đỏ vì lý do ngoài code. Đường
dự phòng đầy đủ ở [terraform-runbook.md](terraform-runbook.md) mục *Deploy tay*.
Ý chính, để bạn thấy pipeline đang làm hộ mình những gì:

```bash
SHA=$(git rev-parse HEAD)

# 1. Đăng ký revision mới cho cả 3 family ở tag $SHA.
#    Service CHƯA đổi ở bước này (ignore_changes = [task_definition]).
sed -i '' "s|^image_tag = .*|image_tag = \"$SHA\"|" infra/tf/envs/prod/terraform.tfvars
terraform -chdir=infra/tf/envs/prod apply

# 2. Migration TRƯỚC. Kiểm image của revision — phải in ra ...migrator:$SHA
aws ecs describe-task-definition --task-definition hushstore-migrator \
  --query 'taskDefinition.containerDefinitions[0].image'

# 3. Chỉ khi exit code = 0 mới trỏ 2 service sang revision mới.
```

Bước 2 tồn tại vì đúng cái bẫy ở [§6.3](#63-cái-bẫy-ở-bước-④--đáng-đọc-kỹ): nếu
lệnh đó **không** in ra `$SHA` thì `apply` ở bước 1 chưa chạy, và migration bạn
sắp chạy là migration cũ.

---

## 11. Đọc tiếp

| Cần gì | Mở |
|---|---|
| Sơ đồ luồng CI/CD dạng hình | [diagrams/hushstore-aws-2026.drawio](diagrams/hushstore-aws-2026.drawio) trang 3 |
| Hiểu ECR, ECS, task definition là gì | [thiet-ke-he-thong-aws.md](thiet-ke-he-thong-aws.md) Phần II |
| Bật/tắt hạ tầng, xử lý sự cố | [terraform-runbook.md](terraform-runbook.md) |
| 10 role IAM và vì sao chia như vậy | [bao-mat-he-thong.md](bao-mat-he-thong.md) §3 Lớp 5 |
| Bản đồ code Terraform | [doc-code-terraform.md](doc-code-terraform.md) |
| File gốc, có comment giải thích từng quyết định | [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) |
