# Thiết kế — WAF mã nguồn mở trên EC2 (ModSecurity v3 + OWASP CRS 4)

> Ngày 08/11/2026 · Nhánh `codex/remove-custom-nacl` · nền: commit rollback Cloudflare
> (commit ngay sau `630781e`). Nguồn: [`docs/ban-giao-waf-modsecurity-ec2.md`](../../ban-giao-waf-modsecurity-ec2.md).
> Mỗi quyết định ghi rõ **ai chốt**: *chủ dự án*, *thầy*, hay *lựa chọn của agent*.

## 1. Mục đích và lý do

Giảng viên góp ý dự án nên có WAF, và muốn một **WAF mã nguồn mở tự vận hành trên EC2** để
học được nhiều hơn — đó là lý do Cloudflare WAF đã bị rollback. Thầy **đã cho phép WAF chạy chung
máy với web/api** (*thầy*).

Thành công nghĩa là chứng minh được trước thầy ba điều:

1. **Mọi request đi qua WAF** — không có đường nào từ ALB tới thẳng app.
2. **Payload tấn công bị chặn**: SQLi/XSS trả **403** (*chủ dự án: trạng thái cuối là `On`*).
3. **Log JSON** của WAF ghi rule ID + điểm anomaly của request bị chặn, đọc được trên CloudWatch.

## 2. Mục tiêu · Không làm · Thất bại dù "chạy được"

**Mục tiêu**
- ModSecurity v3 + nginx + OWASP CRS 4.x, image gốc `owasp/modsecurity-crs:nginx` ghim tag có
  ngày (*agent phân tích, chủ dự án đồng ý*). Dự phòng nếu ModSecurity không dùng được:
  Coraza + Caddy (`ghcr.io/coreruleset/coraza-crs:caddy-alpine`; lưu ý coraza#1628 phình RAM với
  body lớn).
- Chắn **cả web lẫn api**.
- Trạng thái cuối `MODSEC_RULE_ENGINE=On`, `BLOCKING_PARANOIA=1`, `ANOMALY_INBOUND=5`,
  `ANOMALY_OUTBOUND=4` (*chủ dự án chốt `On`; các ngưỡng là mặc định CRS — lựa chọn của agent*).

**Không làm**
- Không dựng EC2 riêng cho WAF (*thầy*).
- Không sửa C# (`ProxyHeaders.cs` giữ `ForwardLimit = 1`) — WAF phải trong suốt với app.
- Không bot management, không rate limit ở WAF (rate limit đăng nhập đã có ở app), không paranoia > 1.
- BunkerWeb, SafeLine, open-appsec, NAXSI: đã loại (lý do ở file bàn giao §3).

**Thất bại dù kỹ thuật chạy được**
- Rate limit đăng nhập khoá **mọi người cùng lúc** vì app đọc nhầm IP của WAF (bẫy XFF, §5.2).
- Upload ảnh 5 MB trả 413, hoặc đăng nhập bằng mật khẩu có ký tự đặc biệt bị 403 (false positive).
- Còn đường đi vòng WAF (ALB tới được cổng của app).
- ALB health check xanh/đỏ sai sự thật, hoặc ECS giết nhầm container.

## 3. Ràng buộc

- EC2 **t3.micro 1 GB** (+2 GB swap từ `user_data`). Đã đặt chỗ `memoryReservation`: api 384,
  web 96, migrator 256 (chạy song song lúc deploy), seeder 128. RAM của ModSecurity+CRS **chưa
  từng đo**.
- **Nếu không vừa 1 GB ⇒ nâng lên t3.small, vẫn chắn cả web lẫn api** (*chủ dự án*). Biến
  `instance_type` đã có (`infra/tf/envs/prod/variables.tf:150`); kéo theo sửa giá
  `HS_RATE_EC2` ở `infra/tf/scripts/lib.sh:68` và comment giá ở `infra/tf/modules/costguard/lambda.tf:129`.
