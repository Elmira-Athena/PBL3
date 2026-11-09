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
logs() { docker compose logs --no-color --no-log-prefix waf-api 2>/dev/null; }
# audit_has <bộ lọc jq trên .transaction>: chờ tối đa 5 giây tới khi audit log có bản ghi khớp.
audit_has() {
  for _ in 1 2 3 4 5; do
    logs | jq -Rre "fromjson? | .transaction | select($1) | true" >/dev/null 2>&1 && return 0
    sleep 1
  done
  return 1
}
# khong_lo <chuỗi>: "không lộ" nếu chuỗi vắng mặt trong MỌI log của waf-api (audit + access + error).
# Không dùng grep -q: nó thoát ở dòng khớp đầu, `docker compose logs` nhận SIGPIPE và pipefail
# biến pipeline thành "thất bại" ⇒ báo "không lộ" đúng lúc ĐANG lộ (đối chứng âm đã bắt được).
khong_lo() { if logs | grep -F -- "$1" >/dev/null; then echo lộ; else echo "không lộ"; fi; }
login_json() { jq -nc --arg e "$1" --arg p "$2" '{email:$e,password:$p}'; }
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

# Mô tả sản phẩm là HTML đã sanitize (ProductService.cs:131-132, 202-203) và CÓ <img src=https://…>:
# CRS 941160 khớp "<img src=" nếu không có exclusion 1005. Ca đối chứng mo_ta_script bên dưới
# phải vẫn 403 — exclusion chỉ gỡ 941160, 941100/941110… còn nguyên.
S3IMG='https://hushstore-public-assets.s3.ap-southeast-1.amazonaws.com/a.jpg'
PRODUCT_HTML=$(jq -nc --arg d "<h2>Giới thiệu</h2><p>Mô tả <b>CPU</b></p><ul><li>20 nhân</li></ul><a href=\"https://hushstore.io.vn/\">Xem thêm</a><img src=\"$S3IMG\" alt=\"cpu\">" \
  '{name:"Intel Core i7-14700K",manufacturerId:1,categoryId:1,status:1,shortDescription:$d,description:$d}')
expect mo_ta_html_co_anh 200 "$(code -X PUT "${J[@]}" --data-binary "$PRODUCT_HTML" "$API/api/products/1")"
expect mo_ta_html_co_anh_post 200 "$(code -X POST "${J[@]}" --data-binary "$PRODUCT_HTML" "$API/api/products")"

# Tên file upload có dấu ' hoặc ; — CRS 920120 chặn. API đổi tên file thành GUID (chỉ giữ đuôi),
# xem comment exclusion 1006.
head -c 2048 /dev/urandom > "$TMP/anh-nho.png"
codes=(); for n in "Card's RTX.png" "screenshot;1.png"; do
  # filename="…": curl hiểu ";" trong tên file chỉ khi nằm trong dấu nháy kép.
  codes+=("$(code -H 'Expect:' -F "file=@$TMP/anh-nho.png;type=image/png;filename=\"$n\"" \
    "$API/api/images/upload?folder=products")")
done
expect upload_ten_file_dau_nhay "200 200" "${codes[*]}"

# Mật khẩu "khó": mỗi cái từng khớp một nhóm luật CRS khác nhau (932 / 930 / 941 / 942).
HARD=('Abc;ls -la1' '../x' '<script>' '"; DROP')
codes=(); for p in "${HARD[@]}"; do
  codes+=("$(code "${J[@]}" --data-binary "$(login_json a@b.vn "$p")" "$API/api/auth/login")")
done
expect mat_khau_kho_login "200 200 200 200" "${codes[*]}"
codes=(); for p in "${HARD[@]}"; do
  codes+=("$(code "${J[@]}" --data-binary "$(jq -nc --arg p "$p" \
    '{fullName:"Nguyễn Văn A",email:"a@b.vn",phoneNumber:"0901234567",password:$p,confirmPassword:$p}')" \
    "$API/api/auth/register")")
done
expect mat_khau_kho_register "200 200 200 200" "${codes[*]}"
codes=(); for p in "${HARD[@]}"; do
  codes+=("$(code -X PUT "${J[@]}" --data-binary "$(jq -nc --arg p "$p" \
    '{currentPassword:$p,newPassword:$p,confirmPassword:$p}')" "$API/api/auth/change-password")")
