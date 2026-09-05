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

# 🚨 ĐO TRƯỚC KHI PHÁ. Sau bước 3 thì ALB, NAT và EC2 đã biến mất, và không có
# API nào hỏi được "cái vừa bị xoá đã sống bao lâu". Bản trước bù chỗ đó bằng
# cách nhân cửa sổ với đơn giá cả stack — và in ra $75.50 cho một cửa sổ tốn
# vài xu. Xem comment dài ở hs_spend_now trong lib.sh.
SPEND_AT_START="$(hs_spend_now)"

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

# ── READ REPLICA PHẢI CHẾT TRƯỚC PRIMARY ───────────────────────
# 🔴 THỨ TỰ Ở ĐÂY KHÔNG PHẢI SỞ THÍCH. AWS: "You can't stop a DB instance that
# has a read replica, or that is a read replica." Nếu còn replica thì lệnh
# stop-db-instance ở BƯỚC 4 bị từ chối bằng InvalidDBInstanceState, và nhánh
# `*)` của case dưới chỉ in một dòng warn màu vàng — script vẫn exit 0. Kết quả:
# người chạy thấy down.sh "xong", trong khi CẢ primary lẫn replica vẫn tính đủ
# tiền giờ. Cộng với việc RDS tự khởi động lại sau 7 ngày stopped, một lần bỏ
# sót là hoá đơn chạy nhiều ngày.
#
# Đặt ở đây, SAU apply của bước 3 và TRƯỚC lệnh stop — không gộp vào apply trên
# để dòng log nói đúng việc đang làm khi nó mất vài phút.
# 🚨 HỎI AWS, KHÔNG CHỈ HỎI tfvars. Bản trước chỉ đọc `enable_read_replica`
# trong tfvars. Điều đó đúng khi tfvars luôn khớp thực tế — nhưng chính script
# này ĐẶT nó về false rồi mới apply, nên một lần apply hỏng giữa chừng (mạng
# rớt, Ctrl-C, hết hạn credential) để lại đúng trạng thái tồi nhất: tfvars nói
# "không có replica", AWS thì vẫn còn. Lần chạy sau guard bị bỏ qua hoàn toàn,
# stop bị từ chối, và script vẫn báo xong.
rep_live() {
  aws rds describe-db-instances --db-instance-identifier "$HS_DB" \
    --query 'DBInstances[0].ReadReplicaDBInstanceIdentifiers' --output text \
    --profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager 2>/dev/null \
    | sed 's/None//' | tr -d '[:space:]'
}

REP_NOW="$(rep_live)"
if [ -n "$REP_NOW" ] || [ "$(hs_tfvar_get enable_read_replica)" = "true" ]; then
  hs_warn "còn read replica (${REP_NOW:-theo tfvars}) — phải huỷ TRƯỚC khi stop primary, nếu không AWS từ chối lệnh stop"
  hs_tfvar_set enable_read_replica false
  hs_apply "huỷ read replica"

  # Xác nhận lại bằng AWS. Nếu replica được tạo ngoài Terraform thì apply ở trên
  # KHÔNG xoá nó (state không có), và im lặng đi tiếp là quay lại đúng cái bẫy.
  if [ -n "$(rep_live)" ]; then
    hs_warn "VẪN CÒN replica sau khi apply — nhiều khả năng nó được tạo ngoài Terraform."
    hs_warn "Xoá tay rồi chạy lại: aws rds delete-db-instance --db-instance-identifier <id> --skip-final-snapshot"
  fi
fi

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
  *)
    hs_warn "RDS đang ở ${st} — chưa stop được, chạy lại down.sh sau"
    # Nêu đích danh nghi phạm hay gặp nhất, vì `modifying` do huỷ replica sinh ra
    # trông giống hệt `modifying` do bất kỳ thay đổi nào khác.
    rep="$(aws rds describe-db-instances --db-instance-identifier "$HS_DB" \
      --query 'DBInstances[0].ReadReplicaDBInstanceIdentifiers' --output text \
      --profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager 2>/dev/null || echo "")"
    if [ -n "$rep" ] && [ "$rep" != "None" ]; then
      hs_warn "nguyên nhân: vẫn còn read replica ($rep). Đặt enable_read_replica=false rồi apply."
    fi
    ;;
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

