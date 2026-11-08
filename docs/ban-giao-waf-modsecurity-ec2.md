# Bàn giao — triển khai WAF mã nguồn mở trên EC2 (ModSecurity + OWASP CRS)

> Viết 08/11/2026 cho **một phiên agent mới không có ngữ cảnh**. Đọc hết file này trước
> khi chạm vào code. Đọc kèm: `CLAUDE.md`, [`rollback-cloudflare-waf-2026-11-08.md`](rollback-cloudflare-waf-2026-11-08.md),
> [`VIEC-CON-LAI.md`](VIEC-CON-LAI.md).

## 1. Vì sao làm việc này

Giảng viên góp ý dự án nên có WAF. Chủ dự án đã thử Cloudflare WAF rồi **rollback**, vì thầy
muốn một **WAF mã nguồn mở tự vận hành trên EC2** (để học được nhiều hơn). Thầy **đã đồng ý cho
WAF chạy chung máy với web/api** (không dựng EC2 riêng).

Mục tiêu đồ án: chứng minh được trước thầy rằng **mọi request đi qua WAF**, payload tấn công
(SQLi/XSS…) bị phát hiện/chặn, và có **log JSON** ghi rule ID + điểm anomaly để trình bày.

## 2. Trạng thái hiện tại (đã kiểm 08/11/2026)

- Nhánh: `codex/remove-custom-nacl`. Commit gần nhất: `630781e`.
- **54 thay đổi CHƯA COMMIT** = đợt rollback Cloudflare (xoá `infra/cloudflare/`, sửa alb/security/ecs
  `.tf` + test, envs, CI, `ProxyHeaders.cs`, `Program.cs`, `DbRateLimitAttribute.cs`,
  `tools/ProxyHeadersProbe/*`, nhiều docs + file mới `docs/rollback-cloudflare-waf-2026-11-08.md`).
  - `terraform test` đã chạy trên nền này: alb 11 · security 8 · ecs 35 · network 11 — ĐẠT.
  - Phần C# **CHƯA BUILD** — máy chưa có .NET SDK.
- Migration NACL → SG **đã apply lên AWS** (`136d7fb`, `630781e`). NACL mặc định allow-all.
- Hạ tầng AWS đang **tắt** (serving stack off). Account `551897327153`, profile `hushstore`.

## 3. Quyết định đã chốt

| Việc               | Chốt                                                                                                          | Ai chốt                           |
| ------------------ | ------------------------------------------------------------------------------------------------------------- | --------------------------------- |
| WAF                | **ModSecurity v3 + nginx + OWASP CRS 4.x**, image `owasp/modsecurity-crs:nginx` (ghim tag có ngày)            | agent phân tích, chủ dự án đồng ý |
| Phương án dự phòng | Coraza + Caddy (`ghcr.io/coreruleset/coraza-crs:caddy-alpine`) — lưu ý coraza#1628 phình RAM với body lớn     | agent                             |
| Loại bỏ            | BunkerWeb, SafeLine (nặng quá với 1 GB) · open-appsec (ML hộp đen) · NAXSI (không có image, bỏ dở từ 12/2024) | agent                             |
| Vị trí             | **Cùng EC2 với web/api**                                                                                      | **thầy**                          |

## 4. Kiến trúc đề xuất (lựa chọn của agent — chủ dự án chưa phản đối)

```
Internet → ALB (443, định tuyến theo Host) → WAF container (cổng riêng trên host)
         → web (host :80) / api (host :8080) trên CÙNG máy
```

- **Một container WAF chắn cả web lẫn api**, định tuyến theo `Host` bên trong nginx.
- Chạy như **ECS service riêng**. Gợi ý: `scheduling_strategy = "DAEMON"` (mỗi máy đúng một WAF,
  luôn đứng cạnh app). Hiện mỗi service có đúng 1 task/máy, cổng tĩnh, `network_mode = bridge`
  (`infra/tf/modules/ecs/variables.tf:187-196`, `service.tf`).
- WAF gọi app qua **IP host / docker0 gateway** (vd `172.17.0.1:80`, `:8080`). Traffic container→host
  cùng máy **không đi qua ENI nên không qua SG** — cần xác minh thực tế khi lên AWS.
- **Chống đi vòng WAF:** sửa `infra/tf/modules/security/main.tf` để ALB chỉ tới được **cổng WAF**;
  bỏ rule `web-http` (80) và `web-api` (8080) từ `sg-alb`, cùng 2 egress tương ứng của `sg-alb`.
  Cập nhật `tests/sg.tftest.hcl` (đang ghim `exact_ingress_chain` `length == 5`).
- **Target group:** `infra/tf/modules/alb/alb.tf:43` (`web`) và `:74` (`api`) trỏ sang cổng WAF
  (có thể 2 cổng WAF riêng để giữ 2 health check khác nhau: `/healthz` và `/health/ready`).
  Giữ default action 403 cho Host lạ (`alb.tf:158-166`).
- **Image:** build image mỏng `FROM owasp/modsecurity-crs:nginx-<tag>` + file exclusion + cấu hình,
  đẩy lên **ECR repo thứ 5** (`infra/tf/modules/storage/ecr.tf:2` — thêm `"waf"` vào `ecr_repos`),
  build qua CI như 4 image hiện có (`.github/workflows/deploy.yml`). Không kéo thẳng Docker Hub lúc chạy.

## 5. BẪY ĐÃ BIẾT — đọc kỹ

