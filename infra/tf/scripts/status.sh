#!/usr/bin/env bash
# HushStore — bảng trạng thái hạ tầng, kèm đồng hồ cho từng resource.
#
# Vì sao cần: bật/tắt mất nhiều phút và không có tín hiệu nào ở tầng Terraform
# cho biết "đã dùng được chưa". Script này trả lời đúng ba câu: cái gì đang
# chạy, chạy bao lâu rồi, và đang tốn bao nhiêu.
#
# Thời gian lấy từ timestamp của AWS (CreatedTime / LaunchTime / registeredAt),
# không phải từ đồng hồ của script — nên tắt máy rồi mở lại vẫn ra số đúng.
#
# Dùng:
#   bash infra/tf/scripts/status.sh              # xem một lần
#   bash infra/tf/scripts/status.sh -w           # theo dõi liên tục (15s)
#   bash infra/tf/scripts/status.sh -w 5         # theo dõi, chu kỳ 5 giây
#
# Chỉ gọi API đọc. Không tạo, không sửa, không xoá gì.
#
# Exit code:  0 = đang BẬT đầy đủ   10 = đang TẮT   20 = đang chuyển trạng thái

# shellcheck source=lib.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

WATCH=0
INTERVAL=15
case "${1:-}" in
  -w|--watch) WATCH=1; [ -n "${2:-}" ] && INTERVAL="$2" ;;
  -h|--help)  sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
esac

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# ── Thu thập: mọi lệnh describe chạy song song ──────────────────
collect() {
  aws elbv2 describe-load-balancers --names "$HS_ALB" "${AWSQ[@]}" \
    >"$TMP/alb.json" 2>/dev/null || echo '{}' >"$TMP/alb.json" &

  aws ec2 describe-nat-gateways \
    --filter "Name=tag:Project,Values=${HS_PROJECT}" \
             "Name=state,Values=pending,available,deleting" \
    "${AWSQ[@]}" >"$TMP/nat.json" 2>/dev/null || echo '{}' >"$TMP/nat.json" &

  aws ec2 describe-addresses --filters "Name=tag:Project,Values=${HS_PROJECT}" \
    "${AWSQ[@]}" >"$TMP/eip.json" 2>/dev/null || echo '{}' >"$TMP/eip.json" &

  aws ec2 describe-instances \
    --filters "Name=tag:Project,Values=${HS_PROJECT}" \
              "Name=instance-state-name,Values=pending,running,shutting-down,stopping,stopped" \
    "${AWSQ[@]}" >"$TMP/ec2.json" 2>/dev/null || echo '{}' >"$TMP/ec2.json" &

  aws ecs describe-services --cluster "$HS_CLUSTER" \
    --services "$HS_SVC_API" "$HS_SVC_WEB" \
    "${AWSQ[@]}" >"$TMP/svc.json" 2>/dev/null || echo '{}' >"$TMP/svc.json" &

  aws rds describe-db-instances --db-instance-identifier "$HS_DB" \
    "${AWSQ[@]}" >"$TMP/rds.json" 2>/dev/null || echo '{}' >"$TMP/rds.json" &

  # Mốc thời gian start/stop của RDS không có trong describe-db-instances —
  # chỉ có trong event stream. Đây là nguồn duy nhất trả lời "RDS lên bao lâu rồi".
  aws rds describe-events --source-identifier "$HS_DB" --source-type db-instance \
    --duration 20160 "${AWSQ[@]}" >"$TMP/rdsev.json" 2>/dev/null || echo '{}' >"$TMP/rdsev.json" &

  aws ec2 describe-flow-logs --filter "Name=tag:Project,Values=${HS_PROJECT}" \
    "${AWSQ[@]}" >"$TMP/flow.json" 2>/dev/null || echo '{}' >"$TMP/flow.json" &

  # Container instance: phải list rồi describe, nên gộp vào một subshell.
  (
    arns="$(aws ecs list-container-instances --cluster "$HS_CLUSTER" \
      --query 'containerInstanceArns' --output text \
      --profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager 2>/dev/null || true)"
    if [ -n "$arns" ] && [ "$arns" != "None" ]; then
      # shellcheck disable=SC2086
      aws ecs describe-container-instances --cluster "$HS_CLUSTER" \
        --container-instances $arns "${AWSQ[@]}" >"$TMP/ci.json" 2>/dev/null \
        || echo '{}' >"$TMP/ci.json"
    else
      echo '{}' >"$TMP/ci.json"
    fi
  ) &

  # Target group: lấy ARN rồi hỏi sức khoẻ từng cái.
  (
    aws elbv2 describe-target-groups --names "$HS_TG_API" "$HS_TG_WEB" \
      "${AWSQ[@]}" >"$TMP/tg.json" 2>/dev/null || echo '{}' >"$TMP/tg.json"
    for t in api web; do
      case $t in api) n="$HS_TG_API" ;; web) n="$HS_TG_WEB" ;; esac
      arn="$(jq -r --arg n "$n" '.TargetGroups[]? | select(.TargetGroupName==$n) | .TargetGroupArn' \
             "$TMP/tg.json" 2>/dev/null | head -1)"
      if [ -n "$arn" ]; then
        aws elbv2 describe-target-health --target-group-arn "$arn" \
          "${AWSQ[@]}" >"$TMP/th-$t.json" 2>/dev/null || echo '{}' >"$TMP/th-$t.json"
      else
        echo '{}' >"$TMP/th-$t.json"
      fi
    done
  ) &

  wait
}