- Bridge network, host port **tĩnh**, 1 task/service/máy; `deregistration_delay = 5` bị test ghim — giữ.
- Upload ảnh tối đa **5 MB** (`src/API/Controllers/Admin/ImageController.cs:18`).
- Ràng buộc vận hành của file bàn giao §9 giữ nguyên: không `terraform plan/apply` vào prod khi
  chưa được duyệt; không đụng `terraform.tfvars`; bật hạ tầng xong phải tắt và **xác minh bằng
  AWS API**; commit kết thúc bằng `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## 4. Kiến trúc — WAF làm **sidecar** trong task của api và của web

> ⚠️ **Lựa chọn của agent, KHÁC với file bàn giao §4** (bản đó đề xuất một ECS service WAF
> riêng, DAEMON, gọi app qua `172.17.0.1`). Đổi vì một sự thật đọc được trong code, mục 4.1.

```
Internet → ALB :443 (định tuyến theo Host, default 403)
   ├─ tg-api (cổng host 8080) → [task api]  waf ──link──▶ api:8080  (api KHÔNG mở cổng host)
   └─ tg-web (cổng host 80)   → [task web]  waf ──link──▶ web:80    (web KHÔNG mở cổng host)
```

### 4.1 Vì sao không dùng service WAF riêng

- Container `api` và `web` **không có healthCheck tầng container** (`infra/tf/modules/ecs/taskdef.tf:143-144`
  — image aspnet không có curl). Sức khoẻ của chúng **chỉ** do target group ALB đo
  (`/health/ready` chạm DB, `/healthz`).
- Nếu target group trỏ sang service WAF riêng thì: (a) service api/web mất hẳn health check ⇒
  circuit breaker không còn thấy deploy hỏng; (b) api chết làm `/health/ready` qua WAF đỏ ⇒ ECS
  **giết task WAF** (vô tội), không đụng tới api — vòng lặp thay WAF mãi.
- Sidecar giữ nguyên ngữ nghĩa: api chết ⇒ task api (gồm cả WAF của nó) bị thay. Grace period,
  circuit breaker, `wait services-stable` của pipeline đều đúng như cũ.

### 4.2 Hệ quả có lợi

- **Chống đi vòng WAF bằng cấu trúc**, không chỉ bằng SG: container app **không còn `hostPort`**,
  nên trên máy không có cổng nào dẫn thẳng vào app. Cổng host 80/8080 nay thuộc về WAF.
- **SG không đổi** (vẫn 5 ingress, `sg.tftest.hcl` không sửa), **cổng target group không đổi**,
  chỉ `load_balancer.container_name` của 2 service đổi thành `waf`.
- WAF chỉ có **một upstream**, không phải định tuyến theo Host — health check của ALB (Host = IP
  máy) đi đúng chỗ mà không cần luật riêng.
- Không phụ thuộc `docker0`/`172.17.0.1`; WAF gọi app qua `links` của bridge mode.

### 4.3 Cái giá

- **Hai tiến trình WAF** trên mỗi máy (một ở task api, một ở task web) ⇒ RAM WAF gấp đôi.
  Chủ dự án đã chấp nhận t3.small nếu không vừa, nên đây là đánh đổi chấp nhận được.
- Image WAF đi cùng nhịp deploy của app (pipeline đổi image của **cả** container app lẫn `waf`
  trong một revision).

### 4.4 Image đầu tiên và lần bật stack đầu tiên (đã kiểm trong repo)

- Khi stack tắt, service api/web **không tồn tại** (`count = var.enable_alb ? 1 : 0`,
  `infra/tf/modules/ecs/service.tf:16,109`). `up.sh` bật `enable_alb` rồi apply ⇒ service được
  **tạo mới** từ task def do Terraform đăng ký, đã có container `waf`. Không có ca "service cũ
  trỏ `container_name = waf` vào revision chưa có `waf`".
- Task def Terraform dùng `${ecr_url}:${var.image_tag}`; `image_tag` nằm trong `terraform.tfvars`
  (chủ dự án tự đặt, `up.sh` cảnh báo khi có image mới hơn — `infra/tf/scripts/lib.sh:347`).
  Image `waf` dùng **chung `image_tag`** với api/web (lựa chọn của agent) ⇒ chỉ cần trỏ
  `image_tag` vào một SHA được build **sau** thay đổi này. Builder thêm vào `hs_image_tag_check`
  một chốt: dừng `up.sh` nếu repo `hushstore-waf` không có tag đó (thiếu thì ECS kẹt pull image).
- CI chỉ đẩy được lên ECR từ nhánh `main` (OIDC `sub_deploy`, `infra/tf/modules/cicd/main.tf:43`),
  và repo `hushstore-waf` chỉ có sau một lần apply. Thứ tự đưa image đầu tiên lên: xem §9.

## 5. Chi tiết đã chốt

### 5.1 Image WAF
- `devops/waf/Dockerfile`: `FROM owasp/modsecurity-crs:nginx-<tag có ngày>` + file cấu hình nginx
  (template) + file exclusion. Một image dùng cho cả hai task; khác nhau bằng env (`BACKEND`,
  giới hạn body).
- ECR repo thứ 5: thêm `"waf"` vào `ecr_repos` (`infra/tf/modules/storage/ecr.tf:2`). Build trong
  matrix của `.github/workflows/deploy.yml` như 4 image hiện có. Không kéo Docker Hub lúc chạy.
- Pipeline: bước "Đăng ký 3 revision" (`deploy.yml:376-394`) đổi image của container `waf` trong
  `hushstore-api` và `hushstore-web` sang `hushstore-waf:${GITHUB_SHA}`; chốt `grep -q` phải kiểm
  **cả hai** image đã đổi.

### 5.2 Header proxy (bẫy XFF — bắt buộc đúng)
- WAF chuyển **nguyên** `X-Forwarded-For` (`proxy_set_header X-Forwarded-For $http_x_forwarded_for;`),
  **không** dùng `$proxy_add_x_forwarded_for`. Chuyển nguyên `X-Forwarded-Proto` và `Host`.
  Lý do: app tin đúng một hop (`src/API/Middleware/ProxyHeaders.cs:15`); thêm IP WAF vào XFF ⇒
  mọi người dùng chung IP ⇒ rate limit đăng nhập khoá tất cả.
- Cấu hình proxy mặc định trong image owasp có thể tự đặt XFF — phải ghi đè, và **kiểm bằng
  `tools/ProxyHeadersProbe/`**.
- WAF dùng `real_ip` (`REAL_IP_HEADER=X-Forwarded-For`, `SET_REAL_IP_FROM=<vpc_cidr>`) chỉ để
  log của chính nó có IP thật.

### 5.3 Giới hạn body
- WAF của api: `client_max_body_size` và `MODSEC_REQ_BODY_LIMIT` ≥ 5 MB + overhead multipart
  (agent chọn **8 MB**). WAF của web: mặc định (web chỉ phục vụ file tĩnh).

### 5.4 Log
- `MODSEC_AUDIT_LOG_FORMAT=JSON`, `MODSEC_AUDIT_LOG=/dev/stdout`, access log ra stdout ⇒ awslogs.
- Log group riêng `/ecs/hushstore-waf` (cùng `log_retention_days`), stream prefix phân biệt
  `api`/`web` — để demo chỉ cần mở một chỗ. Kiểm quyền của execution role với log group mới.
- Audit engine `RelevantOnly` (chỉ ghi request bị gắn cờ) — tránh đầy CloudWatch.

### 5.5 Công tắc chế độ
- Biến Terraform `waf_rule_engine` (`On` | `DetectionOnly`), mặc định **`On`**, đưa vào env
  `MODSEC_RULE_ENGINE` của cả hai container `waf`. Lưu ý: service có
  `ignore_changes = [task_definition]`, nên đổi biến chỉ có hiệu lực từ lần deploy kế tiếp
  (pipeline lấy revision ACTIVE mới nhất làm khuôn).

### 5.6 False positive
- Lộ trình: `DetectionOnly` → chạy bộ request thật → đọc log → viết exclusion hẹp nhất có thể
  (`ctl:ruleRemoveTargetById` trong `REQUEST-900-EXCLUSION-RULES-BEFORE-CRS.conf`) → `On`.
- Vùng phải soi: trường mật khẩu và nội dung sản phẩm (HTML/ký tự đặc biệt) trong JSON của api;
  upload multipart; `_framework/*.dll|.wasm` của Blazor; health check (920350 — Host là IP).
- Exclusion **không** được tắt cả nhóm rule cho cả ứng dụng.

### 5.7 RAM
- `NGINX_WORKER_PROCESSES=1`, `MODSEC_RESP_BODY_ACCESS=Off`.
- `memory`/`memoryReservation` của container `waf` đặt **theo số đo** (§6 bước 1), không đoán.
- Phép cộng phải vừa: api + web + 2×waf + **migrator** (chạy cùng lúc khi deploy) ≤ RAM ECS
  đăng ký được trên máy. Không vừa ⇒ t3.small.

## 6. Thứ tự làm (lựa chọn của agent)

1. **Spike đo RAM local bằng Docker ($0)** — throwaway. Container WAF + CRS, 1 worker, idle và
   dưới tải (vài nghìn request lẫn payload tấn công, kèm một upload 5 MB). Ghi `docker stats`
   đỉnh. Chốt t3.micro hay t3.small từ số này. (Docker Desktop hiện đang tắt — phải bật.)
2. Image WAF + cấu hình + exclusion, chạy local sau api/web thật (`devops/docker/`), ở
   `DetectionOnly`, đi các luồng chính (đăng nhập, xem/sửa sản phẩm, upload ảnh, giỏ hàng,
   checkout) ⇒ viết exclusion ⇒ bật `On`, chạy lại, thêm payload SQLi/XSS phải ra 403.
   Kiểm XFF bằng `ProxyHeadersProbe`.
3. Terraform: ECR repo, log group, container `waf` trong 2 task def, bỏ `hostPort` của app,
   `container_name` trong 2 service, biến `waf_rule_engine`, (nếu cần) `instance_type`.
   `terraform test` phải xanh, có test mới cho bất biến chống đi vòng (§7).
4. CI: matrix thêm `waf`, bước đăng ký revision đổi image `waf`. Đưa image đầu tiên lên ECR theo §9.
5. **Chỉ khi chủ dự án duyệt**: bật stack trên AWS, đo, demo, tắt, xác minh tắt bằng AWS API.

## 7. Bằng chứng phải có

- **Test Terraform** (có ca đối chứng âm — thêm lại `hostPort` cho app thì test phải đỏ):
  container `api`/`web` không có `hostPort`; container `waf` giữ `hostPort` 8080/80;
  `load_balancer.container_name == "waf"` ở cả 2 service; `ecr_repos` có `waf`.
- **Local**: payload SQLi/XSS ⇒ 403 + một dòng JSON có rule ID; luồng chính ⇒ 0 chặn nhầm;
  upload 5 MB ⇒ thành công; `ProxyHeadersProbe` ⇒ app thấy đúng IP client.
- **AWS** (sau khi duyệt): như trên qua domain thật; health check 2 target group xanh; log trên
  CloudWatch; trạng thái tắt xác minh bằng `aws ... describe-*`.
- Ghi vào `docs/evidence/` theo quy ước repo.

## 8. Để builder tự quyết

- Tên file cấu hình, cách ghi đè template nginx của image owasp, tag ngày cụ thể để ghim.
- Công cụ bắn tải trong spike, bộ payload tấn công, danh sách exclusion cụ thể (theo log).
- `dependsOn`/`links` cụ thể giữa `waf` và app, `stopTimeout` của `waf`.
- Cách sắp lại test ECS hiện có đang giả định 1 container/task.
- Tài liệu cập nhật kèm (README, `docs/bao-mat-he-thong.md`, sơ đồ) — làm sau khi chạy được.
- Trailer commit: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` cho mọi commit,
  kể cả commit do subagent làm (theo file bàn giao §9).

## 9. Đường đưa image `waf` đầu tiên lên ECR

Chốt (*chủ dự án*): **apply hẹp rồi merge `main`**.

1. Chỉ sau khi local đạt (§6 bước 2) và `terraform test` xanh.
2. `terraform plan -target` **chỉ** gồm repo ECR `hushstore-waf` (+ lifecycle policy) và log
   group `/ecs/hushstore-waf` — $0, không chạm RDS/ALB/NAT/EC2. **Đưa plan cho chủ dự án xem**,
   được duyệt mới `apply`. Plan có bất kỳ resource nào khác ⇒ dừng.
3. Merge vào `main` ⇒ CI build đủ 5 image cùng một SHA (stack tắt thì job deploy tự bỏ qua).
4. Chủ dự án tự trỏ `image_tag` trong `terraform.tfvars` vào SHA đó; cửa sổ AWS (§6 bước 5)
   vẫn cần duyệt riêng.
