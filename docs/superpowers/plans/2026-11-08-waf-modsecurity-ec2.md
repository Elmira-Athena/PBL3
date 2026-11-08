# WAF ModSecurity + CRS (sidecar) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Đặt một container WAF (nginx + ModSecurity v3 + OWASP CRS 4) làm sidecar đứng trước container `api` và `web` trong task ECS, để mọi request người dùng phải qua WAF, payload SQLi/XSS bị 403 và có log JSON.

**Architecture:** Mỗi task (`hushstore-api`, `hushstore-web`) có thêm container `waf` giữ cổng host (8080 / 80) và gọi app qua `links`; container app **không còn `portMappings`**. Target group, SG, cổng giữ nguyên; chỉ `load_balancer.container_name` đổi sang `waf`. Một image `hushstore-waf` dùng cho cả hai, khác nhau bằng env.

**Tech Stack:** `owasp/modsecurity-crs:<ver>-nginx-alpine-<ngày>` (ghim), Terraform (AWS provider đang ghim trong repo), GitHub Actions, bash + curl + jq + Docker Compose cho kiểm thử local.

**Spec:** [`docs/superpowers/specs/2026-11-08-waf-modsecurity-ec2-design.md`](../specs/2026-11-08-waf-modsecurity-ec2-design.md) — đọc cùng file này.

## Global Constraints

- Không `terraform plan/apply` vào state prod trừ Task 8 (apply hẹp, có duyệt) và Task 9 (cửa sổ AWS, có duyệt). `terraform test` thì được chạy (dùng profile `hushstore`; test bị `skip` vì hết SSO = **chưa kiểm**, không phải đạt — chạy `aws sso login --profile hushstore` rồi chạy lại).
- Không đọc/in/commit `infra/tf/envs/prod/terraform.tfvars`.
- Không sửa C#. `src/API/Middleware/ProxyHeaders.cs` giữ `ForwardLimit = 1`.
- WAF chuyển **nguyên** `X-Forwarded-For` (`$http_x_forwarded_for`), `X-Forwarded-Proto` (`$http_x_forwarded_proto`), `Host` (`$host`). Cấm `$proxy_add_x_forwarded_for`.
- Trạng thái cuối: `MODSEC_RULE_ENGINE=On`, `BLOCKING_PARANOIA=1`, `ANOMALY_INBOUND=5`, `ANOMALY_OUTBOUND=4`, `NGINX_WORKER_PROCESSES=1`, `MODSEC_RESP_BODY_ACCESS=Off`, `MODSEC_AUDIT_LOG_FORMAT=JSON`, `MODSEC_AUDIT_LOG=/dev/stdout`, audit engine `RelevantOnly`.
- WAF của api: body tối đa **8 MB** (`client_max_body_size 8m`, `MODSEC_REQ_BODY_LIMIT=8388608`). Upload ảnh thật tối đa 5 MB.
- `deregistration_delay = 5` giữ nguyên. Image ghim tag có ngày; không kéo Docker Hub lúc chạy trên AWS.
- Exclusion CRS hẹp nhất có thể (`ctl:ruleRemoveTargetById` theo đường dẫn + tham số); cấm tắt cả nhóm rule cho toàn app.
- Comment/thông báo trong repo viết tiếng Việt như phần còn lại của repo.
- Mọi commit kết thúc bằng `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Không push, không merge (Task 8 do người điều phối làm sau khi chủ dự án duyệt).

## Review Focus

1. **Mật khẩu có ký tự đặc biệt khi đăng nhập** (`' -- ; " <`) — người dùng mong đăng nhập bình thường, không 403. Test: Task 2, ca `login_mat_khau_ky_tu_dac_biet`.
2. **Upload ảnh đúng 5 MB multipart** — mong 2xx từ upstream, không 413/403. Test: Task 2, ca `upload_5mb`.
3. **Health check ALB với Host là IP** (`Host: 10.0.1.23:8080`, không User-Agent của trình duyệt) — mong 200. Test: Task 2, ca `health_host_ip`.
4. **XFF có nhiều hop từ client giả mạo** (`X-Forwarded-For: 1.2.3.4, 198.51.100.10`) — mong upstream nhận **y nguyên** chuỗi đó (không thêm IP WAF). Test: Task 2, ca `xff_giu_nguyen`.
5. **File Blazor `_framework/*.wasm`, `*.dll`, `*.dat`, `blazor.boot.json`** — mong 200, không bị chặn vì phần mở rộng. Test: Task 2, ca `blazor_framework`.

