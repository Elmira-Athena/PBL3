#!/usr/bin/env bash
# HushStore — tắt hạ tầng theo đúng thứ tự ngược, rồi TỰ KIỂM CHỨNG là đã tắt.
#
# Dùng:  bash infra/tf/scripts/down.sh
#
# THỨ TỰ CŨNG LÀ RÀNG BUỘC
#
# Service phải chết trước ALB và target group: destroy target group khi service
# còn tham chiếu tới nó thì ECS liên tục thử replace task và apply vướng lại.
# Instance phải chết trước NAT: instance mất egress giữa lúc đang drain thì ECS
# agent không báo được gì về control plane.
#
# Script tự xử lý lifecycle hook `ecs-managed-draining-termination-hook`. ECS tạo
# hook này (heartbeat 3600s) ngay khi ASG được đăng ký làm capacity provider, kể
# cả khi managed_termination_protection = DISABLED — đó là hai tính năng khác
# nhau. Hook KHÔNG do Terraform quản và heartbeat không phải argument của
# aws_ecs_capacity_provider, nên không sửa được bằng config. Bỏ qua nó thì
# instance treo ở Terminating:Wait và vẫn tính giờ tới một tiếng. Ở đây có một
# watcher chạy nền giải phóng hook ngay khi thấy, nên apply không phải chờ.

# shellcheck source=lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

case "${1:-}" in -h|--help) sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;; esac

T_ALL=$SECONDS
hs_tf_check
hs_sso_check

WINDOW="$(hs_window_seconds)"

# ── Watcher giải phóng lifecycle hook ───────────────────────────
hook_watcher() {
  while :; do
    iid="$(aws autoscaling describe-auto-scaling-groups \
      --auto-scaling-group-names "$HS_ASG" \
      --query "AutoScalingGroups[0].Instances[?LifecycleState=='Terminating:Wait'].InstanceId | [0]" \
      --output text --profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager 2>/dev/null || true)"
    if [ -n "$iid" ] && [ "$iid" != "None" ]; then
      aws autoscaling complete-lifecycle-action \
        --lifecycle-hook-name ecs-managed-draining-termination-hook \
        --auto-scaling-group-name "$HS_ASG" --instance-id "$iid" \
        --lifecycle-action-result CONTINUE \
        --profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager >/dev/null 2>&1 || true
    fi
    sleep 15
  done
}

WATCHER=
stop_watcher() { [ -n "$WATCHER" ] && kill "$WATCHER" 2>/dev/null || true; WATCHER=; }
trap stop_watcher EXIT

no_instance() {
  n="$(aws ec2 describe-instances \
    --filters "Name=tag:Project,Values=${HS_PROJECT}" \
              "Name=instance-state-name,Values=pending,running,shutting-down,stopping,stopped" \
    --query 'length(Reservations[].Instances[])' --output text \
    --profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager 2>/dev/null || echo 1)"
  [ "$n" = "0" ]
}

hs_head "BƯỚC 1/5 — ECS service + ALB + target group"
hs_tfvar_set enable_alb false
hs_apply "xoá serving stack (service drain ~2m30s)"

hs_head "BƯỚC 2/5 — EC2 instance về 0"
hook_watcher & WATCHER=$!
hs_info "watcher lifecycle hook đang chạy nền (pid ${WATCHER})"
hs_tfvar_set instance_count 0
hs_apply "hạ ASG về 0"
hs_wait_until "instance biến mất thật" 600 "~1m sau khi hook được giải phóng" no_instance \
  || hs_warn "instance vẫn còn — kiểm tra thủ công trước khi xoá NAT"
stop_watcher

hs_head "BƯỚC 3/5 — NAT Gateway + EIP"
hs_tfvar_set enable_nat false
# Reset luôn hai toggle demo. enable_flow_logs tính phí ingest CloudWatch nếu để
# quên. enable_deny_demo thì $0 nhưng tệ hơn: lần bật sau nó chặn đúng IP của
# mình ở tầng NACL, và triệu chứng là browser timeout — trông y như hạ tầng lỗi.
if [ "$(hs_tfvar_get enable_flow_logs)" = "true" ]; then
  hs_tfvar_set enable_flow_logs false
  hs_info "tắt luôn flow logs (tính phí ingest CloudWatch)"
