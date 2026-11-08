#!/usr/bin/env bash
# Bộ kiểm thử local cho image WAF (devops/waf/Dockerfile): dựng compose.yml, bắn các ca hợp lệ
# (không được chặn) và các ca tấn công (phải 403), rồi tự dọn stack.
#
#   devops/waf/test/run.sh                                  # MODSEC_RULE_ENGINE=On (trạng thái cuối)
#   RULE_ENGINE=DetectionOnly devops/waf/test/run.sh        # ca 403 sẽ ra 200 — chỉ để gom rule khớp
#   WAF_LOG_OUT=/tmp/waf.log devops/waf/test/run.sh         # lưu log hai WAF trước khi dọn
#
# Cần: docker compose, curl, jq. Thoát 0 khi mọi ca đạt, 1 khi có ca hỏng (in tên ca hỏng).
set -uo pipefail

cd "$(dirname "$0")"
API=http://localhost:18080
WEB=http://localhost:18081
TMP=$(mktemp -d)
FAILED=()

cleanup() {
  if [[ -n "${WAF_LOG_OUT:-}" ]]; then
    docker compose logs --no-color --no-log-prefix waf-api waf-web > "$WAF_LOG_OUT" 2>&1
  fi
  docker compose down -v --remove-orphans >/dev/null 2>&1
  rm -rf "$TMP"
}
trap cleanup EXIT

echo "Dựng stack (RULE_ENGINE=${RULE_ENGINE:-On})…"
if ! docker compose up -d --build --wait --wait-timeout 120; then
  echo "HỎNG: không dựng được stack"
  docker compose logs --no-color --tail 30
  exit 1
fi

# expect <tên ca> <giá trị mong đợi, nhiều lựa chọn ngăn bằng "/"> <giá trị nhận được>
expect() {
  if [[ "/$2/" == *"/$3/"* ]]; then
    printf 'ĐẠT  %-30s %s\n' "$1" "$3"
  else
    printf 'HỎNG %-30s mong [%s], nhận [%s]\n' "$1" "$2" "$3"
    FAILED+=("$1")
  fi
}
code() { curl -sS -o /dev/null -w '%{http_code}' "$@" 2>/dev/null; }
b64url() { base64 | tr '+/' '-_' | tr -d '=\n'; }
J=(-H 'Content-Type: application/json')

# ─── Ca hợp lệ: không được chặn ───────────────────────────────────────────────
expect login_thuong 200 "$(code "${J[@]}" \
  --data-binary '{"email":"a@b.vn","password":"Abc@12345"}' "$API/api/auth/login")"

expect login_mat_khau_ky_tu_dac_biet 200 "$(code "${J[@]}" \
  --data-binary "$(jq -nc --arg p "P@ss' OR \"x\"--;<>" '{email:"a@b.vn",password:$p}')" \
  "$API/api/auth/login")"

# Refresh token đúng dạng app sinh (AuthService.GenerateRefreshToken): Base64 CHUẨN của 64 byte
# = 88 ký tự, có "+", "/" và "==". Access token là JWT (base64url ba phần).
ACCESS="$(printf '{"alg":"HS256","typ":"JWT"}' | b64url).$(printf '%s' \
  '{"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier":"3f2b9c1e-7a4d-4e8b-9c2a-1d5e6f7a8b9c","email":"a@b.vn","http://schemas.microsoft.com/ws/2008/06/identity/claims/role":"Customer","exp":1762600000,"iss":"HushStore","aud":"HushStoreClient"}' \
  | b64url).AJTY164sNaYbdhScYvG11eSxNHSp4XvnMX1JQuz96a8"
REFRESH='4lMdUeIYQPlRnnekhdSuCCQPGkMLoflfg2DlmEmp/pPeLr867VVPo6j5A2qS1rx6cSwyLgG010LZ+FNr8G+m0A=='
expect refresh_token 200 "$(code "${J[@]}" \
  --data-binary "$(jq -nc --arg a "$ACCESS" --arg r "$REFRESH" '{accessToken:$a,refreshToken:$r}')" \
  "$API/api/auth/refresh-token")"

