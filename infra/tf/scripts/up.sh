#!/usr/bin/env bash
# HushStore — bật hạ tầng theo đúng thứ tự ràng buộc, có đồng hồ từng bước.
#
# Dùng:
#   bash infra/tf/scripts/up.sh            # bật đầy đủ, mở browser được
#   bash infra/tf/scripts/up.sh --no-alb   # chỉ NAT + EC2 + RDS (chạy migrate/seed)
#
# THỨ TỰ Ở ĐÂY LÀ RÀNG BUỘC, KHÔNG PHẢI KHUYẾN NGHỊ
#
# RDS phải nhận được kết nối TRƯỚC khi có capacity cho ECS service. Health check
# của tg-api gọi /health/ready, mà endpoint đó mở kết nối tới DbContext. Bật
# service khi RDS chưa lên thì ALB kết luận unhealthy sau 45 giây và task API bị
# giết rồi replace liên tục — đo được ~8 phút crash-loop trong lần chạy thật.
#
# Nhưng script này KHÔNG chờ RDS xong mới apply NAT + ALB như runbook viết. Nó
# phát lệnh start RDS trước, rồi apply NAT + ALB TRONG LÚC RDS đang starting, và
# chỉ chặn ở cửa "RDS available" ngay trước khi bật instance. An toàn vì với
# instance_count = 0 thì cluster không có capacity nào, task không được xếp lên
# đâu cả, nên không có gì crash-loop được. Đổi lại tiết kiệm ~4 phút mỗi lần bật:
# hai việc chậm nhất (RDS starting 5-10m, tạo NAT + ALB ~3m) chạy chồng nhau.
# Cửa chặn thật — RDS available trước instance_count = 1 — vẫn nguyên.

# shellcheck source=lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

WANT_ALB=true
case "${1:-}" in
  --no-alb) WANT_ALB=false ;;
  -h|--help) sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
esac

T_ALL=$SECONDS
hs_tf_check
hs_sso_check

rds_status() {
  aws rds describe-db-instances --db-instance-identifier "$HS_DB" \
    --query 'DBInstances[0].DBInstanceStatus' --output text \
    --profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager 2>/dev/null || echo unknown
}
rds_is() { [ "$(rds_status)" = "$1" ]; }

# Chỉ coi là healthy khi MỌI target ở trạng thái đúng chữ "healthy" và có ít
# nhất một target. So khớp chính xác, vì "unhealthy" cũng chứa "healthy" —
# dùng globbing ở đây là tự báo thành công trong lúc hệ thống đang lỗi.
tg_healthy() {
  for n in "$HS_TG_API" "$HS_TG_WEB"; do
    arn="$(aws elbv2 describe-target-groups --names "$n" \
      --query 'TargetGroups[0].TargetGroupArn' --output text \
      --profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager 2>/dev/null || true)"
    [ -n "$arn" ] && [ "$arn" != "None" ] || return 1
    ok="$(aws elbv2 describe-target-health --target-group-arn "$arn" \
      --profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager --output json 2>/dev/null \
      | jq -r '[.TargetHealthDescriptions[]?.TargetHealth.State] as $s
               | if ($s | length) > 0 and ([$s[] | select(. != "healthy")] | length) == 0
                 then "yes" else "no" end')"
    [ "$ok" = "yes" ] || return 1
  done
}

hs_head "BƯỚC 1/6 — RDS"
st="$(rds_status)"
case "$st" in
  available) hs_ok "RDS đã available, không cần làm gì" ;;
  starting)  hs_info "RDS đang starting sẵn từ trước" ;;
  stopping)
    hs_warn "RDS đang stopping — AWS không cho start trong lúc này, phải chờ về stopped"
    hs_wait_until "RDS về stopped" 900 "~5m" rds_is stopped
    aws rds start-db-instance --db-instance-identifier "$HS_DB" \
      "${AWSQ[@]}" >/dev/null
    hs_ok "đã phát lệnh start"
    ;;
  stopped)
    aws rds start-db-instance --db-instance-identifier "$HS_DB" \
      "${AWSQ[@]}" >/dev/null
    hs_ok "đã phát lệnh start — chạy tiếp trong lúc nó lên, chưa chờ ở đây"
    ;;
  *) hs_die "RDS ở trạng thái không xử lý được: ${st}" ;;
esac

if [ "$WANT_ALB" = true ]; then STEP2="NAT Gateway + ALB"; else STEP2="NAT Gateway"; fi
hs_head "BƯỚC 2/6 — ${STEP2}"
hs_window_open
hs_tfvar_set enable_nat true
if [ "$WANT_ALB" = true ]; then
  hs_tfvar_set enable_alb true
else
  hs_info "bỏ qua ALB theo --no-alb: website sẽ KHÔNG mở được, chỉ chạy được task"
