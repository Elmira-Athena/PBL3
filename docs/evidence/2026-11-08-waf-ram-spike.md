# Bằng chứng — đo RAM WAF ModSecurity + CRS (sidecar) bằng Docker local, và soi image

**Ngày đo:** 2026-11-08 · **Máy đo:** Mac arm64, Docker Desktop 29.8.0 (VM 7.7 GiB RAM) ·
**Chi phí:** $0 (không dùng AWS API, không dùng credential)

> Đây là spike bỏ đi (throwaway). Scratch (compose, backend, bộ bắn tải) nằm ở scratchpad của
> phiên, **không commit**. File này là đầu ra duy nhất; Task 2 và Task 4 đọc từ đây.

---

## 0. Kết luận (đọc phần này nếu chỉ có 1 phút)

| Mục | Giá trị |
|---|---|
| (a) `OWASP_TAG` ghim | `4.30.0-nginx-alpine-202610180210` (CRS 4.30.0, nginx 1.30.6, ModSecurity 3.0.17) |
| (a) digest (index đa kiến trúc) | `sha256:873ce68a658d2e3b61484a8a24d215f7d15456e7ce5aec5e7114bf000d743365` |
| (b) RAM **idle** một WAF | **~50 MiB** (cgroup `memory.current`; `docker stats` báo 40–42 MiB vì trừ page cache) |
| (b) RAM **đỉnh** một WAF, đúng tải theo kế hoạch | **178 MiB** (arm64 native); **179 MiB** (amd64 giả lập, lượt 1; ba lượt liên tiếp tới 211 MiB do `memory.peak` tích luỹ — §4.1) |
| (b) **chưa chốt** | **đo lại trên EC2 amd64 (Task 9).** Trên amd64 giả lập `memory.current` sau tải tăng 103 → 133 → 163 MiB qua 3 lượt, không tái hiện trên arm64 native (§6, rủi ro 2); chưa biết đó là rò rỉ hay giả lập |
| (b) đỉnh khi 20 upload 5 MB **đồng thời** (dao động mạnh giữa các lần) | 169–417 MiB (5 lần đo) |
| (b) đỉnh khi 20 upload 8 MB đồng thời (ở đúng trần `MODSEC_REQ_BODY_LIMIT`) | 549–594 MiB |
| (b) đề xuất | `waf_memory_reservation = 128`, `waf_memory_hard = 512` |
| (c) quyết định | **`t3.small`** — `t3.micro` không vừa (xem §5) |

Ba điều bất ngờ phải đọc trước khi viết task sau: §3.1 (nginx của WAF không giới hạn body, giới
hạn nằm ở ModSecurity và trả **403** chứ không phải 413), §3.2 (CRS chặn header `Expect`),
§3.3 (healthcheck mặc định của image dùng HTTPS cổng 8443).

---

## 1. Chọn tag (Bước 2)

Truy vấn Docker Hub API `owasp/modsecurity-crs`, lọc `nginx-alpine-20*`. Tag mới nhất có ngày:

| Tag | Đẩy lên | Tuổi tại 2026-11-08 |
|---|---|---|
| **`4.30.0-nginx-alpine-202610180210`** | 2026-10-18 | 21 ngày — chọn |
| `4.30.0-nginx-alpine-202610051210` | 2026-10-05 | 34 ngày |
| `4.29.0-nginx-alpine-202609301109` | 2026-09-30 | 39 ngày |

Tag có kiến trúc `amd64`, `arm64`, `386`. Digest của manifest `linux/amd64` (thứ EC2 sẽ kéo):
`sha256:375b8d29e48b2fa65c547c4ceb165c9329c9c85ee8806549883c6e49073287d1`. Khi ghim trong
Terraform nên dùng **tag**; digest index ở bảng trên là để đối chiếu nếu tag bị đẩy đè.
Kích thước image 159 MB nén (319 MB trên đĩa theo `docker images`).

Hai tag rút gọn (`4.30-nginx-alpine-…`, `4.30.0-nginx-alpine`) cũng tồn tại — **không** dùng,
vì chỉ tag có dấu thời gian mới bất biến theo bản dựng.

---

## 2. Soi image (Bước 3) — năm mục (d)

Lệnh: `docker run --rm --entrypoint sh <img> -c 'id; ls -R /etc/nginx/templates /etc/modsecurity.d; cat …default.conf.template; env | sort'`
(và đọc thêm `/docker-entrypoint.d/*`, `/etc/nginx/templates/**`).

### 2.1 Đường dẫn template nginx