---

## File Structure

| File | Trách nhiệm |
|---|---|
| `devops/waf/Dockerfile` (mới) | `FROM` image owasp ghim tag; chép template nginx + exclusion |
| `devops/waf/default.conf.template` (mới) | server block: proxy tới `${BACKEND}`, header proxy, body size |
| `devops/waf/REQUEST-900-EXCLUSION-RULES-BEFORE-CRS.conf` (mới) | exclusion hẹp theo log |
| `devops/waf/test/compose.yml` (mới) | `waf-api` + `waf-web` + upstream `echo` (nginx:alpine) cho kiểm thử local |
| `devops/waf/test/echo.conf` (mới) | upstream trả về `XFF|Proto|Host` và 200 cho mọi path |
| `devops/waf/test/run.sh` (mới) | bộ kiểm thử: corpus hợp lệ ⇒ 200, tấn công ⇒ 403, log JSON có rule ID |
| `infra/tf/modules/storage/{ecr.tf,outputs.tf,tests/storage.tftest.hcl}` | repo ECR `waf` |
| `infra/tf/envs/prod/main.tf` | truyền `ecr_waf_url`, `vpc_cidr`, biến WAF vào `module.ecs`; thêm ARN repo waf vào `module.cicd` |
| `infra/tf/envs/prod/variables.tf` | `waf_rule_engine`; (nếu Task 1 chốt) `instance_type = "t3.small"` |
| `infra/tf/modules/ecs/{variables.tf,taskdef.tf,cluster.tf,service.tf}` | container `waf`, log group, bỏ `portMappings` app, `container_name = "waf"` |
| `infra/tf/modules/ecs/tests/{taskdef,service}.tftest.hcl` | test bất biến mới + sửa test cũ |
| `.github/workflows/deploy.yml` | matrix `waf`; đăng ký revision đổi cả image `waf` |
| `infra/tf/scripts/lib.sh` | chốt `hs_image_tag_check` dừng khi thiếu image waf; giá EC2 nếu đổi loại máy |
| `docs/evidence/2026-11-08-waf-ram-spike.md`, `docs/evidence/2026-11-08-waf-local.md` (mới) | bằng chứng |

---

### Task 1: Spike đo RAM + soi image (throwaway)

**Files:**
- Create: `docs/evidence/2026-11-08-waf-ram-spike.md`
- Scratch (không commit): thư mục scratchpad của phiên

**Interfaces:**
- Produces: (a) tag image ghim `OWASP_TAG` (dạng `<crs-ver>-nginx-alpine-<YYYYMMDDHHMM>`); (b) số MiB đỉnh của **một** WAF ⇒ `waf_memory_reservation` và `waf_memory_hard`; (c) quyết định `instance_type` (`t3.micro` | `t3.small`); (d) ghi chú về image: đường dẫn template nginx, đường dẫn file exclusion, env nào điều khiển body size, cấu hình proxy mặc định có đặt XFF không, user/cổng lắng nghe. Ghi tất cả vào file evidence — Task 2 và 4 đọc từ đó.

- [ ] **Step 1:** Bật Docker Desktop. Kiểm: `docker version --format '{{.Server.Version}}'` in ra một số phiên bản.
- [ ] **Step 2:** Chọn tag `nginx-alpine` có ngày mới nhất trên Docker Hub `owasp/modsecurity-crs` (tag ≥ 2 tuần tuổi), `docker pull`. Ghi tag + digest.
- [ ] **Step 3:** Soi image: `docker run --rm --entrypoint sh <img> -c 'id; ls -R /etc/nginx/templates /etc/modsecurity.d | head -80; cat /etc/nginx/templates/conf.d/default.conf.template; env | sort'`. Ghi 5 mục (d) ở trên.
- [ ] **Step 4:** Chạy một WAF với env của Global Constraints (`MODSEC_RULE_ENGINE=On`, `BACKEND` trỏ một `nginx:alpine` trả 200), `docker stats --no-stream` lúc idle.
- [ ] **Step 5:** Bắn tải ~5 000 request (`xargs -P 20 curl`), trộn GET thường, payload SQLi/XSS, và 20 lần POST multipart 5 MB; lấy `docker stats` đỉnh (lấy mẫu mỗi giây). Ghi idle / đỉnh.
- [ ] **Step 6:** Tính phép cộng `memoryReservation`: api 384 + web 96 + migrator 256 + 2 × waf ≤ RAM ECS đăng ký trên t3.micro (ghi nguồn số liệu; nếu không chắc, coi là ~900 MiB và nói rõ là ước). Không vừa (hoặc dư < 10%) ⇒ `t3.small`. Ghi kết luận + số.
- [ ] **Step 7:** Commit file evidence.

