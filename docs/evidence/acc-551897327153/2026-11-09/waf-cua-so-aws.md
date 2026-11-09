# WAF sidecar trên AWS thật — cửa sổ đo 2026-11-09

Account `551897327153`, `ap-southeast-1`. Image tag `4046bc0b949a477d8e0b07642bf43ccc661abc17`
(PR #43 + #44). EC2 **t3.small**. Cửa sổ 15:14 → 15:41 (giờ VN), chi phí theo `down.sh`: **$0.064**.
Mọi phép đo gọi thẳng vào ALB bằng `curl --resolve` vì record CNAME `alb` trên Cloudflare còn
trỏ ALB cũ (domain trả 530) — đúng cách các báo cáo trước đã đo.

## 1. Mọi request đi qua WAF (cấu trúc)

`describe-task-definition` của revision đang chạy:

| Task def | Container | portMappings | links |
|---|---|---|---|
| `hushstore-api:5/6` | `api` | **`[]`** | — |
| | `waf` | `8080 → host 8080` | `api` |
| `hushstore-web:5/6` | `web` | **`[]`** | — |
| | `waf` | `8080 → host 80` | `web` |

App không có cổng host nào; cổng host 80/8080 (thứ duy nhất sg-web mở cho ALB) thuộc về WAF.
`up.sh`: cả `tg-api` (`/health/ready`, chạm DB) và `tg-web` (`/healthz`) **healthy sau 1 phút** —
health check của ALB đi QUA WAF tới upstream.

## 2. `DetectionOnly` — luồng thật không chặn nhầm (15:21)

Luồng: trang web `/`, `/health/ready`, danh sách sản phẩm, tìm kiếm tiếng Việt có dấu, đăng nhập
admin, danh sách đơn hàng (Bearer) → **0 rule CRS khớp**. Payload tấn công (vẫn 200 vì chỉ ghi log):

| Payload | Rule khớp |
|---|---|
| `?search=' OR 1=1--` | 942100, 949110 |
| `?search=<script>alert(1)</script>` | 941100, 941110, 941160, 941390, 949110 |
| `User-Agent: sqlmap/1.7` | 913100, 949110 |

`client_ip` trong audit log = IP công khai thật của máy đo (không phải IP ALB) ⇒ `real_ip` đúng.
Ghi chú: `404 /_framework/blazor.boot.json` và `405 /api/categories` là phản hồi của app
(đường dẫn không tồn tại/sai method), audit log có dòng nhưng **không** rule nào.

Chưa đi tay trên trình duyệt (cần sửa DNS) các luồng ghi: sửa sản phẩm có HTML, upload ảnh,
giỏ hàng, checkout, phiếu nhập/xuất — chúng được phủ bởi `devops/waf/test/run.sh` (30/30) với
upstream giả, chưa với app thật.

## 3. X-Forwarded-For — app phân biệt đúng IP client

Rate limit đăng nhập (5 lần / 60 s / IP), email không tồn tại:

| Nguồn | IP | 7 lần đăng nhập sai |
|---|---|---|
| Chính EC2, ra internet qua NAT (SSM) | `47.130.228.142` | `400 400 400 400 400 429 429` |
| Máy đo, **ngay sau đó** | IP khác | `400 400` — **không** 429 |

Nếu app đọc nhầm IP của WAF/ALB thì hai nguồn chung một khoá và máy đo phải nhận 429.

## 4. RAM thật (t3.small, ngay sau khi boot, chưa tải)

`docker stats` qua SSM: waf-api **62.7 MiB**, waf-web **40.1 MiB**, api 149.1 MiB, web 3.4 MiB;
host dùng 502 / 1913 MB, swap 0. Số đo Docker local (arm64) trước đó: idle ~50 MiB/WAF — khớp.
Chưa đo dưới tải trên EC2.

## 5. `On` — chặn thật (15:29)

`waf_rule_engine = "On"` → `terraform plan` chỉ thay 2 task definition (`DetectionOnly → On`),
apply, `update-service` → `hushstore-api:6`, `hushstore-web:6`, ổn định 15:29:30, 2 TG healthy.

| Request | Kết quả |
|---|---|
| Luồng thật ở §2 | 200 (đăng nhập, đơn hàng admin vẫn chạy) |
| SQLi / XSS / sqlmap | **403 / 403 / 403** |
| 403 có `Access-Control-Allow-Origin`? | Không (giới hạn đã biết) |

Dòng audit log của ca SQLi (rút gọn): [`waf-audit-sqli-403.json`](waf-audit-sqli-403.json) —
`942100 SQL Injection Attack Detected via libinjection`, `949110 Inbound Anomaly Score Exceeded
(Total Score: 5)`, `http_code 403`.

## 6. Tắt — xác minh bằng AWS API (không tin mã thoát)

`down.sh` exit 0, rồi hỏi thẳng AWS: ALB 0 · NAT active 0 · EIP 0 · EC2 sống 0 · ECS service 0 ·
RDS `hushstore-db-tf` **stopped** (15:41:13) · replica 0.

## 7. ECR scan

Tag `4046bc0`: `hushstore-waf` **0 finding** (tag `90f20b2` trước khi vá: 13 CRITICAL / 30 HIGH,
toàn bộ gói Alpine của image gốc). Các image khác có từ trước, ngoài phạm vi WAF: web 1 CRITICAL;
api, migrator 10 HIGH; seeder 6 HIGH.

## 8. Bằng chứng gốc (log và lệnh nguyên văn)

| File | Nội dung |
|---|---|
| [`waf-01-cloudwatch-log-goc.txt`](waf-01-cloudwatch-log-goc.txt) | **Toàn bộ** log CloudWatch `/ecs/hushstore-waf` (stream `waf-api/*`, `waf-web/*`) trong cửa sổ 15:14→15:42: audit log JSON của ModSecurity + access log nginx, nguyên văn. Lọc `t9=` để thấy các request tấn công. Log group chỉ giữ 3 ngày nên đã xuất ra đây |
| [`waf-02-probe.sh`](waf-02-probe.sh) | Script lệnh tấn công + luồng thật đã chạy (mật khẩu admin chuyển sang biến `ADMIN_PASSWORD`) |
| [`waf-02-lenh-tan-cong-va-ket-qua.txt`](waf-02-lenh-tan-cong-va-ket-qua.txt) | Payload và output terminal của 2 lần chạy: `DetectionOnly` (15:21) và `On` (15:29) |
| [`waf-03-xff-rate-limit-va-ram.txt`](waf-03-xff-rate-limit-va-ram.txt) | Lệnh SSM và output nguyên văn: rate limit từ IP NAT, `docker stats`, `free -m`; lệnh và kết quả từ máy đo |
| [`waf-04-task-definition.txt`](waf-04-task-definition.txt) | `describe-task-definition` của `api:5/6`, `web:5/6`: `portMappings`, `links`, `dependsOn`, RAM, env của WAF |
| [`waf-05-bat-tat-va-xac-minh.txt`](waf-05-bat-tat-va-xac-minh.txt) | Log `up.sh`, plan/apply chuyển `On`, log `down.sh`, và lệnh AWS API xác minh đã tắt |
| [`waf-audit-sqli-403.json`](waf-audit-sqli-403.json) | Bản rút gọn của một dòng audit log (ca SQLi bị 403) để trình bày |

Tìm nhanh trong log gốc:
```bash
grep 't9=sqli' waf-01-cloudwatch-log-goc.txt        # audit + access log của 2 lần chạy: 200 (DetectionOnly) rồi 403 (On)
grep -o '"ruleId":"[0-9]*"' waf-01-cloudwatch-log-goc.txt | sort | uniq -c
```