Entry point chạy `envsubst` trên mọi `*.template` trong `/etc/nginx/templates/` rồi ghi ra
`/etc/nginx/` (`NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx`):

```
/etc/nginx/templates/
  nginx.conf.template                      -> /etc/nginx/nginx.conf
  conf.d/default.conf.template             -> /etc/nginx/conf.d/default.conf      (server block)
  conf.d/logging.conf.template             -> /etc/nginx/conf.d/logging.conf
  conf.d/modsecurity.conf.template         -> /etc/nginx/conf.d/modsecurity.conf  (modsecurity on; rules_file setup.conf)
  includes/proxy_backend.conf.template     -> /etc/nginx/includes/proxy_backend.conf
  includes/proxy_backend_ssl.conf.template, cors.conf.template, location_common.conf.template
  modsecurity.d/modsecurity.conf.template  -> /etc/modsecurity.d/modsecurity.conf (SecRule* từ env)
  modsecurity.d/modsecurity-override.conf.template (rỗng — chỗ cho directive không có env)
  modsecurity.d/setup.conf.template        -> /etc/modsecurity.d/setup.conf (thứ tự Include)
```

Muốn thay hẳn server block: **bind-mount đè** `/etc/nginx/templates/conf.d/default.conf.template`
(vẫn được `envsubst`). Muốn thêm file nginx mức `http {}`: mount thêm
`/etc/nginx/templates/conf.d/<tên>.conf.template` (`nginx.conf` có `include /etc/nginx/conf.d/*.conf`).
Muốn thêm directive ModSecurity không có env: mount đè
`/etc/nginx/templates/modsecurity.d/modsecurity-override.conf.template`.

### 2.2 Nội dung `default.conf.template` (nguyên văn, bản của tag này)

```nginx
# Nginx configuration for both HTTP and SSL

server_tokens ${SERVER_TOKENS};

# server_tokens only hides the version, never the product name, so the header is set
# here instead. Without -s this applies to every status code, and an empty
# SERVER_HEADER removes the header altogether.
more_set_headers 'Server: ${SERVER_HEADER}';

map $http_upgrade $connection_upgrade {
    default upgrade;
    '' close;
}

server {
    listen ${PORT} default_server;

    server_name ${SERVER_NAME};
    set $always_redirect ${NGINX_ALWAYS_TLS_REDIRECT};

    PROXY_SSL_CONFIG

    location / {
        client_max_body_size 0;

        if ($always_redirect = on) {
            return 301 https://$host$request_uri;
        }

        include includes/cors.conf;
        include includes/proxy_backend.conf;

        index index.html index.htm;
        root /usr/share/nginx/html;
    }

    include includes/location_common.conf;

}

server {
    listen ${SSL_PORT} ssl;
    HTTP3_CONFIG

    server_name ${SERVER_NAME};

    ssl_certificate ${SSL_CERT_FILE};
    ssl_certificate_key ${SSL_CERT_KEY_FILE};
    ssl_session_timeout 1d;
    ssl_session_cache shared:MozSSL:10m;
    ssl_session_tickets off;

    ssl_dhparam /etc/ssl/certs/dhparam-${SSL_DH_BITS}.pem;

    ssl_protocols ${SSL_PROTOCOLS};
    ssl_ciphers ${SSL_CIPHERS};
    ssl_prefer_server_ciphers ${SSL_PREFER_CIPHERS};

    ssl_stapling ${SSL_OCSP_STAPLING};
    ssl_stapling_verify ${SSL_OCSP_STAPLING};

    ssl_verify_client ${SSL_VERIFY};
    ssl_verify_depth ${SSL_VERIFY_DEPTH};

    PROXY_SSL_CONFIG

    location / {
        client_max_body_size 0;

        include includes/cors.conf;
        include includes/proxy_backend.conf;

        index index.html index.htm;
        root /usr/share/nginx/html;
    }

    include includes/location_common.conf;
}
```

Hệ quả: **một** `location /` proxy hết về `${BACKEND}`; nginx **không** có đường định tuyến
theo path. Hai WAF (một cho `api`, một cho `web`) vì vậy là hai container riêng với hai `BACKEND`.
Block cổng 8443 (`ssl`) luôn được bật, dùng chứng chỉ tự ký do `10-generate-certificate.sh`
sinh lúc khởi động.

### 2.3 Chỗ đặt `REQUEST-900-EXCLUSION-RULES-BEFORE-CRS.conf`