```bash
git add docs/evidence/2026-11-08-waf-ram-spike.md
git commit -m "docs(evidence): đo RAM WAF ModSecurity+CRS bằng Docker local"
```

### Task 2: Image WAF + bộ kiểm thử local

**Files:**
- Create: `devops/waf/Dockerfile`, `devops/waf/default.conf.template`, `devops/waf/REQUEST-900-EXCLUSION-RULES-BEFORE-CRS.conf`, `devops/waf/test/compose.yml`, `devops/waf/test/echo.conf`, `devops/waf/test/run.sh`
- Create: `docs/evidence/2026-11-08-waf-local.md`

**Interfaces:**
- Consumes: `OWASP_TAG` và ghi chú image từ Task 1.
- Produces: image build từ `devops/waf/Dockerfile` với build context = **gốc repo** (giống CI). Env nó nhận (Task 4 dùng đúng tên này): `BACKEND`, `PORT=8080`, `MODSEC_RULE_ENGINE`, `MODSEC_REQ_BODY_LIMIT`, `CLIENT_MAX_BODY_SIZE` (ví dụ `8m`; nếu image đã có env tương đương ở Task 1 thì dùng tên đó và ghi lại), `SET_REAL_IP_FROM`, cùng các env ở Global Constraints. Lắng nghe cổng container **8080**. `run.sh` thoát `0` khi mọi ca đạt, `1` khi có ca hỏng, in tên ca hỏng.

- [ ] **Step 1: Viết `run.sh` + `compose.yml` + `echo.conf` trước (test đỏ).**
  `compose.yml`: `echo` (nginx:alpine, `echo.conf`), `waf-api` (`BACKEND=http://echo:80`, body 8 MB, cổng host 18080), `waf-web` (`BACKEND=http://echo:80`, cổng host 18081), cả hai build từ `devops/waf/Dockerfile`, `MODSEC_RULE_ENGINE=${RULE_ENGINE:-On}`.
  `echo.conf`: mọi path trả `200` với body `$http_x_forwarded_for|$http_x_forwarded_proto|$host`; `client_max_body_size 10m`.
  Các ca trong `run.sh` (mỗi ca: tên, lệnh curl, mã mong đợi):

  | Ca | Đích | Request | Mong đợi |
  |---|---|---|---|
  | `login_thuong` | api | `POST /api/auth/login` JSON `{"email":"a@b.vn","password":"Abc@12345"}` | 200 |
  | `login_mat_khau_ky_tu_dac_biet` | api | như trên, password `P@ss' OR "x"--;<>` | 200 |
  | `refresh_token` | api | `POST /api/auth/refresh-token` JSON có token base64url dài 88 ký tự | 200 |
  | `san_pham_tieng_viet` | api | `PUT /api/products/1` JSON, mô tả `Intel® Core™ i7-14700K — 20 nhân/28 luồng; "boost" 5.6GHz, giảm 10%` + `specifications` dạng object | 200 |
  | `tim_kiem` | api | `GET /api/products?search=card%20đồ%20hoạ%20rtx&pageSize=20` | 200 |
  | `upload_5mb` | api | `POST /api/images/upload?folder=products` multipart, file 5 MB `image/png` | 200 |
  | `health_host_ip` | api | `GET /health/ready`, header `Host: 10.0.1.23:8080`, `User-Agent: ELB-HealthChecker/2.0` | 200 |
  | `health_web` | web | `GET /healthz`, `Host: 10.0.1.23` | 200 |
  | `blazor_framework` | web | `GET /_framework/blazor.boot.json`, `/_framework/App.wasm`, `/_framework/App.dll`, `/_framework/icudt.dat` | 200 cả 4 |
  | `xff_giu_nguyen` | api | `GET /api/x` với `X-Forwarded-For: 1.2.3.4, 198.51.100.10`, `X-Forwarded-Proto: https`, `Host: api.hushstore.io.vn` | 200 **và** body == `1.2.3.4, 198.51.100.10|https|api.hushstore.io.vn` |
  | `sqli` | api | `GET /api/products?search=' OR 1=1--` | 403 |
  | `xss` | api | `POST /api/reviews` JSON `{"comment":"<script>alert(1)</script>"}` | 403 |
  | `path_traversal` | web | `GET /..%2f..%2fetc/passwd` | 403 (hoặc 400 từ nginx — chấp nhận cả hai, ghi lại) |
  | `scanner` | api | `GET /` với `User-Agent: sqlmap/1.7` | 403 |
  | `log_json` | — | sau ca `sqli`: `docker compose logs waf-api` có một dòng JSON parse được bằng `jq` chứa rule ID bắt đầu `942` | có |
  | `body_qua_8mb` | api | `POST` JSON 9 MB | 413 |

