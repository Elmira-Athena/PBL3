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

  # ── NHỊP TIM CỦA COST GUARD ─────────────────────────────────────
  # Cost guard im lặng khi khoẻ: không email, không CloudWatch alarm (alarm tốn
  # $0.10/tháng và credit đã hết). Hệ quả là "guard đang chạy mỗi đêm" và "guard
  # đã chết từ tuần trước" giống nhau từng byte từ bên ngoài — kể cả khi nó chết
  # ở init vì thiếu một biến môi trường, kể cả khi schedule bị xoá, kể cả khi
  # đường Scheduler→Lambda chưa từng chạy lần nào. `raise` trong mã Python làm
  # lần chạy đỏ trong CloudWatch, nhưng không ai canh CloudWatch.
  #
  # Hai lệnh đọc dưới đây là MIỄN PHÍ và trả lời đúng hai nửa của câu hỏi: log
  # stream mới nhất nói guard CHẠY lần cuối bao giờ, còn get-schedule nói lưới
  # an toàn có còn được HẸN hay không. Thiếu nửa sau thì một dòng đỏ "guard chưa
  # chạy 5 ngày" không nói được là guard hỏng hay là có người cố ý tắt lưới bằng
  # enable_auto_stop = false.
  aws logs describe-log-streams \
    --log-group-name "/aws/lambda/${HS_PROJECT}-cost-guard" \
    --order-by LastEventTime --descending --max-items 1 \
    "${AWSQ[@]}" >"$TMP/guard.json" 2>/dev/null || echo '{}' >"$TMP/guard.json" &

  aws scheduler get-schedule --name "${HS_PROJECT}-nightly-stop" \
    "${AWSQ[@]}" >"$TMP/guardsch.json" 2>/dev/null || echo '{}' >"$TMP/guardsch.json" &

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
  echo "${C_DIM}  tfvars: enable_nat=$(hs_tfvar_get enable_nat)  nat_gateway_count=$(hs_tfvar_get nat_gateway_count)  enable_alb=$(hs_tfvar_get enable_alb)  instance_count=$(hs_tfvar_get instance_count)  enable_flow_logs=$(hs_tfvar_get enable_flow_logs)  enable_deny_demo=$(hs_tfvar_get enable_deny_demo)${C_RESET}"
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

  # ── NAT Gateway — MỘT DÒNG MỖI GATEWAY ───────────────────────
  # 🚨 Bản trước đọc `.NatGateways[0]` và in đúng MỘT dòng. Điều đó đúng khi
  # stack chỉ có thể có một NAT, và trở thành BÁO SAI TIỀN từ lúc
  # nat_gateway_count = 2: gateway thứ hai không hiện ở đâu cả, và tổng chi phí
  # thiếu đúng $0,0590/giờ. Đó là chế độ lỗi tệ nhất của một cái đồng hồ tiền —
  # nó không im lặng, nó NÓI DỐI, và nói dối theo hướng làm người đọc yên tâm.
  nat_n="$(jq -r '(.NatGateways // []) | length' "$TMP/nat.json")"
  if [ "${nat_n:-0}" -gt 0 ]; then
    for i in $(seq 0 $((nat_n - 1))); do
      st="$(jq -r --argjson i "$i" '.NatGateways[$i].State // ""' "$TMP/nat.json")"
      az="$(jq -r --argjson i "$i" '.NatGateways[$i].SubnetId // "?"' "$TMP/nat.json")"
      age="$(hs_age "$(jq -r --argjson i "$i" '.NatGateways[$i].CreateTime // ""' "$TMP/nat.json")")"
      c="$(hs_cost "${age:-0}" "$HS_RATE_NAT")"; add_spent "$c"
      case "$st" in available) tally up; note="egress cho ECR + SSM · ${az}" ;;
                    *) tally transit; note="thường ~2m · ${az}" ;; esac
      row "NAT Gateway $((i + 1))/${nat_n}" "$st" "$(hs_hms "${age:-0}")" "$HS_RATE_NAT" "$c" "$note"
    done
    # Cảnh báo khi số gateway thật KHÁC tfvars: hai NAT trong khi tfvars khai 1
    # nghĩa là có một cái mồ côi ngoài Terraform, và nó không bị down.sh dọn.
    want="$(hs_tfvar_get nat_gateway_count || echo 1)"
    if [ "$nat_n" != "$want" ]; then
      row "NAT lệch tfvars" "$nat_n vs $want" "-" "-" "-" \
        "${C_RED}có $nat_n gateway thật nhưng tfvars khai nat_gateway_count=$want${C_RESET} — kiểm gateway mồ côi, down.sh chỉ dọn cái Terraform biết"
    fi
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
        # Multi-AZ nhân đôi tiền instance VÀ tiền storage. Không hiện nó ra thì
        # bảng chi phí đúng con số nhưng thiếu lý do — và lần sau ai đó nhìn
        # hoá đơn sẽ không nối được về đây. (Multi-AZ KHÔNG chặn stop trên
        # PostgreSQL, khác read replica — xem modules/data/main.tf.)
        if [ "$(jq -r '.DBInstances[0].MultiAZ // false' "$TMP/rds.json")" = "true" ]; then
          note="${note} · ${C_B}Multi-AZ${C_RESET} (standby AZ thứ hai, KHÔNG phục vụ đọc)"
        fi
        # Tính phí theo giá niêm yết. 2/3 số này là CPU credit surplus đo hồi
        # còn chạy SQL Server (~36% CPU khi không tải, baseline 10%). Sau khi
        # chuyển PostgreSQL nó là CẬN TRÊN — bảng báo đắt hơn thực tế, lệch về
        # phía an toàn. Chưa đo lại, cố ý. Xem cảnh báo trong lib.sh.
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
        else
          # `age` rỗng nghĩa là describe-events không có sự kiện start/stop nào
          # trong 14 ngày lookback (--duration 20160). Cửa sổ tự-start của AWS là
          # 7 ngày, nên KHÔNG có mốc trong 14 ngày là ca ĐÁNG LO NHẤT chứ không
          # phải ca bình thường: nó nghĩa là mốc đã trôi khỏi tầm nhìn và mọi
          # thứ đang tới hạn hoặc đã quá hạn. Cho cả khối này biến mất khi không
          # có mốc là để bảng im lặng đúng lúc cần nó nhất.
          note="${note} — ${C_YELLOW}không rõ mốc stop cuối${C_RESET}: không có sự kiện start/stop nào trong 14 ngày, nên đồng hồ 7 ngày không tính được. Tự kiểm bằng: aws rds describe-events --source-identifier ${HS_DB} --source-type db-instance --duration 43200 --profile ${HS_PROFILE} --region ${HS_REGION}"
        fi
        ;;
      *)         tally transit; note="" ;;
    esac
    row RDS "$st" "$tstr" "$rate" "$rcost" "$note"

    # ── READ REPLICA — lớp phòng thủ cuối cho "quên huỷ replica" ──
    # 🔴 Replica là tài nguyên DUY NHẤT trong stack vừa tính tiền theo giờ vừa
    # VÔ HIỆU HOÁ cơ chế tắt tiền: còn nó thì AWS từ chối stop primary. Nghĩa là
    # bỏ quên nó không tốn gấp đôi mà tốn gấp đôi MÃI, cho tới khi có người để ý.
    # Cost guard đã được vá để gửi email về việc này, nhưng email chỉ tới vào
    # 00:00; dòng dưới đây là chỗ thấy nó NGAY khi gõ status.sh.
    reps="$(jq -r '.DBInstances[0].ReadReplicaDBInstanceIdentifiers // [] | join(", ")' "$TMP/rds.json")"
    if [ -n "$reps" ]; then
      rep_cost="$(hs_cost "${age:-0}" "$HS_RATE_RDS_UP")"
      row "RDS replica" "sống" "$tstr" "$HS_RATE_RDS_UP" "$rep_cost" \
        "${C_RED}${reps} — CÒN NÓ THÌ KHÔNG STOP ĐƯỢC PRIMARY${C_RESET}: đặt enable_read_replica=false rồi apply, sau đó chạy down.sh"
      tally up
      add_spent "$rep_cost"
    fi
  else
    row RDS "?" "-" "-" "-" "không đọc được — kiểm tra SSO session"
  fi

  # ── COST GUARD — nhịp tim của lưới an toàn ───────────────────
  # Đặt ngay cạnh đồng hồ ngược của RDS vì hai dòng này nói về cùng một rủi ro:
  # đồng hồ kia nói AWS sẽ tự bật RDS lúc nào, dòng này nói còn ai canh việc đó
  # hay không. Đọc riêng một dòng thì cả hai đều thiếu nửa còn lại.
  #
  # KHÔNG gọi tally: guard không phải hạ tầng bật/tắt, và tính nó vào UP/DOWN sẽ
  # làm exit code (và câu "ĐANG BẬT — mở browser được") nói về một thứ khác hẳn.
  sch_state="$(jq -r '.State // ""' "$TMP/guardsch.json")"
  case "$sch_state" in
    ENABLED)  sch_note="schedule ENABLED $(jq -r '.ScheduleExpression // "?"' "$TMP/guardsch.json")" ;;
    DISABLED) sch_note="${C_RED}schedule DISABLED — lưới an toàn đang TẮT${C_RESET}" ;;
    *)        sch_note="${C_RED}không thấy schedule ${HS_PROJECT}-nightly-stop${C_RESET} (enable_auto_stop = false, hoặc chưa apply)" ;;
  esac

  gage="$(hs_age_ms "$(jq -r '.logStreams[0].lastEventTimestamp // ""' "$TMP/guard.json")")"
  if [ -n "$gage" ]; then
    # Ngưỡng theo chu kỳ chạy: guard chạy mỗi 24 giờ, nên quá 24 giờ là đã trượt
    # một đêm và quá 48 giờ là trượt hai đêm liền — mức đó không còn là trùng
    # hợp, nó nghĩa là lưới an toàn không còn hoạt động.
    if [ "$gage" -le 86400 ]; then
      row "cost guard" "$(hs_hms "$gage") trước" "-" "-" "-" "${C_GREEN}✓${C_RESET} ${sch_note}"
    elif [ "$gage" -le 172800 ]; then
      row "cost guard" "$(hs_hms "$gage") trước" "-" "-" "-" "${C_YELLOW}trượt 1 đêm${C_RESET} · ${sch_note}"
    else
      row "cost guard" "$(hs_hms "$gage") trước" "-" "-" "-" "${C_RED}TRƯỢT TỪ 2 ĐÊM — LƯỚI AN TOÀN KHÔNG CÒN${C_RESET} · ${sch_note}"
    fi
  else
    # Chưa có log stream nào. Hai nguyên nhân, cùng một hệ quả: guard chưa từng
    # chạy (đường Scheduler→Lambda chưa được chứng minh lần nào), hoặc log group
    # chưa tồn tại. Cả hai đều nghĩa là không có lưới an toàn nào đã được kiểm
    # chứng, nên đây là ĐỎ chứ không phải "chưa có dữ liệu".
    row "cost guard" "CHƯA CHẠY LẦN NÀO" "-" "-" "-" "${C_RED}không có log stream nào${C_RESET} · ${sch_note}"
    echo "    ${C_DIM}Gọi tay một lần để chứng minh cả đường đi (miễn phí, và guard chỉ TẮT được thứ đang bật):${C_RESET}"
    echo "    ${C_DIM}aws lambda invoke --function-name ${HS_PROJECT}-cost-guard --log-type Tail --profile ${HS_PROFILE} --region ${HS_REGION} /dev/stdout${C_RESET}"
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