`setup.conf` (sinh từ template) có đúng một dòng nạp luật:
`Include /etc/modsecurity.d/owasp-crs/rules/*.conf`. Image đã có sẵn bản mẫu
`/etc/modsecurity.d/owasp-crs/rules/REQUEST-900-EXCLUSION-RULES-BEFORE-CRS.conf.example`
(đuôi `.example` nên **không** được nạp). Bind-mount file của mình thành:

```
/etc/modsecurity.d/owasp-crs/rules/REQUEST-900-EXCLUSION-RULES-BEFORE-CRS.conf
```

Thứ tự nạp theo tên file nên `900` đứng trước `901…` — đúng nghĩa "before CRS".
`RESPONSE-999-EXCLUSION-RULES-AFTER-CRS.conf.example` ở cùng thư mục theo cùng quy ước.
Comment cũ trong `setup.conf` nhắc `/opt/modsecurity/rules/{before,after}-crs/` — **thư mục đó
không tồn tại trong image này**, đừng dựa vào nó. Đường ra khỏi `/etc/modsecurity.d/` do
`90-copy-modsecurity-config.sh` (`touch` + `cp`) cần FS **ghi được** — không bật
`read_only_root_filesystem` mà chưa thử.

### 2.4 Biến env điều khiển giới hạn body

| Lớp | Biến | Mặc định | Ghi chú |
|---|---|---|---|
| ModSecurity | `MODSEC_REQ_BODY_LIMIT` → `SecRequestBodyLimit` | `13107200` (12.5 MiB) | **Đây là trần upload thực tế.** Đặt `8388608` (8 MiB) khi đo |
| ModSecurity | `MODSEC_REQ_BODY_NOFILES_LIMIT` → `SecRequestBodyNoFilesLimit` | `131072` (128 KiB) | trần phần không phải file |
| ModSecurity | `MODSEC_REQ_BODY_LIMIT_ACTION` | `Reject` | |
| nginx | **không có env** | `client_max_body_size 0` (không giới hạn) cứng trong `location /` | muốn đổi phải mount template (§2.1) |

Đã đo ở 8 MiB (`8388608` = 8 388 608 B): upload 1 / 5 / 8 MB (`8 000 000 B`) → `200`;
upload 9 MB → **`403`** (không phải 413) và không có thông điệp luật trong audit log.
Trần "8 MB" tính theo **byte của cả thân request** (gồm biên multipart), không theo số MB của file.

### 2.5 Cấu hình proxy mặc định và `X-Forwarded-For`

`includes/proxy_backend.conf.template` đặt:

```
proxy_set_header Host ${PROXY_HOST_HEADER};        # mặc định $host
proxy_set_header ${REAL_IP_PROXY_HEADER} $remote_addr;   # mặc định X-REAL-IP
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Port ${NGINX_X_FORWARDED_PORT};   # $server_port
proxy_set_header X-Forwarded-Proto ${NGINX_X_FORWARDED_PROTO}; # $scheme
proxy_http_version 1.1; proxy_buffering off; proxy_read_timeout 36000s; proxy_pass ${BACKEND};
set_real_ip_from ${SET_REAL_IP_FROM}  # mặc định 127.0.0.1
real_ip_header ${REAL_IP_HEADER}      # mặc định X-REAL-IP
```

**Có đặt XFF, bằng cách NỐI THÊM** (`$proxy_add_x_forwarded_for`), không ghi đè. Đo bằng backend
in header:

```
gửi  X-Forwarded-For: 1.2.3.4 -> backend thấy xff=[1.2.3.4, 192.168.65.1]
không gửi XFF               -> backend thấy xff=[192.168.65.1]
```

(`192.168.65.1` là IP của Docker Desktop VM — vai "peer trực tiếp" của WAF; trên AWS sẽ là IP của
ALB.)