# ── In một dòng: resource | trạng thái | thời gian | $/giờ | đã tốn | ghi chú
row() {
  printf '%-14s %-18s %-10s %-8s %-8s %s\n' "$1" "$2" "$3" "$4" "$5" "${6:-}"
}

TOTAL_SPENT=0.000
add_spent() { TOTAL_SPENT="$(awk -v a="$TOTAL_SPENT" -v b="$1" 'BEGIN{printf "%.4f", a+b}')"; }

UP=0; DOWN=0; TRANSIT=0
tally() { case "$1" in up) UP=$((UP+1));; down) DOWN=$((DOWN+1));; transit) TRANSIT=$((TRANSIT+1));; esac; }

render() {
  echo "${C_B}HushStore — trạng thái hạ tầng${C_RESET}   ${C_DIM}$(date '+%H:%M:%S %Z')  ·  ${HS_PROFILE} / ${HS_REGION}${C_RESET}"

  # In cả ý muốn (tfvars) cạnh thực tế (AWS). Hai cột này lệch nhau nghĩa là có
  # apply chưa chạy hoặc chạy dở — thông tin đó không suy ra được từ bảng dưới.
  echo "${C_DIM}  tfvars: enable_nat=$(hs_tfvar_get enable_nat)  enable_alb=$(hs_tfvar_get enable_alb)  instance_count=$(hs_tfvar_get instance_count)  enable_flow_logs=$(hs_tfvar_get enable_flow_logs)  enable_deny_demo=$(hs_tfvar_get enable_deny_demo)${C_RESET}"
  if [ "$(hs_tfvar_get enable_deny_demo)" = "true" ]; then
    echo "  ${C_YELLOW}!${C_RESET} enable_deny_demo = true → NACL đang chặn ${C_B}$(hs_tfvar_get my_ip)${C_RESET} ở tầng mạng."
    echo "    ${C_DIM}Browser sẽ timeout và trông y như hạ tầng lỗi. Đặt false rồi apply nếu không đang demo.${C_RESET}"
  fi
  echo
  echo "${C_DIM}RESOURCE       TRẠNG THÁI         THỜI GIAN  $/GIỜ    ĐÃ TỐN   GHI CHÚ${C_RESET}"

  # ── ALB ──────────────────────────────────────────────────────
  st="$(jq -r '.LoadBalancers[0].State.Code // ""' "$TMP/alb.json")"
  if [ -n "$st" ]; then
    age="$(hs_age "$(jq -r '.LoadBalancers[0].CreatedTime // ""' "$TMP/alb.json")")"
    c="$(hs_cost "${age:-0}" "$HS_RATE_ALB")"; add_spent "$c"
    dns="$(jq -r '.LoadBalancers[0].DNSName // ""' "$TMP/alb.json")"
    if [ "$st" = "active" ]; then tally up; note="$dns"
    else tally transit; note="đang provisioning, thường ~3m"; fi
    row ALB "$st" "$(hs_hms "${age:-0}")" "$HS_RATE_ALB" "$c" "$note"
  else
    tally down; row ALB "-" "-" "-" "-" "chưa dựng (enable_alb = false)"
  fi

  # ── NAT Gateway ──────────────────────────────────────────────
  st="$(jq -r '.NatGateways[0].State // ""' "$TMP/nat.json")"
  if [ -n "$st" ]; then
    age="$(hs_age "$(jq -r '.NatGateways[0].CreateTime // ""' "$TMP/nat.json")")"
    c="$(hs_cost "${age:-0}" "$HS_RATE_NAT")"; add_spent "$c"
    case "$st" in available) tally up; note="egress cho ECR + SSM" ;;
                  *) tally transit; note="thường ~2m" ;; esac
    row "NAT Gateway" "$st" "$(hs_hms "${age:-0}")" "$HS_RATE_NAT" "$c" "$note"
  else
    tally down; row "NAT Gateway" "-" "-" "-" "-" "chưa dựng (enable_nat = false)"
  fi

  # ── EIP ──────────────────────────────────────────────────────
  n="$(jq -r '(.Addresses // []) | length' "$TMP/eip.json")"
  if [ "${n:-0}" -gt 0 ]; then
    idle="$(jq -r '[.Addresses[]? | select(.AssociationId == null)] | length' "$TMP/eip.json")"
    if [ "${idle:-0}" -gt 0 ]; then
      row EIP "${n} (${idle} rảnh)" "-" "$HS_RATE_EIP_IDLE" "-" "${C_YELLOW}EIP rảnh vẫn tính phí${C_RESET}"
    else
      row EIP "${n} (đang dùng)" "-" "free" "-" "gắn vào NAT nên miễn phí"
    fi
  else
    row EIP "0" "-" "-" "-" ""
  fi

  # ── EC2 container instance ───────────────────────────────────
  st="$(jq -r '.Reservations[0].Instances[0].State.Name // ""' "$TMP/ec2.json")"
  if [ -n "$st" ]; then
    age="$(hs_age "$(jq -r '.Reservations[0].Instances[0].LaunchTime // ""' "$TMP/ec2.json")")"
    iid="$(jq -r '.Reservations[0].Instances[0].InstanceId // ""' "$TMP/ec2.json")"
    typ="$(jq -r '.Reservations[0].Instances[0].InstanceType // ""' "$TMP/ec2.json")"
    case "$st" in running) tally up ;; *) tally transit ;; esac
    c="$(hs_cost "${age:-0}" "$HS_RATE_EC2")"; add_spent "$c"
    row "EC2 ${typ}" "$st" "$(hs_hms "${age:-0}")" "$HS_RATE_EC2" "$c" "$iid"
  else
    tally down; row "EC2" "-" "-" "-" "-" "chưa dựng (instance_count = 0)"
  fi

  # ── ECS container instance — GATE thật sự, không phải EC2 state ─
  n="$(jq -r '(.containerInstances // []) | length' "$TMP/ci.json")"
  if [ "${n:-0}" -gt 0 ]; then
    cst="$(jq -r '.containerInstances[0].status // ""' "$TMP/ci.json")"
    agent="$(jq -r '.containerInstances[0].agentConnected // false' "$TMP/ci.json")"
    age="$(hs_age "$(jq -r '.containerInstances[0].registeredAt // ""' "$TMP/ci.json")")"
    mem="$(jq -r '[.containerInstances[0].remainingResources[]? | select(.name=="MEMORY") | .integerValue][0] // "?"' "$TMP/ci.json")"
    if [ "$agent" = "true" ] && [ "$cst" = "ACTIVE" ]; then
      tally up; row "ECS instance" "ACTIVE agent-ok" "$(hs_hms "${age:-0}")" "-" "-" "RAM trống ${mem}MB"
    else
      tally transit
      row "ECS instance" "${cst} agent=${agent}" "$(hs_hms "${age:-0}")" "-" "-" "${C_RED}agent chưa kết nối — ECS không xếp task${C_RESET}"
    fi
  else
    tally down; row "ECS instance" "-" "-" "-" "-" "cluster chưa có capacity"
  fi

  # ── ECS service ──────────────────────────────────────────────
  for s in "$HS_SVC_API" "$HS_SVC_WEB"; do
    # Lọc theo status ACTIVE: ECS giữ service đã xoá ở INACTIVE trong describe
    # thêm một lúc. Đếm chúng là đang tồn tại thì bảng báo "0/0 running" và kết
    # luận sai thành "đang chuyển trạng thái" khi hệ thống thực ra đã tắt hẳn.
    got="$(jq -r --arg s "$s" '[.services[]? | select(.serviceName==$s and .status=="ACTIVE")] | length' "$TMP/svc.json")"
    short="svc ${s##*-}"
    if [ "${got:-0}" -gt 0 ]; then
      run="$(jq -r --arg s "$s" '.services[] | select(.serviceName==$s and .status=="ACTIVE") | .runningCount' "$TMP/svc.json")"
      des="$(jq -r --arg s "$s" '.services[] | select(.serviceName==$s and .status=="ACTIVE") | .desiredCount' "$TMP/svc.json")"
      pen="$(jq -r --arg s "$s" '.services[] | select(.serviceName==$s and .status=="ACTIVE") | .pendingCount' "$TMP/svc.json")"
      roll="$(jq -r --arg s "$s" '.services[] | select(.serviceName==$s and .status=="ACTIVE") | .deployments[0].rolloutState // "-"' "$TMP/svc.json")"
      if [ "$run" = "$des" ] && [ "$des" != "0" ]; then tally up; else tally transit; fi
      row "$short" "${run}/${des} running" "-" "-" "-" "pending ${pen} · ${roll}"
    else
      tally down; row "$short" "-" "-" "-" "-" "service chưa dựng"
    fi
  done

  # ── Target group health — điều kiện để browser mở được ───────
  for t in api web; do
    h="$(jq -r '[.TargetHealthDescriptions[]? | .TargetHealth.State] | join(",")' "$TMP/th-$t.json" 2>/dev/null)"
    if [ -n "$h" ] && [ "$h" != "" ]; then
      case "$h" in
        *unhealthy*) tally transit; row "tg-${t}" "$h" "-" "-" "-" "${C_YELLOW}chưa phục vụ được${C_RESET}" ;;
        *healthy*)   tally up;      row "tg-${t}" "$h" "-" "-" "-" "" ;;
        *)           tally transit; row "tg-${t}" "$h" "-" "-" "-" "thường ~1-2m để healthy" ;;
      esac
    else
      row "tg-${t}" "-" "-" "-" "-" "chưa có target nào đăng ký"
    fi
  done

  # ── RDS ──────────────────────────────────────────────────────
  st="$(jq -r '.DBInstances[0].DBInstanceStatus // ""' "$TMP/rds.json")"
  if [ -n "$st" ]; then
    ev="$(jq -r '[.Events[]? | select(.Message | test("^DB instance (started|stopped)$"))] | last | .Date // ""' "$TMP/rdsev.json" 2>/dev/null)"
    age="$(hs_age "$ev")"
    tstr="-"; [ -n "$age" ] && tstr="$(hs_hms "$age")"
    rate="$HS_RATE_RDS_STOPPED"; rcost="-"
    case "$st" in
      available)
        tally up; note="nhận kết nối được"
        # Tính phí theo giá niêm yết: 2/3 số này là CPU credit surplus vì SQL
        # Server ngồi ~36% CPU trên baseline 10%. Xem comment trong lib.sh.
        rate="$HS_RATE_RDS_UP"
        rcost="$(hs_cost "${age:-0}" "$rate")"; add_spent "$rcost"
        ;;
      starting)  tally transit; note="thường 5-10m — đây là bước lâu nhất" ;;
      stopping)  tally transit; note="thường ~5m" ;;
      stopped)
        tally down
        # AWS TỰ START lại một RDS đã stopped sau 7 ngày. Không có thông báo nào
        # ngoài email Budgets, mà email đó chỉ bắn khi đã tiêu tới 25% ngưỡng —
        # tức hơn hai ngày sau khi nó tự bật. Ở $0.098/giờ trên thẻ thật đó là
        # $2.35/ngày. Nên đồng hồ ngược này là thông tin phải thấy mà không phải
        # nhớ lệnh, chứ không phải thông tin đi tìm khi đã muộn.
        note="chỉ còn phí storage 20GB"
        if [ -n "$age" ]; then
          left=$((604800 - age))
          if [ "$left" -le 0 ]; then
            note="${note} — ${C_RED}ĐÃ QUÁ MỐC 7 NGÀY${C_RESET}: AWS có thể tự bật lại bất cứ lúc nào"
          elif [ "$left" -le 86400 ]; then
            note="${note} — ${C_RED}AWS tự bật lại trong $(hs_hms "$left")${C_RESET}"
          elif [ "$left" -le 259200 ]; then
            note="${note} — ${C_YELLOW}AWS tự bật lại sau $(hs_hms "$left")${C_RESET}"
          else
            note="${note} — AWS tự bật lại sau $(hs_hms "$left")"
          fi
        fi
        ;;
      *)         tally transit; note="" ;;
    esac
    row RDS "$st" "$tstr" "$rate" "$rcost" "$note"
  else
    row RDS "?" "-" "-" "-" "không đọc được — kiểm tra SSO session"
  fi

  # ── Flow Logs ────────────────────────────────────────────────
  n="$(jq -r '(.FlowLogs // []) | length' "$TMP/flow.json")"
  if [ "${n:-0}" -gt 0 ]; then
    row "Flow Logs" "$n active" "-" "ingest" "-" "${C_YELLOW}tính phí CloudWatch ingest${C_RESET}"
  fi

  # ── Tổng kết ─────────────────────────────────────────────────
  echo
  if [ "$TRANSIT" -gt 0 ]; then
    verdict="${C_YELLOW}ĐANG CHUYỂN TRẠNG THÁI${C_RESET} — chưa dùng được, chờ thêm"
    code=20
  elif [ "$UP" -gt 0 ]; then
    verdict="${C_GREEN}ĐANG BẬT${C_RESET} — mở browser được"
    code=0
  else
    verdict="${C_DIM}ĐANG TẮT${C_RESET} — không tốn phí theo giờ"
    code=10
  fi
  echo "  Kết luận  : ${verdict}"

  w="$(hs_window_seconds)"
  if [ -n "$w" ]; then
    echo "  Cửa sổ    : $(hs_hms "$w") tính từ lần up.sh gần nhất"
  fi
  echo "  Chi phí   : ${C_B}\$${TOTAL_SPENT}${C_RESET} ${C_DIM}giá niêm yết — ALB + NAT + RDS, tính từ lúc mỗi cái được tạo/start${C_RESET}"
  echo "              ${C_YELLOW}Không free tier, và credit đã HẾT HẠN — đây là tiền ra khỏi thẻ.${C_RESET}"

  if [ "$code" = "0" ]; then
    echo
    echo "  ${C_DIM}Xong việc thì:  bash infra/tf/scripts/down.sh${C_RESET}"
  fi
  return "$code"
}

hs_sso_check

if [ "$WATCH" = "1" ]; then
  while :; do
    collect
    printf '\033[H\033[2J'
    set +e; render; set -e
    echo
    echo "${C_DIM}  Làm mới mỗi ${INTERVAL}s · Ctrl-C để dừng${C_RESET}"
    sleep "$INTERVAL"
    TOTAL_SPENT=0.000; UP=0; DOWN=0; TRANSIT=0
  done
else
  collect
  render
fi