done
expect mat_khau_kho_doi_mat_khau "200 200 200 200" "${codes[*]}"

# JSON gần 1 MB (không phải file) — dưới MODSEC_REQ_BODY_NOFILES_LIMIT=1048576 của waf-api.
{ printf '{"name":"'; head -c 999980 /dev/zero | tr '\0' a; printf '"}'; } > "$TMP/1mb.json"
expect json_gan_1mb 200 "$(code -H 'Expect:' "${J[@]}" --data-binary @"$TMP/1mb.json" "$API/api/products")"

# Phiếu nhập/xuất kho: mảng serial không giới hạn. 5000 phần tử = 5000 ARG, vượt trần mặc định
# 1000 của image (MODSEC_ARGUMENTS_LIMIT) — waf-api đặt 20000.
jq -nc '{serialNumbers: [range(1; 5001) | "SN-\(. | tostring | ("000000" + .)[-6:])"]}' > "$TMP/serials.json"
expect json_nhieu_serial 200 "$(code "${J[@]}" --data-binary @"$TMP/serials.json" "$API/api/import-receipts")"

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
# Mô tả chứa <script> vẫn phải bị chặn dù 941160 đã gỡ khỏi trường description (exclusion 1005).
expect mo_ta_script 403 "$(code -X PUT "${J[@]}" --data-binary \
  "$(jq -nc '{name:"X",manufacturerId:1,categoryId:1,description:"<p>a</p><script>alert(1)</script>"}')" \
  "$API/api/products/1")"
# Ngoài exclusion: cùng <img src=> ở trường KHÁC của cùng path, hoặc ở path khác, vẫn 403 (941160).
expect img_ngoai_pham_vi_1005 "403 403" "$(code -X PUT "${J[@]}" --data-binary \
  "$(jq -nc --arg d "<img src=\"$S3IMG\">" '{name:$d,manufacturerId:1,categoryId:1}')" "$API/api/products/1") \
$(code "${J[@]}" --data-binary "$(jq -nc --arg d "<img src=\"$S3IMG\">" '{comment:$d}')" "$API/api/reviews")"
expect scanner 403 "$(code -A 'sqlmap/1.7' "$API/")"

# Audit log JSON của ca sqli phải có trên stdout của waf-api, chứa rule 942xxx.
if audit_has '(.request.uri | test("search=%27")) and any(.messages[]?; .details.ruleId | startswith("942"))'
then expect log_json có có; else expect log_json có không; fi

# SQLi ở email vẫn bị chặn (1002 chỉ gỡ trường mật khẩu); mật khẩu đi kèm là một mật khẩu "khó"
# ⇒ transaction CÓ bản audit log, và mật khẩu không được nằm trong đó.
PW_LO='Abc;ls -la1'
expect email_sqli_login 403 "$(code "${J[@]}" --data-binary "$(login_json "a' OR 1=1--" "$PW_LO")" "$API/api/auth/login")"
if audit_has '.request.uri == "/api/auth/login" and .response.http_code == 403'
then expect audit_log_khong_lo_mat_khau "không lộ" "$(khong_lo "$PW_LO")"
else expect audit_log_khong_lo_mat_khau "không lộ" "không có bản audit log để kiểm"; fi

# Request bị chặn kèm Bearer token ⇒ token không được nằm trong log (audit lẫn access log).
TOKEN="tok-kiem-thu-$$-$RANDOM$RANDOM$RANDOM"
expect sqli_kem_token 403 "$(code -H "Authorization: Bearer $TOKEN" -G \
  --data-urlencode "search=' OR 1=1--" -d ca=token "$API/api/products")"
if audit_has '.request.uri | test("ca=token")'
then expect audit_log_khong_lo_token "không lộ" "$(khong_lo "$TOKEN")"
else expect audit_log_khong_lo_token "không lộ" "không có bản audit log để kiểm"; fi

{ printf '{"data":"'; head -c 9437184 /dev/zero | tr '\0' a; printf '"}'; } > "$TMP/9mb.json"
expect body_qua_8mb 413 "$(code -H 'Expect:' "${J[@]}" --data-binary @"$TMP/9mb.json" "$API/api/products")"

if ((${#FAILED[@]})); then
  echo "Ca hỏng (${#FAILED[@]}): ${FAILED[*]}"
  exit 1
fi
echo "Mọi ca đạt."
