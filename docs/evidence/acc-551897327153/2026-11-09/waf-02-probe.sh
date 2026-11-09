#!/usr/bin/env bash
# Task 9 probe: real read flows + login + attack payloads through ALB → WAF → app.
set -uo pipefail
IP="${1:?ALB IP}"
R=(--resolve "api.hushstore.io.vn:443:$IP" --resolve "hushstore.io.vn:443:$IP")
c() { curl -s -o /dev/null -w "%{http_code}" "${R[@]}" "$@"; }
API=https://api.hushstore.io.vn
echo "web /             $(c https://hushstore.io.vn/)"
echo "web boot.json     $(c https://hushstore.io.vn/_framework/blazor.boot.json)"
echo "api health/ready  $(c $API/health/ready)"
echo "api products      $(c "$API/api/products?pageNumber=1&pageSize=5")"
echo "api categories    $(c $API/api/categories)"
echo "api search VN     $(c "$API/api/products?search=card%20%C4%91%E1%BB%93%20ho%E1%BA%A1&pageSize=5")"
BODY=$(curl -s "${R[@]}" -H 'Content-Type: application/json' \
  -d '{"email":"admin@hushstore.com","password":"'"$ADMIN_PASSWORD"'"}' $API/api/auth/login)
TOK=$(printf '%s' "$BODY" | python3 -c 'import json,sys
try:
  d=json.load(sys.stdin); x=d.get("data") or {}
  print(x.get("accessToken") or x.get("token") or "")
except Exception: print("")')
echo "login             $([ -n "$TOK" ] && echo "OK (token ${#TOK} ký tự)" || echo "THẤT BẠI: ${BODY:0:120}")"
echo "admin orders      $(c -H "Authorization: Bearer $TOK" "$API/api/orders?pageNumber=1&pageSize=5")"
echo "SQLi              $(c "$API/api/products?search=%27%20OR%201%3D1--&t9=sqli")"
echo "XSS               $(c "$API/api/products?search=%3Cscript%3Ealert(1)%3C%2Fscript%3E&t9=xss")"
echo "scanner UA        $(c -A 'sqlmap/1.7' "$API/api/products?t9=scanner")"