**Mặc định của image VI PHẠM ràng buộc của kế hoạch.** Ràng buộc: WAF phải chuyển tiếp
`X-Forwarded-For` **NGUYÊN VĂN** (`proxy_set_header X-Forwarded-For $http_x_forwarded_for;`),
cấm `$proxy_add_x_forwarded_for`, và `ProxyHeaders.ForwardLimit` giữ 1 (C# không đổi). Với mặc định,
WAF nối thêm IP peer (= IP của ALB) vào cuối chuỗi, nên `api` (ForwardLimit 1 đọc phần tử cuối)
sẽ lấy **IP của ALB** làm IP client ⇒ mọi người dùng chung một IP, rate limiting theo IP hỏng.
**Không có env nào chuyển hành vi XFF** (đã kiểm: `proxy_backend.conf.template` gắn cứng
`$proxy_add_x_forwarded_for`, `env | sort` không có biến XFF). Vì vậy **Task 2 BẮT BUỘC ghi đè
template** — bind-mount đè `/etc/nginx/templates/includes/proxy_backend.conf.template` (hoặc
`conf.d/default.conf.template`) với dòng `proxy_set_header X-Forwarded-For $http_x_forwarded_for;`.

`X-Forwarded-Proto`: mặc định `${NGINX_X_FORWARDED_PROTO}` = `$scheme` = `http` (WAF nhận HTTP từ
ALB) nên giá trị của **ALB** bị ghi đè. **Đã kiểm biến này có thật**: nó xuất hiện trong `env`
của image (`NGINX_X_FORWARDED_PROTO=$scheme`) và được template tham chiếu (`${NGINX_X_FORWARDED_PROTO}`
đi qua `envsubst`). Có hai cách: đặt `NGINX_X_FORWARDED_PROTO='$http_x_forwarded_proto'` (nếu dùng
env, nhớ rằng `envsubst` chỉ thay `${...}` nên giá trị có `$` được giữ nguyên cho nginx — **chưa
chạy thử** cách này), hoặc chính bản ghi đè template ở trên. Nếu đã phải ghi đè template cho XFF thì
đặt luôn Proto ở đó. **Chưa đo** với ALB (spike không có ALB); phần Proto là suy ra từ template.

### 2.6 User và cổng

- `uid=101(nginx) gid=101(nginx)` — **không chạy root** (có `USER nginx`, `EXPOSE 8080/tcp`).
- Lắng nghe: **8080** (HTTP, biến `PORT`) và **8443** (TLS tự ký, `SSL_PORT`). Cả hai mở thật
  (`netstat`). Cổng >1024 nên không cần `NET_BIND_SERVICE`.
- PID file ở `/tmp/nginx.pid`; `HOME=/var/cache/nginx`; log mặc định `ACCESSLOG=/var/log/nginx/access.log`,
  `ERRORLOG=/var/log/nginx/error.log` (không phải `/dev/stdout` — đặt env nếu muốn đẩy lên CloudWatch).
- nginx 1.30.6 với module `ngx_http_modsecurity_module` và `headers_more`.

### 2.7 Danh sách đầy đủ biến env hỗ trợ (từ `env | sort` + templates)

Mặc định trong ngoặc, chỉ ghi khi quan trọng.

- **CRS (script `95-configure-rules.sh`, bản v4):** `PARANOIA` (đặt cả blocking + detection),
  `BLOCKING_PARANOIA` (1), `EXECUTING_PARANOIA`, `DETECTION_PARANOIA`, `ANOMALY_INBOUND` (5),
  `ANOMALY_OUTBOUND` (4) — mặc định lấy từ `ENV` của image, `ENFORCE_BODYPROC_URLENCODED`,
  `ALLOWED_METHODS`, `ALLOWED_REQUEST_CONTENT_TYPE`, `ALLOWED_REQUEST_CONTENT_TYPE_CHARSET`,
  `ALLOWED_HTTP_VERSIONS`, `RESTRICTED_EXTENSIONS`, `RESTRICTED_HEADERS_BASIC`,
  `RESTRICTED_HEADERS_EXTENDED`, `MAX_NUM_ARGS`, `ARG_NAME_LENGTH`, `ARG_LENGTH`, `TOTAL_ARG_LENGTH`,
  `MAX_FILE_SIZE`, `COMBINED_FILE_SIZES`, `VALIDATE_UTF8_ENCODING`, `REPORTING_LEVEL`,
  `SKIP_RESPONSE_ANALYSIS`, `MANUAL_MODE` (=1: bỏ qua toàn bộ script này), `CONFIG_FILE`.
  Biến chỉ có tác dụng khi **không rỗng**.
- **ModSecurity:** `MODSEC_RULE_ENGINE` (on), `MODSEC_REQ_BODY_ACCESS` (on), `MODSEC_REQ_BODY_LIMIT`,
  `MODSEC_REQ_BODY_NOFILES_LIMIT`, `MODSEC_REQ_BODY_LIMIT_ACTION` (Reject),
  `MODSEC_REQ_BODY_JSON_DEPTH_LIMIT` (512), `MODSEC_ARGUMENTS_LIMIT` (1000), `MODSEC_ARGUMENT_SEPARATOR`,
  `MODSEC_PCRE_MATCH_LIMIT`, `MODSEC_PCRE_MATCH_LIMIT_RECURSION`, `MODSEC_RESP_BODY_ACCESS` (**on**),
  `MODSEC_RESP_BODY_MIMETYPE`, `MODSEC_RESP_BODY_LIMIT` (1 MiB), `MODSEC_RESP_BODY_LIMIT_ACTION`,
  `MODSEC_TMP_DIR`, `MODSEC_DATA_DIR`, `MODSEC_UPLOAD_DIR`, `MODSEC_UPLOAD_FILE_MODE`,
  `MODSEC_UPLOAD_KEEP_FILES`, `MODSEC_TMP_SAVE_UPLOADED_FILES` (on), `MODSEC_AUDIT_ENGINE`
  (RelevantOnly), `MODSEC_AUDIT_LOG` , `MODSEC_AUDIT_LOG_FORMAT`, `MODSEC_AUDIT_LOG_PARTS` (ABIJDEFHZ),
  `MODSEC_AUDIT_LOG_TYPE` (Serial), `MODSEC_AUDIT_LOG_RELEVANT_STATUS`, `MODSEC_AUDIT_STORAGE_DIR`,
  `MODSEC_DEBUG_LOG`, `MODSEC_DEBUG_LOGLEVEL`, `MODSEC_COOKIE_FORMAT`, `MODSEC_UNICODE_MAPPING`,
  `MODSEC_STATUS_ENGINE`, `MODSEC_TAG`, `MODSEC_DEFAULT_PHASE1_ACTION`, `MODSEC_DEFAULT_PHASE2_ACTION`,
  `MODSEC_DISABLE_BACKEND_COMPRESSION`.
- **nginx / proxy:** `BACKEND` (`http://localhost:80`), `PORT` (8080), `SSL_PORT` (8443), `SERVER_NAME`,
  `SERVER_TOKENS`, `SERVER_HEADER`, `NGINX_WORKER_PROCESSES` (auto), `WORKER_CONNECTIONS` (1024),
  `CLIENT_BODY_TIMEOUT` (10s), `KEEPALIVE_TIMEOUT` (60s), `HTTP2`, `HTTP3`, `HTTP3_ALT_SVC_PORT`,
  `PROXY_TIMEOUT`, `PROXY_HOST_HEADER`, `PROXY_SSL` (+ `PROXY_SSL_CERT_FILE`, `_CERT_KEY_FILE`,
  `_CIPHERS`, `_PROTOCOLS`, `_VERIFY`, `_VERIFY_DEPTH`), `NGINX_ALWAYS_TLS_REDIRECT`,
  `NGINX_PORT_IN_REDIRECT`, `NGINX_X_FORWARDED_PORT`, `NGINX_X_FORWARDED_PROTO`, `REAL_IP_HEADER`,
  `REAL_IP_PROXY_HEADER`, `REAL_IP_RECURSIVE`, `SET_REAL_IP_FROM` (127.0.0.1), `RESOLVERS`,
  `RESOLVER_CONFIG`, `ACCESSLOG`, `ERRORLOG`, `METRICSLOG`, `LOGLEVEL` (warn), `METRICS_ALLOW_FROM`,
  `METRICS_DENY_FROM`, `NGINX_ENVSUBST_OUTPUT_DIR`.
- **TLS (cổng 8443):** `SSL_CERT_FILE`, `SSL_CERT_KEY_FILE`, `SSL_PROTOCOLS`, `SSL_CIPHERS`,
  `SSL_PREFER_CIPHERS`, `SSL_DH_BITS`, `SSL_OCSP_STAPLING`, `SSL_VERIFY`, `SSL_VERIFY_DEPTH`.
- **CORS (chỉ ảnh hưởng 403 do WAF trả):** `CORS_HEADER_403_ALLOW_ORIGIN` (`*`),
  `CORS_HEADER_403_ALLOW_METHODS`, `CORS_HEADER_403_CONTENT_TYPE`, `CORS_HEADER_403_MAX_AGE`,
  `CORS_HEADER_ACCESS_CONTROL_ALLOW_HEADERS`.

---

## 3. Ba điều bất ngờ khi chạy thật

### 3.1 Giới hạn body: nginx không giới hạn, ModSecurity giới hạn và trả 403

Lần đo đầu cho `413` ở upload 4 MB trở lên dù `MODSEC_REQ_BODY_LIMIT=8388608`. **Nguyên nhân là
backend giả của spike** (`nginx:alpine` mặc định `client_max_body_size 1m`; nginx kiểm Content-Length
ở giai đoạn tìm location, kể cả khi chỉ `return 200`), không phải WAF. Mất một vòng chẩn đoán
vì body 413 là trang của nginx — giống hệt trang của WAF. Sửa backend (`client_max_body_size 0`)
thì 1/5/8 MB → `200`, 9 MB → `403`. Bài học cho task sau: **khi thấy 413 sau WAF, kiểm backend trước.**
Với HushStore: nginx của `web` (static) và Kestrel của `api` có trần riêng, đừng đoán.

### 3.2 CRS chặn header `Expect` (luật 920450)

`curl -F` với file lớn tự gửi `Expect: 100-continue`. CRS luật `920450` ("HTTP header is restricted
by policy (/expect/)", +5 điểm) cộng với ngưỡng `ANOMALY_INBOUND=5` ⇒ **403 ngay ở PL1**. Trình
duyệt (`fetch` của Blazor WASM) không gửi header này nên người dùng thật không dính; nhưng **mọi
script kiểm thử / smoke test dùng `curl -F` phải thêm `-H 'Expect:'`**, nếu không sẽ thấy "WAF chặn
upload" mà thực ra là chặn công cụ.

### 3.3 Healthcheck mặc định dùng HTTPS cổng 8443

`HEALTHCHECK` của image chạy `/usr/local/bin/healthcheck` = `curl -sk https://localhost:${SSL_PORT}/healthz`.
Đo: cả `http://localhost:8080/healthz` và `https://localhost:8443/healthz` đều `200`, `curl` có
sẵn ở `/usr/bin/curl`. Với ECS nên khai healthCheck riêng: `curl -fs http://localhost:8080/healthz`.
Lưu ý `/healthz` do **chính WAF** trả (`return 200 "OK"`), không đi tới backend, nên nó chứng minh
nginx sống, **không** chứng minh `api`/`web` sống.

---

## 4. Đo RAM (Bước 4–5)

**Cấu hình đo:** `MODSEC_RULE_ENGINE=On`, `BLOCKING_PARANOIA=1`, `PARANOIA=1`, `ANOMALY_INBOUND=5`,
`ANOMALY_OUTBOUND=4`, `NGINX_WORKER_PROCESSES=1`, `MODSEC_RESP_BODY_ACCESS=Off`,
`MODSEC_AUDIT_LOG_FORMAT=JSON`, `MODSEC_AUDIT_LOG=/dev/stdout`, `MODSEC_AUDIT_ENGINE=RelevantOnly`,
`MODSEC_REQ_BODY_LIMIT=8388608`, `BACKEND=http://backend:80` (nginx:alpine trả 200).
Xác nhận trong `crs-setup.conf` đã sinh: `blocking_paranoia_level=1`, `detection_paranoia_level=1`,
`inbound_anomaly_score_threshold=5`, `outbound_anomaly_score_threshold=4`.

**Nền tảng:** đo **chính** trên `linux/arm64` (native, không giả lập); chạy lại 3 lượt trên
`linux/amd64` giả lập để đối chứng (EC2 là Intel). **arm64 chỉ là ước lượng sơ bộ cho amd64, chưa
phải bằng chứng tương đương:** đỉnh lượt đầu khớp (178 vs 179 MiB) nhưng đó là **một** mẫu amd64;
idle amd64 cao hơn (58 vs 46–51 MiB), `memory.current` sau tải cao hơn (103 vs 51 MiB), và amd64 tăng
dần qua các lượt (§4.1, §6 rủi ro 2) còn arm64 thì không. Số chốt phải đo lại trên amd64 thật.

**Cách đo:** `docker stats` chỉ cho điểm mẫu mỗi ~2 giây và **trừ page cache**, nên bỏ lỡ đỉnh
(mẫu cao nhất chỉ 68 MiB trong khi đỉnh thật 178 MiB). Số đỉnh lấy từ **cgroup v2
`memory.peak`** (`docker exec … cat /sys/fs/cgroup/memory.peak`) — đây là đỉnh thật, gồm cả page
cache của file tạm `client_temp`, và là thứ ECS dùng để giết container khi vượt `memory` hard.
Bộ bắn: 5000 request qua `xargs -P 20 curl` — 4500 GET `/api/products?page=N&q=laptop`, 250 payload
tấn công (SQLi `' OR '1'='1`, `UNION SELECT`, XSS `<script>`, SQLi trong JSON login), và 20 POST
multipart 5 MB (`-H 'Expect:'`), xáo trộn ngẫu nhiên.

### 4.1 Kết quả

| Phép đo | arm64 (native) | amd64 (giả lập) |
|---|---|---|
| Idle, ngay sau khởi động (`memory.current`) | 46–51 MiB | 58 MiB |
| Idle (`docker stats`) | 39.8–42.3 MiB | 48.5 MiB |
| Đỉnh, 1 lượt 5000 request | **178 MiB** (lượt 1); 136 MiB (lượt đo nháp trước khi sửa backend) | **179 MiB** |
| Sau lượt tải, `memory.current` | 51 MiB | 103 MiB |
| Mã HTTP | `4500×200`, `250×403`, `20×200` | `4500×200`, `250×403`, `20×200` |

**amd64 giả lập, ba lượt 5000 request liên tiếp trên cùng một container** (`memory.peak` là đỉnh
**tích luỹ** từ lúc khởi động, không đặt lại giữa các lượt):

| Lượt | `memory.current` sau tải | `memory.peak` tích luỹ |
|---|---|---|
| 1 | 103 MiB | 179 MiB |
| 2 | 133 MiB | 187 MiB |
| 3 | 163 MiB | 211 MiB |

Con số 211 MiB là cao nước của lượt 3 chứ không phải một lần chạy độc lập; vì baseline tăng theo
lượt, nó có thể phản ánh phần tăng chưa giải thích được (§6, rủi ro 2) chứ không phải tải thuần.

**Chặn đúng:** 250/250 payload tấn công bị `403`, **0/4500** GET thường bị chặn (không dương tính
giả ở PL1 với đường dẫn `/api/products`; đó không phải là kiểm toàn bộ ứng dụng, chỉ kiểm đúng mẫu
đã bắn). Mọi lượt có 20 upload 5 MB đều `200`.

### 4.2 Sức đo xấu hơn: nhiều upload đồng thời

RAM đỉnh phụ thuộc **số upload đồng thời**, không phụ thuộc số request nhỏ. Mỗi lượt khởi động lại
container để `memory.peak` sạch:

| Kịch bản | Đỉnh `memory.peak` |
|---|---|
| Chỉ request nhỏ (4500 GET + 250 tấn công), lặp 4 lượt | 46 → 51 MiB (tăng ~5 MiB, không rò) |
| 1 upload 5 MB | 97–99 MiB |
| 5 đồng thời × 5 MB | 245 MiB |
| 10 đồng thời × 5 MB | 154 MiB |
| 20 đồng thời × 5 MB | 169 / 340 / 353 / 407 / 417 MiB (5 lần đo) |
| 20 đồng thời × 8 MB, lặp 3 lượt | 549 / 594 / 594 MiB |

Phân rã một lần 20×5 MB (lấy mẫu `memory.stat` mỗi 0,1 giây): `anon` đỉnh **~110 MiB**, `file`
(page cache của file tạm, thu hồi được) đỉnh **~45 MiB** — tổng 155–170 MiB ở lần đó. Các lần
340–417 MiB cao hơn nhiều vì thời điểm trùng nhau của các request; **biến động giữa các lần lớn
(169 → 417), nên đừng tin một số đơn lẻ.** Sau mỗi đợt, `memory.current` trở về ~48 MiB — bộ nhớ
thân request được nhả, không giữ.

Trong lượt đo theo đúng kế hoạch, 20 upload rải ngẫu nhiên trong ~16 giây nên hiếm khi trùng, vì
thế đỉnh chỉ 178 MiB. Đó là tải "bình thường" của cửa hàng này (ảnh sản phẩm do admin tải, rất
ít khi đồng thời). Nó **không** là trần: một kẻ gửi liên tục 20 upload 8 MB song song đẩy WAF tới
~600 MiB (xem §6, rủi ro 1).

### 4.3 Đề xuất

- `waf_memory_reservation = 128` — ~2,5× idle, cho phép vài upload nối nhau mà không bị ECS
  coi là chiếm quá mức khi chia chỗ. (Reservation là mềm; chỉ dùng để xếp task lên host.)
- `waf_memory_hard = 512` — phủ đỉnh theo kế hoạch (178–211) 2,4×, và phủ cả 20 upload 5 MB đồng
  thời (đo cao nhất 417 + ~20%). **Không** phủ 20×8 MB (594). Chọn 512 thay vì 384 vì ở 384 hai trong năm lần 20×5 MB (407 và 417 MiB) sẽ bị OOM-kill, và
  OOM-kill một WAF là mất toàn bộ luồng của task đó (xem §6, rủi ro 1).

---

## 5. Phép cộng `memoryReservation` (Bước 6)

Số liệu của các container khác (do người giao việc cung cấp): `api` 384, `web` 96, `migrator` 256
(chạy **đồng thời** lúc deploy), `seeder` 128 (một lần, không đồng thời với deploy). Hai WAF mỗi host
(mỗi task một).

**Bộ nhớ ECS đăng ký trên `t3.micro` (ECS-optimized AL2023): KHÔNG tìm được con số có thẩm quyền**
(không gọi AWS API theo ràng buộc; tài liệu AWS không có bảng "memory đăng ký theo loại instance").
Dùng **~900 MiB làm ước lượng, có nhãn "ước"** (phần còn lại của 1 GiB đã bị OS + ECS agent + docker
ăn). `t3.micro` có 1024 MiB vật lý nên 900 là cận **trên** hợp lý, không thể cao hơn 1024.

| | waf_reservation = 128 | waf_reservation = 64 (ví dụ siết) |
|---|---|---|
| api + web + migrator | 384 + 96 + 256 = 736 | 736 |
| 2 × waf | 256 | 128 |
| **Tổng** | **992** | **864** |
| So với ~900 (ước) | **vượt 92 MiB (+10%)** | vừa, dư 36 MiB = **4%** |
| So với 1024 (RAM vật lý tuyệt đối) | dư 32 MiB = 3% — vô nghĩa vì OS còn ăn | dư 160 |

Kết luận không phụ thuộc vào độ chính xác của số 900: ở `128` tổng 992 **vượt** cả cận ước, và
chạm gần trần vật lý 1024; ở `64` (thấp hơn cả idle thực tế 50 MiB) dư chỉ 4% < 10% và còn không
chừa chỗ cho swap/page cache. Cả hai đều rơi vào điều kiện "không vừa hoặc dư < 10%" của kế hoạch.

**Quyết định: `instance_type = "t3.small"`** (2 GiB vật lý, ECS đăng ký ước ~1,8–1,9 GiB — cũng là
ước, **chưa đo**). Với 992 MiB tổng reservation, dư ~45%. Kèm lưu ý: tổng `memory` **hard** (api
chưa biết + web + 2×512 + migrator) có thể vượt RAM host; điều đó chấp nhận được vì hard chỉ là trần
cắt, nhưng đỉnh đồng thời của tất cả là điều chưa ai đo.

---

## 6. Rủi ro và việc chưa làm

1. **DoS bằng upload song song.** Hard 512 MiB không phủ 20×8 MB (594 MiB) ⇒ WAF bị OOM-kill, ECS
   dựng lại task, trong lúc đó luồng đó 502. Giảm rủi ro không cần tính năng mới: hạ
   `MODSEC_REQ_BODY_LIMIT` xuống đúng mức ảnh thật cần (ví dụ 6 MiB cho ảnh 5 MB + biên), hoặc đặt
   hạn mức kết nối ở ALB/WAF. Chưa quyết ở spike này.
2. **Tăng bộ nhớ khi giả lập amd64.** Ba lượt 5000 request liên tiếp trên amd64 giả lập:
   `memory.current` sau tải 103 → 133 → 163 MiB (worker RSS 151 MiB). **Không tái hiện trên arm64
   native** (46 → 49 MiB sau 4 lượt không-upload; 42 → 49 MiB sau 4 lượt upload). Khả năng cao là
   do giả lập, nhưng spike **không chứng minh** điều đó. Cần đo lại trên EC2 thật (amd64 native),
   nhìn `memory.current` sau nhiều lượt trước khi đóng số hard.
3. **Spike chưa đo gì về CPU**: t3.small là burstable (credit); giả lập amd64 chậm hơn nên số thời
   gian (16 s arm64, 20 s amd64 cho 5000 request) không đại diện cho EC2.
4. **Backend giả trả 200 ngay**, không đo thời gian giữ body khi backend chậm (`api` thật). Body
   được buffer trọn trước khi proxy, nên backend chậm ⇒ giữ RAM lâu hơn ⇒ đỉnh có thể cao hơn số
   đo ở đây.
5. **Chỉ PL1.** Số RAM ở PL cao hơn (nhiều luật hơn) không đo.
6. `X-Forwarded-Proto` sau ALB (§2.5) chưa đo — chỉ suy ra từ template; cách đặt qua env cũng chưa chạy thử.

## 7. Cách tái hiện

Toàn bộ trong scratchpad, không commit. Tóm tắt: `docker compose` hai service (`backend` =
`nginx:alpine` với `client_max_body_size 0; location / { return 200 }`; `waf` = image trên với env ở
§4) → bộ bắn `xargs -P 20 -L1 ./req.sh` → đọc `/sys/fs/cgroup/memory.peak` qua `docker exec`.
Chạy amd64 bằng `platform: linux/amd64` (giả lập). Đã dọn: `docker ps -a --filter name=wafspike` = 0.