fi
if [ "$WANT_ALB" = true ]; then A2="tạo NAT + ALB + target group + service"; else A2="tạo NAT"; fi
hs_apply "$A2"

hs_head "BƯỚC 3/6 — cửa chặn: RDS phải available"
hs_wait_until "RDS available" 1200 "5-10m, bước lâu nhất" rds_is available \
  || hs_die "RDS không lên. Kiểm tra: aws rds describe-events --source-identifier ${HS_DB} --source-type db-instance --duration 60 --profile ${HS_PROFILE}"

hs_head "BƯỚC 4/6 — EC2 container instance"
hs_tfvar_set instance_count 1
hs_apply "bật instance"

hs_head "BƯỚC 5/6 — cửa chặn: instance phải đăng ký vào ECS cluster"
# `terraform apply` xanh KHÔNG có nghĩa cluster dùng được: ASG dùng
# health_check_type = "EC2" nên nó chỉ hỏi "EC2 có running không". Script này
# kiểm cả agentConnected — đó là khác biệt giữa "đã dựng" và "dựng xong dùng được".
bash "${HS_SCRIPT_DIR}/wait-for-capacity.sh" "$HS_CLUSTER" 420 \
  || hs_die "cluster không có capacity dùng được — xem hướng dẫn chẩn đoán ở trên"

if [ "$WANT_ALB" = false ]; then
  hs_head "XONG (chế độ --no-alb) — $(hs_hms $((SECONDS - T_ALL)))"
  echo "  Chạy task được rồi. Website chưa mở được vì không có ALB."
  echo "  Trạng thái : bash infra/tf/scripts/status.sh"
  echo "  Tắt        : bash infra/tf/scripts/down.sh"
  exit 0
fi

hs_head "BƯỚC 6/6 — target group phải healthy"
hs_wait_until "cả tg-api và tg-web healthy" 600 "1-3m sau khi task lên" tg_healthy \
  || hs_warn "target chưa healthy hết. Xem log: aws logs tail /ecs/${HS_PROJECT}-api --since 10m --profile ${HS_PROFILE}"

# ── DNS: tên DNS của ALB đổi mỗi lần ALB được tạo lại ──────────
# Phần hậu tố trong hushstore-alb-<số>.ap-southeast-1.elb.amazonaws.com do AWS
# sinh lúc tạo. down.sh destroy ALB nên mỗi lần bật lại là một tên mới. Zone có
# một record trung gian `alb` để chỉ phải sửa MỘT chỗ thay vì hai.
hs_head "DNS"
ALB_DNS="$(hs_tf_out alb_dns_name)"
WEB_DOMAIN="$(hs_tfvar_get web_domain)"; WEB_DOMAIN="${WEB_DOMAIN:-hushstore.io.vn}"
CUR="$(dig +short CNAME "alb.${WEB_DOMAIN}" 2>/dev/null | sed 's/\.$//' | head -1)"

echo "  ALB hiện tại      : ${ALB_DNS}"
echo "  alb.${WEB_DOMAIN} đang trỏ : ${CUR:-（chưa có record）}"
if [ -n "$ALB_DNS" ] && [ "$CUR" = "$ALB_DNS" ]; then
  hs_ok "DNS đã đúng, không phải sửa gì"
else
  echo
  hs_warn "PHẢI SỬA CLOUDFLARE TRƯỚC KHI MỞ BROWSER"
  echo "     Sửa record  CNAME  alb   →   ${C_B}${ALB_DNS}${C_RESET}"
  echo "     Proxy status: DNS only (mây xám). Hai record @ và api không phải chạm."
  echo "     Sau khi sửa, DNS lan trong ~1-2 phút."
fi

hs_head "KIỂM TRA CUỐI"
for u in "https://${WEB_DOMAIN}/" "https://api.${WEB_DOMAIN}/health/ready"; do
  code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "$u" 2>/dev/null || echo 000)"
  case "$code" in
    200) hs_ok  "${u} → ${code}" ;;
    000) hs_warn "${u} → không kết nối được (DNS chưa lan, hoặc record alb chưa sửa)" ;;
    *)   hs_warn "${u} → ${code}" ;;
  esac
done

hs_head "XONG — tổng $(hs_hms $((SECONDS - T_ALL)))"
echo "  Website     : https://${WEB_DOMAIN}/"
echo "  API         : https://api.${WEB_DOMAIN}/"
echo "  Đăng nhập   : admin@hushstore.com / Admin@123"
echo
echo "  Theo dõi    : bash infra/tf/scripts/status.sh -w"
echo "  ${C_YELLOW}Tắt khi xong: bash infra/tf/scripts/down.sh${C_RESET}  ${C_DIM}(NAT + ALB \$0.0675/giờ)${C_RESET}"
