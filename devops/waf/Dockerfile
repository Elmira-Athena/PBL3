# Image WAF (OWASP ModSecurity + CRS trên nginx) chạy sidecar trước `api` và `web`.
# Build context = GỐC REPO (CI: context: ., file: devops/waf/Dockerfile) — đường dẫn COPY tính từ đó.
#
# Ghim tag CÓ NGÀY (bất biến theo bản dựng), không dùng tag rút gọn. Digest index đa kiến trúc
# để đối chiếu nếu tag bị đẩy đè:
#   sha256:873ce68a658d2e3b61484a8a24d215f7d15456e7ce5aec5e7114bf000d743365
# Nguồn: docs/evidence/2026-11-08-waf-ram-spike.md §1.
FROM owasp/modsecurity-crs:4.30.0-nginx-alpine-202610180210

# Vá CVE của gói Alpine trong image gốc. ECR scan 2026-11-09 trên image chưa vá: 13 CRITICAL /
# 30 HIGH, TẤT CẢ ở gói hệ điều hành (curl, perl, zlib, brotli, expat, musl, libx11), không
# ở ModSecurity hay CRS. Đánh đổi có ý thức: `apk upgrade` làm bản build phụ thuộc thời điểm
# build (kho Alpine trôi theo ngày) — chấp nhận, vì tag image của mình là git SHA bất biến
# trên ECR, và tag gốc vẫn ghim nên ModSecurity/CRS/nginx không đổi.
# Image gốc chạy USER nginx (uid 101); apk cần root, xong phải trả lại nginx.
USER root
RUN apk upgrade --no-cache
USER nginx

# Thay HẲN server block của image: bản gốc nối IP peer vào X-Forwarded-For
# ($proxy_add_x_forwarded_for) và ghi đè X-Forwarded-Proto bằng $scheme — cả hai làm `api`
# (ForwardLimit = 1) đọc sai IP/giao thức client. Xem comment trong template.
COPY devops/waf/default.conf.template /etc/nginx/templates/conf.d/default.conf.template

# Tên file bắt đầu "900" nên được nạp trước luật CRS (setup.conf: Include rules/*.conf).
COPY devops/waf/REQUEST-900-EXCLUSION-RULES-BEFORE-CRS.conf /etc/modsecurity.d/owasp-crs/rules/REQUEST-900-EXCLUSION-RULES-BEFORE-CRS.conf

# HEALTHCHECK của image gọi https://localhost:8443/healthz — template của mình không còn cổng
# 8443 lẫn /healthz nội bộ, nên nó luôn đỏ. ECS không đọc HEALTHCHECK của Dockerfile; health
# check thật phải đi QUA WAF tới upstream (khai trong task definition).
HEALTHCHECK NONE
