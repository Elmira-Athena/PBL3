#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# KỊCH BẢN THỬ WAF — cửa sổ AWS 2026-11-09 (Task 9 của kế hoạch WAF sidecar)
#
# MỤC ĐÍCH: chứng minh hai điều cùng lúc, bằng CÙNG một bộ request:
#   (1) Người dùng thật KHÔNG bị chặn nhầm  — nhóm "luồng thật" phải ra 200.
#   (2) Request tấn công BỊ CHẶN            — nhóm "tấn công" phải ra 403
#                                             (khi WAF ở chế độ On).
# Chạy y nguyên script này hai lần: lần 1 với waf_rule_engine = DetectionOnly
# (WAF chỉ ghi log, không chặn), lần 2 với On (WAF chặn thật). Kết quả của cả
# hai lần ở file waf-02-lenh-tan-cong-va-ket-qua.txt.
#
# ĐƯỜNG ĐI của mỗi request:
#   máy đo ──HTTPS──► ALB (443) ──► container waf (nginx + ModSecurity + CRS)
#                                     └──► container api / web (app thật)
# App không mở cổng host nào, nên KHÔNG có đường nào tới app mà né được WAF.
#
# CÁCH CHẠY:
#   ADMIN_PASSWORD='<mật khẩu admin demo>' bash waf-02-probe.sh <IP của ALB>
#   IP của ALB lấy bằng: dig +short <DNS name của ALB>
#
# VÌ SAO `curl --resolve`: lúc đo, record CNAME `alb` trên Cloudflare còn trỏ
# ALB của lần bật trước (ALB đổi DNS name mỗi lần dựng lại), nên domain trả
# 530. --resolve ép tên miền về đúng IP của ALB mới mà vẫn giữ đúng Host và
# SNI — tức request đi y hệt như từ trình duyệt, chỉ bỏ qua bước tra DNS.
#
# THẺ `t9=…`: mỗi request tấn công gắn thêm tham số t9=sqli / t9=xss /
# t9=scanner. App bỏ qua tham số này; nó chỉ để tìm đúng dòng trong log gốc
# (waf-01-cloudwatch-log-goc.txt): grep 't9=sqli' waf-01-cloudwatch-log-goc.txt
#
# Ghi chú: phần chú thích được thêm SAU khi chạy; lệnh bên dưới giữ nguyên như
# lúc chạy, chỉ thay mật khẩu admin bằng biến ADMIN_PASSWORD để không commit nó.
# ─────────────────────────────────────────────────────────────────────────────
set -uo pipefail
IP="${1:?ALB IP}"
R=(--resolve "api.hushstore.io.vn:443:$IP" --resolve "hushstore.io.vn:443:$IP")
# c URL… → chỉ in mã HTTP trả về (200, 403…), bỏ phần thân phản hồi.
c() { curl -s -o /dev/null -w "%{http_code}" "${R[@]}" "$@"; }
API=https://api.hushstore.io.vn

# ── NHÓM 1: LUỒNG THẬT — phải đi qua WAF bình thường (không bị chặn nhầm) ────
# Trang chủ Blazor (đi qua container waf của task web).
echo "web /             $(c https://hushstore.io.vn/)"
# File khởi động Blazor. Ra 404 là do app (.NET 10 không còn file này), không
# phải WAF — log WAF không ghi rule nào cho request này.
echo "web boot.json     $(c https://hushstore.io.vn/_framework/blazor.boot.json)"
# Đúng đường ALB health check dùng; endpoint này mở kết nối tới DB.
echo "api health/ready  $(c $API/health/ready)"
# Danh sách sản phẩm có phân trang.
echo "api products      $(c "$API/api/products?pageNumber=1&pageSize=5")"
# Ra 405 là do app (sai method/đường dẫn), không phải WAF — không rule nào khớp.
echo "api categories    $(c $API/api/categories)"
# Tìm kiếm tiếng Việt có dấu ("card đồ hoạ") — kiểm WAF không nhầm chữ UTF-8 là tấn công.
echo "api search VN     $(c "$API/api/products?search=card%20%C4%91%E1%BB%93%20ho%E1%BA%A1&pageSize=5")"
# Đăng nhập admin bằng JSON. Mật khẩu là trường WAF cố ý KHÔNG soi (để mật
# khẩu không bao giờ lọt vào log) — xem exclusion 1002 ở devops/waf/.
BODY=$(curl -s "${R[@]}" -H 'Content-Type: application/json' \
  -d '{"email":"admin@hushstore.com","password":"'"$ADMIN_PASSWORD"'"}' $API/api/auth/login)
TOK=$(printf '%s' "$BODY" | python3 -c 'import json,sys
try:
  d=json.load(sys.stdin); x=d.get("data") or {}
  print(x.get("accessToken") or x.get("token") or "")
except Exception: print("")')
# Chỉ in ĐỘ DÀI token, không in token — tránh lộ JWT vào output/evidence.
echo "login             $([ -n "$TOK" ] && echo "OK (token ${#TOK} ký tự)" || echo "THẤT BẠI: ${BODY:0:120}")"
# Gọi API cần quyền admin với token vừa lấy — kiểm WAF không làm hỏng header Authorization.
echo "admin orders      $(c -H "Authorization: Bearer $TOK" "$API/api/orders?pageNumber=1&pageSize=5")"

# ── NHÓM 2: TẤN CÔNG — DetectionOnly: 200 + ghi log; On: 403 ────────────────
# SQL injection kinh điển: search=' OR 1=1--  → CRS 942100 (libinjection).
echo "SQLi              $(c "$API/api/products?search=%27%20OR%201%3D1--&t9=sqli")"
# XSS: search=<script>alert(1)</script>  → CRS 941100/941110/941160/941390.
echo "XSS               $(c "$API/api/products?search=%3Cscript%3Ealert(1)%3C%2Fscript%3E&t9=xss")"
# Công cụ quét lỗ hổng tự nhận diện qua User-Agent: sqlmap → CRS 913100.
echo "scanner UA        $(c -A 'sqlmap/1.7' "$API/api/products?t9=scanner")"
