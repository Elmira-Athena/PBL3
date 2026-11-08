# Image WAF (OWASP ModSecurity + CRS trên nginx) chạy sidecar trước `api` và `web`.
# Build context = GỐC REPO (CI: context: ., file: devops/waf/Dockerfile) — đường dẫn COPY tính từ đó.
#
# Ghim tag CÓ NGÀY (bất biến theo bản dựng), không dùng tag rút gọn. Digest index đa kiến trúc
# để đối chiếu nếu tag bị đẩy đè:
#   sha256:873ce68a658d2e3b61484a8a24d215f7d15456e7ce5aec5e7114bf000d743365
# Nguồn: docs/evidence/2026-11-08-waf-ram-spike.md §1.
FROM owasp/modsecurity-crs:4.30.0-nginx-alpine-202610180210

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