fi
if [ "$(hs_tfvar_get enable_deny_demo)" = "true" ]; then
  hs_tfvar_set enable_deny_demo false
  hs_info "tắt luôn NACL deny demo (để lần bật sau không tự chặn IP của mình)"
fi
hs_apply "xoá NAT + EIP"

hs_head "BƯỚC 4/5 — RDS"
st="$(aws rds describe-db-instances --db-instance-identifier "$HS_DB" \
  --query 'DBInstances[0].DBInstanceStatus' --output text \
  --profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager 2>/dev/null || echo unknown)"
case "$st" in
  available)
    aws rds stop-db-instance --db-instance-identifier "$HS_DB" "${AWSQ[@]}" >/dev/null
    hs_ok "đã phát lệnh stop (mất ~5m để về stopped, không cần chờ)"
    ;;
  stopped|stopping) hs_ok "RDS đã ${st}" ;;
  *) hs_warn "RDS đang ở ${st} — chưa stop được, chạy lại down.sh sau" ;;
esac

# ── Xác nhận độc lập — đừng chỉ tin terraform ───────────────────
hs_head "BƯỚC 5/5 — kiểm chứng bằng AWS API, không tin terraform"
fail=0
chk() { # chk <nhãn> <số đếm đọc được>
  if [ "$2" = "0" ]; then printf '  %-22s %s\n' "$1" "${C_GREEN}0${C_RESET}"
  else printf '  %-22s %s\n' "$1" "${C_RED}$2 — CÒN SỐNG${C_RESET}"; fail=1; fi
}
chk "Load balancer" "$(aws elbv2 describe-load-balancers --query 'length(LoadBalancers)' --output text "${AWSQT[@]}" 2>/dev/null || echo '?')"
chk "NAT Gateway" "$(aws ec2 describe-nat-gateways --filter "Name=state,Values=pending,available" --query 'length(NatGateways)' --output text "${AWSQT[@]}" 2>/dev/null || echo '?')"
chk "Elastic IP" "$(aws ec2 describe-addresses --query 'length(Addresses)' --output text "${AWSQT[@]}" 2>/dev/null || echo '?')"
chk "EC2 đang sống" "$(aws ec2 describe-instances --filters "Name=instance-state-name,Values=pending,running,stopping,stopped" --query 'length(Reservations[].Instances[])' --output text "${AWSQT[@]}" 2>/dev/null || echo '?')"
chk "VPC Flow Log" "$(aws ec2 describe-flow-logs --query 'length(FlowLogs)' --output text "${AWSQT[@]}" 2>/dev/null || echo '?')"

mkdir -p "${HS_LOCAL_DIR}/logs"
set +e
terraform -chdir="$HS_TF_DIR" plan -detailed-exitcode -input=false -lock-timeout=5m -no-color \
  >"${HS_LOCAL_DIR}/logs/plan-down.log" 2>&1
prc=$?
set -e
case $prc in
  0) hs_ok "terraform plan: No changes — state khớp thực tế" ;;
  2) hs_warn "terraform plan còn thay đổi chưa áp — xem ${HS_LOCAL_DIR}/logs/plan-down.log"; fail=1 ;;
  *) hs_warn "terraform plan lỗi — xem ${HS_LOCAL_DIR}/logs/plan-down.log"; fail=1 ;;
esac

hs_head "XONG — tổng $(hs_hms $((SECONDS - T_ALL)))"
if [ -n "$WINDOW" ]; then
  echo "  Cửa sổ tính phí : $(hs_hms "$WINDOW")  ·  ~\$$(hs_cost "$WINDOW" 0.0675)  ${C_DIM}(NAT \$0.045 + ALB \$0.0225)${C_RESET}"
  echo "  ${C_DIM}Đây là chặn trên: NAT và ALB sống ngắn hơn cả cửa sổ vì được tạo sau và xoá trước.${C_RESET}"
fi
hs_window_close

if [ "$fail" = "0" ]; then
  hs_ok "Không còn resource nào tính theo giờ. Còn lại: S3, ECR, snapshot, ACM cert — tất cả \$0 hoặc xấp xỉ."
else
  hs_warn "Có mục chưa sạch ở trên. Chạy: bash infra/tf/scripts/status.sh"
  exit 1
fi