# 🚨 RDS TỪNG KHÔNG CÓ TRONG DANH SÁCH NÀY, VÀ ĐÓ LÀ LỖ HỔNG LỚN NHẤT CỦA
# down.sh. Năm phép kiểm phía trên đều là resource do Terraform tạo/xoá, nên
# `terraform plan` ở dưới bắt được. RDS thì KHÁC: "đang chạy" hay "đã stop" là
# trạng thái RUNTIME mà Terraform không quản — plan trả về "No changes" y hệt
# trong cả hai trường hợp.
# Hệ quả trước khi vá: nếu lệnh stop ở BƯỚC 4 bị từ chối (còn replica, hoặc
# instance đang `modifying`/`backing-up`), nhánh `*)` chỉ in một dòng vàng và
# KHÔNG đặt fail=1 — script in "Không còn resource nào tính theo giờ" rồi exit 0
# trong khi RDS, khoản ĐẮT NHẤT của cả stack, vẫn chạy. Cộng với việc AWS tự
# khởi động lại sau 7 ngày, một lần bỏ sót là hoá đơn nhiều ngày.
rds_st="$(aws rds describe-db-instances --db-instance-identifier "$HS_DB" \
  --query 'DBInstances[0].DBInstanceStatus' --output text \
  --profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager 2>/dev/null || echo '?')"
case "$rds_st" in
  stopped|stopping)
    printf '  %-22s %s\n' "RDS" "${C_GREEN}${rds_st}${C_RESET}"
    ;;
  *)
    printf '  %-22s %s\n' "RDS" "${C_RED}${rds_st} — CÒN TÍNH TIỀN${C_RESET}"
    fail=1
    ;;
esac

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
  # 🚨 CON SỐ NÀY TỪNG IN CỨNG "0.1954" — tổng của ĐÚNG MỘT NAT. Từ lúc
  # nat_gateway_count = 2 nó báo thiếu $0.059/giờ, và báo thiếu ở dòng cuối cùng
  # người dùng đọc trước khi rời máy. Nay tính từ HS_RATE_* và số NAT thật.
  # Cửa sổ là THÔNG TIN, không phải thừa số nhân. Nó nói "bao lâu kể từ up.sh",
  # không nói "bao lâu có thứ gì đó tính tiền" — hai câu khác hẳn nhau khi stack
  # nằm im phần lớn thời gian.
  echo "  Cửa sổ    : $(hs_hms "$WINDOW") kể từ up.sh gần nhất ${C_DIM}(không phải thời gian tính tiền)${C_RESET}"
  echo "  Đã tốn    : ${C_B}\$${SPEND_AT_START}${C_RESET} ${C_DIM}— cộng theo TUỔI THẬT của từng resource còn sống lúc down.sh bắt đầu${C_RESET}"
  if [ "$(hs_multi_az)" = "true" ]; then
    echo "  ${C_DIM}  Multi-AZ BẬT: RDS thật ~\$0.057/giờ (instance ×2 + storage ×2), tức DƯỚI \$${HS_RATE_RDS_UP} ở trên.${C_RESET}"
    echo "  ${C_DIM}  \$${HS_RATE_RDS_UP} là số đo trên sqlserver-ex, giữ lại làm CẬN TRÊN — xem lib.sh.${C_RESET}"
  fi
  echo "  ${C_DIM}  Giá niêm yết, trừ vào credit trả trước. Số chính xác hơn: chạy status.sh TRƯỚC down.sh.${C_RESET}"
fi
hs_window_close

if [ "$fail" = "0" ]; then
  hs_ok "Không còn resource nào tính theo giờ. Còn lại: S3, ECR, snapshot, ACM cert — tất cả \$0 hoặc xấp xỉ."
else
  hs_warn "Có mục chưa sạch ở trên. Chạy: bash infra/tf/scripts/status.sh"
  exit 1
fi