- [ ] **Step 2:** Chạy `devops/waf/test/run.sh` — mong **đỏ** (chưa có Dockerfile, build lỗi).
- [ ] **Step 3: Viết `Dockerfile` + `default.conf.template`.** `FROM owasp/modsecurity-crs:<OWASP_TAG>`; chép template đè lên đúng đường dẫn template của image (theo ghi chú Task 1); chép file exclusion vào đúng chỗ. Template: một `server` nghe `${PORT}`, `client_max_body_size ${CLIENT_MAX_BODY_SIZE}`, `location /` `proxy_pass ${BACKEND}` với 3 `proxy_set_header` ở Global Constraints, `real_ip_header X-Forwarded-For; set_real_ip_from ${SET_REAL_IP_FROM}; real_ip_recursive on;`. Không có `location /healthz` riêng — health check phải tới upstream.
- [ ] **Step 4:** Chạy `RULE_ENGINE=DetectionOnly devops/waf/test/run.sh` (ca 403 sẽ thành 200 — chấp nhận ở chế độ này), rồi đọc audit log: liệt kê mọi rule khớp ở các ca **hợp lệ**.
- [ ] **Step 5:** Viết exclusion cho đúng các rule đó vào `REQUEST-900-...conf`, mỗi exclusion kèm comment: ca nào, rule nào, vì sao hẹp. Mỗi exclusion giới hạn theo `REQUEST_FILENAME` + tên tham số (ví dụ `ARGS:json.password` cho `/api/auth/login`).
- [ ] **Step 6:** Chạy `devops/waf/test/run.sh` (mặc định `On`) — mong `0` ca hỏng.
- [ ] **Step 7: Đối chứng âm cho XFF.** Tạm đổi template sang `$proxy_add_x_forwarded_for`, chạy lại ⇒ ca `xff_giu_nguyen` phải **đỏ**. Hoàn tác, chạy lại ⇒ xanh.
- [ ] **Step 8:** Ghi `docs/evidence/2026-11-08-waf-local.md`: tag image, bảng ca + kết quả, rule khớp ở DetectionOnly, exclusion đã viết, kết quả đối chứng âm, trích một dòng audit log JSON của ca `sqli`.
- [ ] **Step 9:** Commit.

```bash
git add devops/waf docs/evidence/2026-11-08-waf-local.md
git commit -m "feat(waf): image ModSecurity+CRS và bộ kiểm thử local"
```

### Task 3: ECR repo `waf` + quyền đẩy image của CI

**Files:**
- Modify: `infra/tf/modules/storage/ecr.tf:2`, `infra/tf/modules/storage/outputs.tf`, `infra/tf/modules/storage/tests/storage.tftest.hcl:15-30`
- Modify: `infra/tf/envs/prod/main.tf:173-178` (`module.cicd.ecr_repository_arns`)