PRODUCT=$(jq -nc '{
  name: "Intel Core i7-14700K", manufacturerId: 1, categoryId: 1, status: 1,
  shortDescription: "CPU Intel thế hệ 14",
  description: "Intel® Core™ i7-14700K — 20 nhân/28 luồng; \"boost\" 5.6GHz, giảm 10%",
  specifications: {"Socket": "LGA1700", "Số nhân": "20", "Xung boost": "5.6GHz", "TDP": "125W"}
}')
expect san_pham_tieng_viet 200 "$(code -X PUT "${J[@]}" --data-binary "$PRODUCT" "$API/api/products/1")"

# Trình duyệt mã hoá UTF-8 thành %XX; --data-urlencode làm đúng như vậy.
expect tim_kiem 200 "$(code -G --data-urlencode 'search=card đồ hoạ rtx' -d pageSize=20 "$API/api/products")"

# -H 'Expect:': curl tự gửi "Expect: 100-continue" cho body lớn và CRS 920450 chặn header đó ở PL1.
# Trình duyệt/fetch không bao giờ gửi nó (xem docs/evidence/2026-11-08-waf-local.md).
head -c 5242880 /dev/urandom > "$TMP/anh-5mb.png"
expect upload_5mb 200 "$(code -H 'Expect:' -F "file=@$TMP/anh-5mb.png;type=image/png" \
  "$API/api/images/upload?folder=products")"

expect health_host_ip 200 "$(code -H 'Host: 10.0.1.23:8080' -A 'ELB-HealthChecker/2.0' "$API/health/ready")"
expect health_web 200 "$(code -H 'Host: 10.0.1.23' -A 'ELB-HealthChecker/2.0' "$WEB/healthz")"

codes=()
for f in blazor.boot.json App.wasm App.dll icudt.dat; do codes+=("$(code "$WEB/_framework/$f")"); done
expect blazor_framework "200 200 200 200" "${codes[*]}"

out=$(curl -sS -w '\n%{http_code}' -H 'X-Forwarded-For: 1.2.3.4, 198.51.100.10' \
  -H 'X-Forwarded-Proto: https' -H 'Host: api.hushstore.io.vn' "$API/api/x" 2>/dev/null)
expect xff_giu_nguyen "200 1.2.3.4, 198.51.100.10|https|api.hushstore.io.vn" "${out##*$'\n'} ${out%$'\n'*}"

# ─── Ca tấn công: phải bị chặn ────────────────────────────────────────────────
expect sqli 403 "$(code -G --data-urlencode "search=' OR 1=1--" "$API/api/products")"
expect xss 403 "$(code "${J[@]}" --data-binary '{"comment":"<script>alert(1)</script>"}' "$API/api/reviews")"
# 400 cũng chấp nhận: nginx có thể từ chối URI trước khi ModSecurity xem.
expect path_traversal "403/400" "$(code --path-as-is "$WEB/..%2f..%2fetc/passwd")"
expect scanner 403 "$(code -A 'sqlmap/1.7' "$API/")"

# Audit log JSON của ca sqli phải có trên stdout của waf-api, chứa rule 942xxx.
log_json=không
for _ in 1 2 3 4 5; do
  if docker compose logs --no-color --no-log-prefix waf-api 2>/dev/null \
    | jq -Rr 'fromjson? | select(.transaction.request.uri | test("search=%27")) | .transaction.messages[]?.details.ruleId' \
    | grep -q '^942'; then log_json=có; break; fi
  sleep 1
done
expect log_json có "$log_json"

{ printf '{"data":"'; head -c 9437184 /dev/zero | tr '\0' a; printf '"}'; } > "$TMP/9mb.json"
expect body_qua_8mb 413 "$(code -H 'Expect:' "${J[@]}" --data-binary @"$TMP/9mb.json" "$API/api/products")"

if ((${#FAILED[@]})); then
  echo "Ca hỏng (${#FAILED[@]}): ${FAILED[*]}"
  exit 1
fi
echo "Mọi ca đạt."
