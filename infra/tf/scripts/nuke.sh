#!/usr/bin/env bash
# HushStore — XOÁ TOÀN BỘ hạ tầng (terraform destroy).
#
# Đây KHÔNG phải cách tiết kiệm chi phí hằng ngày — dùng down.sh cho việc đó.
# Script này chỉ dùng khi thật sự muốn dẹp hẳn dự án.
#
# Dùng:  bash infra/tf/scripts/nuke.sh
#
# Trước khi destroy, script in ra đúng những thứ KHÔNG lấy lại được và bắt gõ
# một câu xác nhận. Không có cờ nào để bỏ qua bước đó.

# shellcheck source=lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

case "${1:-}" in -h|--help) sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;; esac

hs_tf_check
hs_sso_check

hs_head "ĐANG TÍNH XEM SẼ XOÁ NHỮNG GÌ"
mkdir -p "${HS_LOCAL_DIR}/logs"
PLAN_LOG="${HS_LOCAL_DIR}/logs/plan-destroy.log"
set +e
terraform -chdir="$HS_TF_DIR" plan -destroy -input=false -lock-timeout=5m -no-color >"$PLAN_LOG" 2>&1
prc=$?
set -e
[ "$prc" = "0" ] || { tail -20 "$PLAN_LOG" >&2; hs_die "plan -destroy thất bại — xem ${PLAN_LOG}"; }
grep -E '^Plan:' "$PLAN_LOG" | tail -1 | sed 's/^/  /'

# ── Những thứ không lấy lại được ────────────────────────────────
hs_head "${C_RED}KHÔNG LẤY LẠI ĐƯỢC${C_RESET}"

# skip_final_snapshot mặc định true, nên destroy xoá DB KHÔNG để lại snapshot.
snaps="$(aws rds describe-db-snapshots --db-instance-identifier "$HS_DB" \
  --query 'length(DBSnapshots)' --output text "${AWSQT[@]}" 2>/dev/null || echo '?')"
echo "  ${C_RED}RDS ${HS_DB}${C_RESET} — skip_final_snapshot = true, destroy KHÔNG tạo snapshot cuối."
echo "    Snapshot thủ công đang có: ${snaps}"
echo "    Muốn giữ dữ liệu thì tạo snapshot TRƯỚC:"
echo "      ${C_DIM}aws rds create-db-snapshot --db-instance-identifier ${HS_DB} \\"
echo "        --db-snapshot-identifier ${HS_DB}-truoc-khi-nuke --profile ${HS_PROFILE}${C_RESET}"
echo
echo "  ${C_RED}ECR${C_RESET} — force_delete = true, mọi image bị xoá kể cả khi repo còn tag."
echo "  ${C_RED}ACM cert${C_RESET} — bị xoá; dựng lại phải validate CNAME lại từ đầu."
echo "  ${C_RED}SSM Parameter${C_RESET} — connection-string, jwt-secret, db-password mất theo."
echo
echo "  ${C_GREEN}Bucket ảnh sản phẩm được bảo vệ:${C_RESET} force_destroy = false, nên destroy sẽ"
echo "  ${C_GREEN}THẤT BẠI ở bucket đó nếu nó còn object. Đó là lưới an toàn, không phải lỗi.${C_RESET}"
echo "  ${C_DIM}Muốn xoá thật thì phải tự empty bucket một cách có ý thức trước.${C_RESET}"

alive="$(aws elbv2 describe-load-balancers --query 'length(LoadBalancers)' --output text "${AWSQT[@]}" 2>/dev/null || echo 0)"
if [ "$alive" != "0" ]; then
  echo
  hs_warn "ALB đang chạy. Nên chạy down.sh trước để destroy gọn hơn."
fi

hs_head "XÁC NHẬN"
echo "  Gõ đúng câu sau rồi Enter (mọi thứ khác = huỷ):"
echo "    ${C_B}xoa het hushstore${C_RESET}"
printf '  > '
read -r answer
[ "$answer" = "xoa het hushstore" ] || { echo; hs_info "đã huỷ, không xoá gì."; exit 0; }

hs_head "DESTROY"
T0=$SECONDS
mkdir -p "${HS_LOCAL_DIR}/logs"
LOG="${HS_LOCAL_DIR}/logs/destroy-$(date +%Y%m%d-%H%M%S).log"

# Không pipe: exit code phải là của terraform.
terraform -chdir="$HS_TF_DIR" destroy -auto-approve -input=false -lock-timeout=5m -no-color \
  >"$LOG" 2>&1 &
pid=$!
while kill -0 "$pid" 2>/dev/null; do
  last="$(grep -E 'Destroying\.\.\.|Destruction complete|Still destroying' "$LOG" 2>/dev/null | tail -1 | cut -c1-72)"
  printf '\r  %-74s' "$(hs_hms $((SECONDS - T0)))  ${last}"
  sleep 5
done
set +e; wait "$pid"; rc=$?; set -e
printf '\r%-78s\r' ' '

if [ "$rc" -eq 0 ]; then
  hs_ok "destroy xong — $(hs_hms $((SECONDS - T0)))"
  hs_window_close
  echo "  ${C_DIM}Còn lại ngoài Terraform: bucket state (hushstore-tfstate) và snapshot RDS nếu có.${C_RESET}"
else
  echo "${C_RED}✗${C_RESET} destroy thất bại (exit ${rc}) — $(hs_hms $((SECONDS - T0)))" >&2
  tail -25 "$LOG" >&2
  echo "${C_DIM}Log đầy đủ: ${LOG}${C_RESET}" >&2
  echo "${C_DIM}Nếu vướng ở bucket ảnh: đúng như thiết kế. Empty bucket rồi chạy lại.${C_RESET}" >&2
  exit "$rc"
fi