**Interfaces:**
- Produces: outputs `module.storage.ecr_waf_url` (string), `module.storage.ecr_waf_arn` (string).

- [ ] **Step 1:** Sửa test `co_dung_4_ecr_repository_va_deu_immutable` → đổi tên `co_dung_5_ecr_repository_va_deu_immutable`, `length(aws_ecr_repository.this) == 5`, thêm assert `contains(keys(aws_ecr_repository.this), "waf")`; cập nhật error_message liệt kê 5 repo.
- [ ] **Step 2:** `terraform -chdir=infra/tf/modules/storage test` — mong FAIL ở run vừa sửa.
- [ ] **Step 3:** Thêm `"waf"` vào `ecr_repos`; thêm 2 output; thêm `module.storage.ecr_waf_arn` vào danh sách của `module.cicd`.
- [ ] **Step 4:** `terraform -chdir=infra/tf/modules/storage test` và `terraform -chdir=infra/tf/modules/cicd test` — mong tất cả PASS, 0 skip. `terraform -chdir=infra/tf/envs/prod validate` — mong `Success`.
- [ ] **Step 5:** Commit `feat(tf): repo ECR hushstore-waf và quyền đẩy image của CI`.

### Task 4: Container `waf` sidecar trong task api/web

**Files:**
- Modify: `infra/tf/modules/ecs/variables.tf`, `taskdef.tf` (locals `log_config` dòng 11-50; container `api` ~dòng 88-145; container `web` ~dòng 166-186), `cluster.tf:209-228` (log group), `service.tf:79-83,152-156`
- Modify: `infra/tf/modules/ecs/tests/taskdef.tftest.hcl` (run `static_host_port_...` dòng ~71-93; block `variables`), `tests/service.tftest.hcl:121-149` và block `variables` của mọi file test trong `modules/ecs/tests/` cần biến mới
- Modify: `infra/tf/envs/prod/main.tf` (`module "ecs"` dòng 63-110), `infra/tf/envs/prod/variables.tf`

**Interfaces:**
- Consumes: `module.storage.ecr_waf_url` (Task 3); số RAM + `instance_type` (Task 1); tên env của image (Task 2).
- Produces: biến module ecs `ecr_waf_url` (string), `vpc_cidr` (string), `waf_rule_engine` (string, `On`|`DetectionOnly`, mặc định `"On"`, có `validation`), `waf_memory_reservation` (number), `waf_memory_hard` (number) — mặc định lấy từ Task 1. Biến prod `waf_rule_engine` cùng kiểu/mặc định. Container tên **`waf`** trong cả `hushstore-api` và `hushstore-web` (Task 5 đổi image theo tên này). Log group `/ecs/${var.project}-waf`, stream prefix `waf-api` / `waf-web`.

- [ ] **Step 1: Test mới trong `taskdef.tftest.hcl`** (thêm biến mới vào block `variables`):
  - `run "app_khong_mo_cong_host_de_khong_di_vong_waf"`: với `api` và `web`, container trùng tên app có `length(try(c.portMappings, [])) == 0`. Error message giải thích: có `containerPort` mà thiếu `hostPort` ở bridge mode là **cổng host động** — vẫn là cửa sau.
  - Sửa `run "static_host_port_dung_80_va_8080_..."`: cổng 80/8080 nay thuộc container `waf` (`hostPort == 80 && containerPort == 8080` cho web; `8080/8080` cho api).
  - `run "waf_dung_truoc_app"`: container `waf` có `links` chứa tên app, `essential = true`, `dependsOn` app với `condition = "START"`, env `BACKEND` == `http://api:8080` / `http://web:80`, `MODSEC_RULE_ENGINE == var.waf_rule_engine`, image kết thúc bằng `:${var.image_tag}`, log group là `/ecs/<project>-waf`.
  - `run "waf_api_nhan_upload_8mb"`: env body limit của waf api == `8388608` và `8m`.
  - `run "waf_rule_engine_sai_gia_tri_bi_tu_choi"`: `variables { waf_rule_engine = "Off" }`, `expect_failures = [var.waf_rule_engine]`.