1. **IP người dùng / rate limit đăng nhập.** `src/API/Middleware/ProxyHeaders.cs` đặt
   `ForwardLimit = 1`, `KnownProxies.Clear()` — app tin **đúng một hop** (ALB). Nếu WAF dùng
   `$proxy_add_x_forwarded_for` (như `devops/nginx/nginx.multi.conf:37`) thì XFF có thêm IP của WAF,
   app đọc nhầm → **mọi người dùng chung một IP → rate limit login khoá tất cả cùng lúc**.
   ⇒ WAF phải **chuyển nguyên** XFF: `proxy_set_header X-Forwarded-For $http_x_forwarded_for;`
   và chuyển `X-Forwarded-Proto`, `Host`. **Không sửa C#.** WAF tự dùng `real_ip` (`REAL_IP_HEADER`,
   `SET_REAL_IP_FROM` = CIDR VPC, `REAL_IP_RECURSIVE`) chỉ để log của nó có IP thật.
   Kiểm bằng `tools/ProxyHeadersProbe/`.
2. **RAM chưa từng đo.** t3.micro 1 GB. Đã đặt chỗ: api 384/512, web 96/192, migrator 256/512 và
   seeder 128/256 (chạy một lần). Không có nguồn đáng tin cho RAM của ModSecurity+CRS ⇒ **phải đo**.
   `NGINX_WORKER_PROCESSES=1`, `MODSEC_RESP_BODY_ACCESS=Off`.
3. **Giới hạn body:** upload ảnh tối đa **5 MB** (`src/API/Controllers/Admin/ImageController.cs:18`).
   `client_max_body_size` của nginx và `MODSEC_REQ_BODY_LIMIT` phải ≥ 5 MB (chừa overhead multipart),
   nếu không upload ảnh sẽ 413.
4. **Health check qua WAF:** `/healthz` (web) và `/health/ready` (api, chạm DB). ALB health check có
   thể dính rule CRS (vd 920350 — Host là IP). Tôi đoán nó chỉ là warning, dưới ngưỡng 5 ở PL1 —
   **chưa kiểm**, phải xem log DetectionOnly.
5. **`deregistration_delay = 5`** đang bị test ghim — giữ nguyên.
6. **False positive của CRS** với Blazor WASM (file `.dll`/`.wasm`, `_framework/`) và JSON API.
   Viết exclusion ở `REQUEST-900-EXCLUSION-RULES-BEFORE-CRS.conf` (`ctl:ruleRemoveTargetById`), hẹp nhất có thể.

## 6. Mặc định agent tự chọn (ghi vào design doc như "lựa chọn của agent")

- Lộ trình: `MODSEC_RULE_ENGINE=DetectionOnly` → đọc log, viết exclusion → `On`, `BLOCKING_PARANOIA=1`,
  `ANOMALY_INBOUND=5`, `ANOMALY_OUTBOUND=4`.
- Log: `MODSEC_AUDIT_LOG_FORMAT=JSON`, `MODSEC_AUDIT_LOG=/dev/stdout`, `ACCESSLOG=/dev/stdout` → awslogs/CloudWatch.
- Đặt `memory` (hard limit) cho container WAF sau khi đo.
- Thứ tự: **đo RAM local bằng Docker trước ($0)** → rồi mới lên AWS.

## 7. CÂU HỎI CÒN MỞ — hỏi chủ dự án trước khi viết design doc

1. **(Quan trọng nhất, chưa trả lời)** Nếu đo xong WAF không vừa 1 GB: chấp nhận nâng lên
   **t3.small** (tiền EC2 ~gấp đôi khi bật stack), hay bắt buộc ở lại t3.micro và cắt phạm vi
   (vd chỉ chắn `api`)?
2. Commit đợt rollback Cloudflare thành một commit riêng làm nền sạch trước? Cài .NET SDK để build
   phần C# của rollback (và mở khoá việc NuGet trong `VIEC-CON-LAI.md`)?
3. Trạng thái cuối để chấm: bật chặn (`On`) hay chỉ `DetectionOnly`? Thầy muốn xem demo gì?

## 8. Quy trình phiên mới phải theo

1. Dùng skill `superpowers:brainstorming` — giai đoạn hỏi đã gần xong; chỉ hỏi §7.
2. Viết design doc: `docs/superpowers/specs/2026-11-08-waf-modsecurity-ec2-design.md` từ file này.
3. Gửi một subagent (`model: "sonnet"`) làm builder-check, gom câu hỏi "của chủ dự án" một lượt.
4. **Không build gì trước khi chủ dự án duyệt design doc.** Duyệt xong mới gọi `superpowers:writing-plans`.
5. Bước triển khai đầu tiên sau kế hoạch: spike đo RAM local bằng Docker.

## 9. Ràng buộc vận hành (không được vi phạm)

- **Không** `terraform plan/apply` vào state prod khi chưa được duyệt (state chứa mật khẩu master RDS).
- **Không** commit hay in ra `infra/tf/envs/prod/terraform.tfvars` (gitignored, có `my_ip` và email bên thứ ba).
- **Không** bật `enable_read_replica` qua đêm. Bật hạ tầng xong phải tắt, và **xác minh tắt bằng AWS API**,
  không tin mã thoát (`up.sh` từng chết giữa chừng mà vẫn exit 0).
- Chi phí: ~$0.25/giờ khi bật đủ stack; cost guard chỉ chạy 1 lần/ngày (`cron(0 0 * * ? *)`).
- Mọi thông báo lỗi cho người dùng bằng tiếng Việt; luật đầy đủ ở `CLAUDE.md`.
- Commit kết thúc bằng `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## 10. Câu mở phiên gợi ý

> Đọc `docs/ban-giao-waf-modsecurity-ec2.md`. Câu trả lời của tôi cho §7: … Viết design doc theo §8.