- [ ] **Step 2: Sửa `service.tftest.hcl:121-149`:** `container_name == "waf"` cho cả 2 service; `container_port == 8080` cho cả 2 (cổng **container** của waf).
- [ ] **Step 3:** `terraform -chdir=infra/tf/modules/ecs test` — mong FAIL đúng ở các run trên.
- [ ] **Step 4:** Cài đặt: biến; log group `waf`; 2 entry `log_config`; container `waf` trong mỗi task def (memory theo Task 1, `stopTimeout` ≥ app); xoá `portMappings` của `api` và `web`; `container_name = "waf"`, `container_port = 8080` ở 2 `load_balancer`. Sửa comment liên quan ở `alb.tf:41-42`/`service.tf` nếu chúng nói "container api/web nhận traffic từ ALB". Nếu Task 1 chốt `t3.small`: đổi default `instance_type` ở `envs/prod/variables.tf:150` và description.
- [ ] **Step 5:** Nối dây ở `envs/prod/main.tf`: `ecr_waf_url = module.storage.ecr_waf_url`, `vpc_cidr = var.vpc_cidr`, `waf_rule_engine = var.waf_rule_engine`.
- [ ] **Step 6:** `terraform -chdir=infra/tf/modules/ecs test` — PASS hết, 0 skip; chạy cả `alb`, `security`, `network` test (không đổi, phải vẫn xanh); `terraform -chdir=infra/tf/envs/prod validate` — `Success`.
- [ ] **Step 7: Đối chứng âm:** tạm thêm lại `portMappings = [{ containerPort = 8080 }]` (không hostPort) cho `api` ⇒ `app_khong_mo_cong_host_...` phải FAIL. Hoàn tác.
- [ ] **Step 8:** Commit `feat(tf): WAF sidecar trước api/web, app không còn cổng host`.

### Task 5: CI — build image `waf`, đổi image `waf` khi đăng ký revision

**Files:**
- Modify: `.github/workflows/deploy.yml:102-122` (matrix), `:370-410` (bước "Đăng ký 3 revision")

**Interfaces:**
- Consumes: container `waf` trong `hushstore-api`/`hushstore-web` (Task 4); repo `hushstore-waf` (Task 3).

- [ ] **Step 1: Test jq trước.** Trong scratchpad, tạo `td-api.json` mẫu gồm 2 container (`api`, `waf`) và `td-migrator.json` 1 container; viết lệnh jq **mới** ra một file script tạm và chạy với `key=api` ⇒ assert cả `hushstore-api:SHA` lẫn `hushstore-waf:SHA` có trong output; `key=migrator` ⇒ output y nguyên trừ image migrator, **không** lỗi.
- [ ] **Step 2:** Sửa matrix: thêm `- image: waf`, `dockerfile: devops/waf/Dockerfile`, `build_args: ""`.
- [ ] **Step 3:** Sửa jq: ngoài `.name == $name`, container `.name == "waf"` nhận image `${REGISTRY}/hushstore-waf:${GITHUB_SHA}`. Với `api`/`web` thêm chốt `grep -q "hushstore-waf:${GITHUB_SHA}"` kèm `::error` tiếng Việt cùng khuôn chốt hiện có. Đổi tên bước nếu tên còn nói "3 revision" mà nội dung đã khác (vẫn là 3 revision — giữ).
- [ ] **Step 4:** Chạy lại lệnh jq của Step 1 bằng đúng chuỗi trong `deploy.yml` (copy nguyên) — PASS. `python3 -c 'import yaml,sys; yaml.safe_load(open(".github/workflows/deploy.yml"))'` không lỗi.
- [ ] **Step 5:** Commit `ci: build image waf và đổi image sidecar khi deploy`.

### Task 6: `up.sh` dừng khi thiếu image waf; giá EC2 nếu đổi loại máy

**Files:**
- Modify: `infra/tf/scripts/lib.sh:347-372` (`hs_image_tag_check`)
- Modify (chỉ khi Task 1 chốt `t3.small`): `infra/tf/scripts/lib.sh:68` (`HS_RATE_EC2`), comment giá ở `infra/tf/modules/costguard/lambda.tf:129`

- [ ] **Step 1:** Trong `hs_image_tag_check`, sau khi có `want`: `aws ecr describe-images --repository-name "${HS_PROJECT}-waf" --image-ids imageTag="$want" "${AWSQ[@]}"` thất bại ⇒ `hs_die` tiếng Việt: thiếu image waf ở tag này, ECS sẽ kẹt pull; hướng dẫn trỏ `image_tag` vào SHA build sau thay đổi WAF.
- [ ] **Step 2:** Nếu `t3.small`: `HS_RATE_EC2` = giá on-demand t3.small ap-southeast-1 **tra lại từ trang giá AWS hoặc Pricing API** (không lấy từ trí nhớ), ghi nguồn + ngày trong comment.
- [ ] **Step 3:** `bash -n infra/tf/scripts/lib.sh infra/tf/scripts/up.sh` — không lỗi; `shellcheck` nếu có.
- [ ] **Step 4:** Commit `fix(scripts): up.sh dừng khi image waf chưa có trên ECR`.

### Task 7: Tài liệu

**Files:**
- Modify: `docs/bao-mat-he-thong.md` (mục WAF), `docs/VIEC-CON-LAI.md` (việc còn lại), `CLAUDE.md` (một đoạn ngắn trong "Đang làm dở": WAF sidecar, `run.sh`, bẫy XFF, bẫy `containerPort` không `hostPort`)

- [ ] **Step 1:** Viết mục WAF: sơ đồ sidecar (như spec §4), WAF chặn gì / không chặn gì, cách bật/tắt `waf_rule_engine`, lệnh `devops/waf/test/run.sh`, đường tới evidence.
- [ ] **Step 2:** Commit `docs: WAF sidecar ModSecurity+CRS`.

### Task 8: ⛔ CỔNG DUYỆT — apply hẹp + merge `main` (người điều phối làm, không giao subagent)

- [ ] **Step 1:** `terraform -chdir=infra/tf/envs/prod plan -target=module.storage.aws_ecr_repository.this[\"waf\"] -target=module.storage.aws_ecr_lifecycle_policy.this[\"waf\"] -target=module.cicd -out=<scratchpad>/waf-narrow.plan`. Plan **chỉ** được chứa: tạo repo + lifecycle policy `waf`, sửa policy IAM của CI. Có bất kỳ thứ gì khác ⇒ dừng, báo chủ dự án. (Log group `waf` để `up.sh` tạo — nó nằm trong module ecs, không cần cho CI.)
- [ ] **Step 2:** Đưa tóm tắt plan cho chủ dự án. **Chỉ apply khi được duyệt rõ ràng.**
- [ ] **Step 3:** Sau apply: `aws ecr describe-repositories --repository-names hushstore-waf --profile hushstore` trả repo; hỏi chủ dự án trước khi push/merge vào `main`.
- [ ] **Step 4:** Sau khi CI chạy: `aws ecr describe-images` ở cả 5 repo có cùng tag SHA. Báo SHA cho chủ dự án để tự đặt `image_tag`.

### Task 9: ⛔ CỔNG DUYỆT — cửa sổ AWS: đo, demo, tắt

- [ ] **Step 1:** Được duyệt mới chạy `infra/tf/scripts/up.sh`. Ghi giờ bật.
- [ ] **Step 2:** Kiểm: `describe-target-health` cả 2 TG `healthy`; `curl https://api.hushstore.io.vn/api/products?search=%27%20OR%201%3D1--` ⇒ 403; luồng hợp lệ ⇒ 200; đăng nhập 2 tài khoản từ 2 IP khác nhau không khoá lẫn nhau; CloudWatch `/ecs/hushstore-waf` có dòng JSON rule `942…`; `describe-task-definition` cho thấy `api`/`web` không có `portMappings`; RAM thực trên máy (`docker stats` qua ECS Exec hoặc CloudWatch Container Insights nếu đã bật).
- [ ] **Step 3:** `infra/tf/scripts/down.sh`, rồi **xác minh tắt bằng AWS API** (`describe-load-balancers`, `describe-nat-gateways`, `describe-instances`, `describe-db-instances` ⇒ `stopped`) — không tin mã thoát.
- [ ] **Step 4:** Ghi `docs/evidence/acc-551897327153/<ngày>/` + báo cáo ngắn (chi phí, kết quả từng kiểm tra).
