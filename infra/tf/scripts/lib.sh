#!/usr/bin/env bash
# HushStore — thư viện chung cho up.sh / down.sh / status.sh / nuke.sh.
# File này KHÔNG chạy trực tiếp, chỉ được `source`.
#
# Ba thứ trong đây đáng đọc trước khi sửa:
#
# 1. hs_apply KHÔNG BAO GIỜ pipe terraform qua grep/tail. Đã bị lừa hai lần:
#    `terraform apply | grep ...` trả exit code của grep, nên một apply THẤT BẠI
#    hiện ra là thành công và mình đi tiếp trên một hạ tầng chưa đổi. Ở đây
#    terraform chạy nền, ghi thẳng ra file log, và exit code lấy bằng `wait $pid`.
#
# 2. Mọi biến đứng ngay trước dấu ':' phải viết ${VAR}. zsh (và cả bash trong
#    một số ngữ cảnh) hiểu `$A:role` là modifier `:r` và ăn mất ký tự — đã làm
#    hỏng cả một bảng kết quả IAM. Script này dùng bash, nhưng giữ quy ước.
#
# 3. Thời gian luôn lấy từ timestamp của AWS (CreatedTime, LaunchTime,
#    registeredAt...) chứ không phải từ đồng hồ nội bộ của script. Đóng/mở máy
#    tính giữa hai lần chạy vẫn ra số đúng.

set -euo pipefail

HS_PROFILE="${AWS_PROFILE:-hushstore}"
HS_REGION="${AWS_REGION:-ap-southeast-1}"
HS_PROJECT="${HS_PROJECT:-hushstore}"

HS_SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HS_TF_DIR="$(cd "${HS_SCRIPT_DIR}/../envs/prod" && pwd)"
HS_TFVARS="${HS_TF_DIR}/terraform.tfvars"

# Thư mục runtime: log apply và mốc thời gian cửa sổ làm việc. Gitignored.
HS_LOCAL_DIR="$(cd "${HS_SCRIPT_DIR}/.." && pwd)/.local"
HS_WINDOW_FILE="${HS_LOCAL_DIR}/window"

# Tên resource — đều dẫn xuất từ var.project nên suy ra được, không cần đọc
# state. Nhờ vậy status.sh chạy được cả khi terraform chưa init.
HS_CLUSTER="${HS_PROJECT}"
HS_ASG="${HS_PROJECT}-asg"
HS_CP="${HS_PROJECT}-cp"
HS_ALB="${HS_PROJECT}-alb"
HS_TG_WEB="${HS_PROJECT}-tg-web"
HS_TG_API="${HS_PROJECT}-tg-api"
HS_SVC_WEB="${HS_PROJECT}-web"
HS_SVC_API="${HS_PROJECT}-api"
HS_DB="${HS_PROJECT}-db-tf"

# Đơn giá ap-southeast-1, USD/giờ — GIÁ NIÊM YẾT, đo được từ Cost Explorer
# ngày 2026-08-20 (13.67 giờ uptime thật), không phải ước lượng từ bảng giá.
#
# RDS KHÔNG miễn phí như tưởng. db.t3.micro có baseline CPU 10%, mà SQL Server
# Express chạy không tải vẫn ngồi ở ~36% suốt (đo bằng CloudWatch: trung bình
# 38.8%, CPUCreditBalance = 0 phẳng cả cửa sổ, CPUSurplusCreditBalance leo tới
# 44.9). Phần vượt baseline bị tính vào usage type CPUCredits:db.t3, và nó tốn
# $0.0674/giờ — nhiều gấp đôi tiền instance hours, và xấp xỉ ĐÚNG BẰNG NAT + ALB
# cộng lại. RDS T3 không có cách tắt unlimited mode: khác EC2, cả
# create-db-instance lẫn modify-db-instance đều không có tham số credit nào.
#
# KHÔNG có free tier trên account này, VÀ credit cũng đã hết hạn (2026-08-23):
# account được đưa vào một Organization để dùng SSO, và việc đó chuyển nó sang
# chế độ trả phí. Nên mọi con số dưới đây là TIỀN RA KHỎI THẺ, không phải giá
# niêm yết được credit bù.
#
# Bằng chứng cho phần "không free tier", đo hồi còn đọc được Cost Explorer:
# APS1-InstanceUsage:db.t3.micro nằm ở RECORD_TYPE = Usage với đúng $0.031/giờ
# giá niêm yết — nếu còn 750h free tier thì dòng đó phải là $0.
HS_RATE_ALB=0.0252        # APS1, không phải $0.0225 của us-east-1
HS_RATE_NAT=0.0590        # APS1, không phải $0.045 của us-east-1
HS_RATE_EIP_IDLE=0.005
HS_RATE_EC2=0.0132        # t3.micro APS1
# ⚠️ HAI SỐ DƯỚI ĐÂY ĐO TRÊN sqlserver-ex/db.t3.micro/gp2 VÀ CỐ Ý CHƯA SỬA SAU
# KHI CHUYỂN POSTGRESQL (đợt 7). Chúng đáng tin CHÍNH VÌ là số đo, không phải số
# ước lượng — thay bằng một con số đoán là biến bảng chi phí từ "đã đo" thành
# "nghe hợp lý", mà không có gì đánh dấu sự khác nhau đó.
#
# 2/3 của $0.098 là CPU credit surplus, vì SQL Server Express ngồi ~36% CPU
# NGAY CẢ KHI KHÔNG TẢI trong khi baseline t3.micro là 10%. PostgreSQL idle thấp
# hơn nhiều, nên $0.098 giờ là CẬN TRÊN — bảng sẽ báo đắt hơn thực tế, tức lệch
# về phía an toàn.
#
# 🚨 T4g CŨNG chạy unlimited mode, credit surplus vẫn $0.075/vCPU-giờ. Khoản
# surplus chỉ biến mất NẾU PostgreSQL thật sự idle dưới baseline 10% — đó là giả
# thuyết, chưa phải phép đo. Chỉ sửa hai số này (và docs/terraform-runbook.md)
# sau khi vẽ CPUUtilization + CPUCreditBalance + CPUSurplusCreditBalance trên
# cùng cửa sổ VÀ Cost Explorer xác nhận sau 24-48h.
HS_RATE_RDS_UP=0.098      # instance $0.031 + CPU surplus $0.067 — đo trên SQL Server, xem cảnh báo trên
# 🔴 ĐÃ NHÂN ĐÔI VÌ MULTI-AZ. Storage được cấp phát ở CẢ HAI AZ và AWS tính tiền
# cả hai, kể cả khi instance đã stopped. Nghĩa là Multi-AZ nâng cái SÀN chi phí
# của dự án — khoảng +$2,3/tháng chạy vĩnh viễn, không tắt được bằng down.sh.
# Đây là cái giá thật của việc ghim enable_multi_az = true, và nó phải hiện ra
# ở đây chứ không nằm trong đầu ai đó.
# $0.004 là số ĐO ĐƯỢC cho 20GB single-AZ; ×2 là suy ra, chưa đối chiếu hoá đơn.
HS_RATE_RDS_STOPPED=0.008 # storage 20GB × 2 AZ — tính cả khi stopped

if [ -t 1 ] && [ -z "${NO_COLOR:-}" ]; then
  C_RESET=$'\033[0m'; C_DIM=$'\033[2m'; C_B=$'\033[1m'
  C_GREEN=$'\033[32m'; C_YELLOW=$'\033[33m'; C_RED=$'\033[31m'; C_CYAN=$'\033[36m'
else
  C_RESET=; C_DIM=; C_B=; C_GREEN=; C_YELLOW=; C_RED=; C_CYAN=
fi

AWSQ=(--profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager --output json)
AWSQT=(--profile "$HS_PROFILE" --region "$HS_REGION" --no-cli-pager --output text)

hs_die()  { echo "${C_RED}LỖI:${C_RESET} $*" >&2; exit 1; }
hs_warn() { echo "${C_YELLOW}!${C_RESET} $*" >&2; }
hs_ok()   { echo "${C_GREEN}✓${C_RESET} $*"; }
hs_info() { echo "${C_CYAN}·${C_RESET} $*"; }

hs_head() {
  echo
  echo "${C_B}$*${C_RESET}"
  echo "${C_DIM}────────────────────────────────────────────────────────────${C_RESET}"
}

# ── Thời gian ───────────────────────────────────────────────────
# hs_hms 3725 -> "1h 02m". Dưới 60 giây in ra giây để lúc chờ thấy nó nhích.
#
# Từ 24 giờ trở lên thì in ra NGÀY: đồng hồ ngược 7 ngày của RDS (mốc AWS tự
# start lại) từng render thành "150h 23m", và không ai đọc "150h" ra được "còn
# hơn 6 ngày" mà không phải chia trong đầu. Đó là con số duy nhất trên bảng mà
# người đọc phải ra quyết định từ nó, nên nó phải đọc được ngay.
hs_hms() {
  s=${1:-0}
  [ "$s" -lt 0 ] && s=0
  if [ "$s" -lt 60 ]; then printf '%ds' "$s"
  elif [ "$s" -lt 86400 ]; then printf '%dh %02dm' $((s / 3600)) $(((s % 3600) / 60))
  else printf '%dd %02dh' $((s / 86400)) $(((s % 86400) / 3600))
  fi
}

# hs_age_ms <epoch millis> -> số giây tính tới hiện tại. Rỗng/không phải số -> rỗng.
# CloudWatch Logs trả timestamp theo MILLI giây (lastEventTimestamp), khác mọi
# API khác trong script này vốn trả ISO8601 cho hs_age. Chia 1000 ở đây thay vì
# ở chỗ gọi để không ai phải nhớ API nào dùng đơn vị nào.
hs_age_ms() {
  ts="${1:-}"
  case "$ts" in ''|None|null|*[!0-9]*) echo ""; return 0 ;; esac
  echo $(( $(date +%s) - ts / 1000 ))
}

# hs_age <ISO8601> -> số giây tính tới hiện tại. Rỗng/None -> rỗng.
hs_age() {
  ts="${1:-}"
  case "$ts" in ''|None|null) return 0 ;; esac
  python3 - "$ts" <<'PY' 2>/dev/null || true
import sys, datetime
t = sys.argv[1].replace('Z', '+00:00')
try:
    d = datetime.datetime.fromisoformat(t)
except ValueError:
    sys.exit(0)
if d.tzinfo is None:
    d = d.replace(tzinfo=datetime.timezone.utc)
print(int((datetime.datetime.now(datetime.timezone.utc) - d).total_seconds()))
PY
}

# hs_cost <giây> <đơn giá/giờ> -> USD, 3 chữ số thập phân.
hs_cost() {
  awk -v s="${1:-0}" -v r="${2:-0}" 'BEGIN { printf "%.3f", s / 3600 * r }'
}

# ── tfvars ──────────────────────────────────────────────────────
hs_tfvar_get() {
  grep -E "^[[:space:]]*$1[[:space:]]*=" "$HS_TFVARS" 2>/dev/null \
    | head -1 | sed -E 's/^[^=]*=[[:space:]]*//' | tr -d '"' | awk '{print $1}'
}

# Đặt giá trị và ĐỌC LẠI để xác nhận. sed không báo lỗi khi pattern không khớp,
# nên không verify thì một lần đổi tên biến sẽ thành "apply mà không đổi gì".
hs_tfvar_set() {
  key="$1"; val="$2"
  grep -qE "^[[:space:]]*${key}[[:space:]]*=" "$HS_TFVARS" \
    || hs_die "không thấy biến '${key}' trong ${HS_TFVARS}"
  sed -i '' -E "s|^[[:space:]]*${key}[[:space:]]*=.*|${key} = ${val}|" "$HS_TFVARS"
  # `|| true` cùng lý do như trong hs_image_tag_check: hs_tfvar_get là một
  # pipeline mở đầu bằng grep, và file này bật `set -euo pipefail`. Nhánh này chỉ
  # tới được khi sed vừa rồi không khớp gì (key biến mất giữa hai lệnh, hoặc
  # tfvars bị ghi đè song song) — đúng cái ca mà hs_die bên dưới tồn tại để báo,
  # nhưng không có `|| true` thì -e giết script NGAY TẠI DÒNG GÁN và người dùng
  # không nhận được câu thông báo nào.
  cur="$(hs_tfvar_get "$key" || true)"
  [ "$cur" = "$val" ] || hs_die "đặt ${key} = ${val} không có tác dụng (đang là '${cur}')"
}

# ── terraform ───────────────────────────────────────────────────
hs_tf_check() {
  command -v terraform >/dev/null || hs_die "chưa cài terraform"
  command -v aws >/dev/null       || hs_die "chưa cài aws cli"
  command -v jq >/dev/null        || hs_die "chưa cài jq (brew install jq)"
  [ -d "${HS_TF_DIR}/.terraform" ] \
    || hs_die "terraform chưa init. Chạy: terraform -chdir=${HS_TF_DIR} init"
  [ -f "$HS_TFVARS" ] || hs_die "không thấy ${HS_TFVARS}"
}

hs_sso_check() {
  aws sts get-caller-identity "${AWSQ[@]}" >/dev/null 2>&1 \
    || hs_die "SSO session hết hạn. Chạy: aws sso login --profile ${HS_PROFILE}"
}

# hs_apply "<nhãn>" — chạy terraform apply, in tiến độ trực tiếp, trả exit code
# THẬT của terraform. Không pipe. Log đầy đủ nằm ở $HS_LAST_LOG.
# Có thay đổi nào đang chờ trên module.data (tức RDS) không?
#
# Cần biết vì apply chạm RDS sẽ gọi ModifyDBInstance, mà AWS từ chối lệnh đó
# khi instance đang `starting` hoặc `stopping` (InvalidDBInstanceState). up.sh
# cố tình apply NAT + ALB TRONG LÚC RDS đang lên để tiết kiệm ~4 phút; phép
# chồng việc đó an toàn với ECS (instance_count = 0 nên không có gì crash-loop)
# nhưng KHÔNG an toàn nếu chính lệnh apply lại đi sửa RDS.
#
# `-detailed-exitcode`: 0 = không có gì thay đổi, 2 = có, 1 = lỗi. Chỉ trả về
# "có" khi đúng mã 2 — lỗi (mã 1) không được im lặng thành "không có thay đổi",
# vì như vậy sẽ mở lại đúng cái bẫy hàm này tồn tại để đóng.
#
# ĐÂY LÀ HÀM VỊ TỪ: nó báo kết quả bằng exit code, nên PHẢI gọi trong một điều
# kiện (`if ...`, `&&`, `||`). Mọi script ở đây chạy `set -euo pipefail`, và gọi
# trực tiếp ngoài điều kiện thì lần trả về "không có thay đổi" (mã 1) sẽ giết
# luôn script — cùng lớp lỗi im lặng với mấy pipeline `hs_tfvar_get` đã phải vá
# bằng `|| true`. Trong `if` thì bash treo errexit nên trả 1 là an toàn.
hs_data_has_pending() {
  set +e
  terraform -chdir="$HS_TF_DIR" plan -input=false -lock-timeout=5m -no-color \
    -detailed-exitcode -target=module.data >/dev/null 2>&1
  rc=$?
  set -e
  case "$rc" in
    2) return 0 ;;
    0) return 1 ;;
    *) hs_warn "không kiểm được thay đổi chờ trên module.data (terraform plan mã ${rc}) — coi như CÓ để đi đường an toàn"
       return 0 ;;
  esac
}

hs_apply() {
  label="$1"
  mkdir -p "$HS_LOCAL_DIR/logs"
  HS_LAST_LOG="${HS_LOCAL_DIR}/logs/apply-$(date +%Y%m%d-%H%M%S).log"
  t0=$SECONDS

  terraform -chdir="$HS_TF_DIR" apply -auto-approve -input=false -lock-timeout=5m -no-color \
    >"$HS_LAST_LOG" 2>&1 &
  pid=$!

  while kill -0 "$pid" 2>/dev/null; do
    # 🚨 `|| true` Ở ĐÂY LÀ BẮT BUỘC, VÀ THIẾU NÓ ĐÃ GIẾT down.sh TRONG IM LẶNG.
    # File này bật `set -euo pipefail`. Khi apply KHÔNG có thay đổi nào,
    # terraform in "No changes." và log KHÔNG chứa một dòng tiến độ nào — grep
    # không khớp gì, trả 1, pipefail cho cả pipeline trả 1, và `set -e` giết
    # script NGAY TẠI DÒNG GÁN NÀY, khoảng 5 giây sau khi vòng lặp bắt đầu.
    # Không thông báo, không mã lỗi nào người dùng thấy được: down.sh in đúng
    # "BƯỚC 1/5" rồi trả về prompt như thể đã xong.
    #
    # Đây là lỗi TIỀM ẨN từ lâu, chỉ nổ khi mọi thứ đã tắt sẵn nên bước 1 thành
    # no-op — tức đúng lúc người ta chạy down.sh lần thứ hai cho chắc. Và cái nó
    # bỏ qua là BƯỚC 4: stop RDS. Nghĩa là chế độ lỗi của nó là "người dùng tin
    # đã tắt xong, trong khi RDS vẫn chạy và vẫn tính tiền".
    # (Đã đo: log của một apply "No changes" chứa 0 dòng khớp regex dưới đây.)
    last="$(grep -E 'Still (creating|destroying|modifying)|Creating\.\.\.|Destroying\.\.\.|Modifying\.\.\.|Creation complete|Destruction complete|Modifications complete' \
             "$HS_LAST_LOG" 2>/dev/null | tail -1 | cut -c1-72 || true)"
    printf '\r  %-74s' "$(hs_hms $((SECONDS - t0)))  ${last}"
    sleep 5
  done

  set +e; wait "$pid"; rc=$?; set -e
  printf '\r%-78s\r' ' '

  if [ "$rc" -ne 0 ]; then
    echo "${C_RED}✗${C_RESET} ${label} — thất bại sau $(hs_hms $((SECONDS - t0))))" >&2
    echo "${C_DIM}--- 25 dòng cuối của log ---${C_RESET}" >&2
    tail -25 "$HS_LAST_LOG" >&2
    echo "${C_DIM}Log đầy đủ: ${HS_LAST_LOG}${C_RESET}" >&2
    return "$rc"
  fi

  # Cùng lý do: nếu terraform đổi câu chữ ở phiên bản sau, grep trượt và script
  # chết im lặng NGAY SAU một apply đã THÀNH CÔNG — chế độ lỗi tệ nhất có thể.
  changes="$(grep -E '^Apply complete!' "$HS_LAST_LOG" | tail -1 || true)"
  hs_ok "${label} — $(hs_hms $((SECONDS - t0)))  ${C_DIM}${changes}${C_RESET}"
}

hs_tf_out() { terraform -chdir="$HS_TF_DIR" output -raw "$1" 2>/dev/null || true; }

# ── Số NAT theo CẤU HÌNH ĐÃ COMMIT, không theo tfvars ────────────
# 🚨 VÌ SAO KHÔNG DÙNG hs_tfvar_get: terraform.tfvars bị .gitignore (dòng 119
# `*.tfvars`), nên nó có thể không tồn tại trên máy này. Phiên bản trước viết
# `hs_tfvar_get nat_gateway_count || echo 1` — fallback đó IM LẶNG báo thiếu
# $0,059/giờ khi thực tế đang chạy 2 NAT. Nay đọc từ `terraform output`, tức từ
# chính cấu hình Terraform sắp/vừa áp.
#
# Trả về RỖNG khi không đọc được, và người gọi PHẢI xử lý ca rỗng bằng cách nói
# "không xác định được" — tuyệt đối không thay bằng một con số đoán. Một dòng
# tiền sai tệ hơn một dòng tiền thiếu.
# Hai nguồn, theo thứ tự: THỰC TẾ trước, Ý ĐỊNH sau. Cả hai đều là dữ liệu
# thật, không có bước nào đoán.
#   1. `terraform output` — phản ánh state, tức cấu hình vừa/sắp được áp.
#   2. default trong envs/prod/variables.tf — file ĐƯỢC COMMIT nên luôn tồn tại.
#      Cần bước này vì output chỉ vào state SAU lần apply đầu tiên kể từ khi nó
#      được thêm; thiếu nó thì mọi lần chạy trước lần apply đó mất dòng chi phí.
# Rỗng chỉ xảy ra khi cả hai đều không đọc được — lúc đó người gọi PHẢI nói
# "không xác định được", không được thay bằng một con số đoán.
hs_nat_count() {
  n="$(hs_tf_out nat_gateway_count)"
  if [ -z "$n" ]; then
    n="$(awk '/^variable "nat_gateway_count"/,/^}/' "$HS_TF_DIR/variables.tf" 2>/dev/null \
      | sed -nE 's/^[[:space:]]*default[[:space:]]*=[[:space:]]*([0-9]+).*/\1/p' | head -1)"
  fi
  case "$n" in
    '' | *[!0-9]*) echo "" ;;
    *) echo "$n" ;;
  esac
}

# Cùng khuôn cho Multi-AZ: state trước, default đã commit sau.
hs_multi_az() {
  v="$(hs_tf_out multi_az_enabled)"
  if [ -z "$v" ]; then
    v="$(awk '/^variable "enable_multi_az"/,/^}/' "$HS_TF_DIR/variables.tf" 2>/dev/null \
      | sed -nE 's/^[[:space:]]*default[[:space:]]*=[[:space:]]*(true|false).*/\1/p' | head -1)"
  fi
  echo "$v"
}

# ─── CẢNH BÁO TAG LỆCH (Phase 2) ─────────────────────────────────────────────
# Từ Phase 2, GitHub Actions push image lên ECR ở MỌI lần push vào main — kể cả
# khi hạ tầng đang tắt. Nhưng `image_tag` trong terraform.tfvars thì chỉ đổi khi
# có người sửa tay. Hệ quả: bật stack lên có thể đang chạy một image cũ hơn
# commit mới nhất, và không có gì trên màn hình nói ra điều đó.
#
# Đây đúng là loại nhầm lẫn tốn cả buổi: sửa bug, push, thấy Actions xanh, bật
# stack, rồi bug vẫn còn.
hs_image_tag_check() {
  local want newest
  # `|| true` là thứ làm guard bên dưới TỚI ĐƯỢC. hs_tfvar_get là một pipeline
  # mở đầu bằng grep, và file này bật `set -euo pipefail`: nếu tfvars không có
  # image_tag thì grep exit 1, pipefail lan ra, và -e giết up.sh NGAY TẠI DÒNG
  # GÁN — trước khi chạy tới `[ -n "$want" ]`. Tức là ý định "thiếu image_tag
  # thì bỏ qua cảnh báo" biến thành "up.sh chết không rõ lý do".
  want="$(hs_tfvar_get image_tag || true)"
  [ -n "$want" ] || return 0

  # Tag mới nhất theo thời điểm push, không theo thứ tự chữ cái — git SHA không
  # có thứ tự thời gian nào cả.
  newest="$(aws ecr describe-images --repository-name "${HS_PROJECT}-api"               "${AWSQ[@]}" 2>/dev/null             | jq -r '[.imageDetails[] | select(.imageTags != null)]
                     | sort_by(.imagePushedAt) | last | .imageTags[0] // ""' 2>/dev/null || true)"

  [ -n "$newest" ] && [ "$newest" != "null" ] || return 0

  if [ "$newest" != "$want" ]; then
    hs_warn "ECR có image mới hơn tag đang dùng:"
    hs_warn "    tfvars image_tag : ${want}"
    hs_warn "    mới nhất trên ECR: ${newest}"
    hs_warn "  Nếu muốn chạy bản mới nhất thì sửa tfvars TRƯỚC khi bật tiếp:"
    hs_warn "    sed -i '' 's|^image_tag = .*|image_tag = \"${newest}\"|' infra/tf/envs/prod/terraform.tfvars"
  fi
}

# ─── CẢNH BÁO REVISION ĐANG CHẠY LỆCH TFVARS ─────────────────────────────────
# hs_image_tag_check ở trên im lặng đúng ở ca gây nhầm lẫn nhất: stack ĐANG BẬT,
# người ta sửa image_tag thành SHA mới nhất trên ECR rồi chạy lại up.sh. Hai giá
# trị khớp nhau nên phép so kia không nói gì; `apply` ĐĂNG KÝ một revision mới;
# rồi `ignore_changes = [task_definition]` trên aws_ecs_service giữ service ở
# revision CŨ. Kết quả: tfvars nói một đằng, container đang phục vụ một nẻo, và
# không có dòng nào trên màn hình nói ra điều đó.
#
# Nên khi service tồn tại thì phải so image của REVISION ĐANG CHẠY với tfvars,
# không phải so tag mới nhất của ECR với tfvars. Toàn bộ hàm chỉ đọc
# (DescribeServices + DescribeTaskDefinition) và chạy bằng credential SSO của
# người dùng, nên không cần đổi IAM.
hs_running_image_check() {
  local want svc td img running
  want="$(hs_tfvar_get image_tag || true)"
  [ -n "$want" ] || return 0

  for svc in "$HS_SVC_API" "$HS_SVC_WEB"; do
    # Lọc status == ACTIVE: describe-services CÒN trả về service đã xoá với
    # status INACTIVE một khoảng thời gian sau đó, và revision của một service
    # INACTIVE không nói gì về cái đang chạy.
    td="$(aws ecs describe-services --cluster "$HS_CLUSTER" --services "$svc" \
            --query 'services[?status==`ACTIVE`].taskDefinition | [0]' \
            "${AWSQT[@]}" 2>/dev/null || true)"
    case "$td" in ''|None|null) continue ;; esac

    img="$(aws ecs describe-task-definition --task-definition "$td" \
             --query 'taskDefinition.containerDefinitions[0].image' \
             "${AWSQT[@]}" 2>/dev/null || true)"
    case "$img" in ''|None|null) continue ;; esac

    # Tag là phần sau dấu ':' CUỐI CÙNG, nên dùng `##*:`. URL của ECR chứa nhiều
    # dấu ':' nếu có port, và `#*:` sẽ cắt sai chỗ.
    running="${img##*:}"
    [ "$running" != "$want" ] || continue

    hs_warn "${svc} đang chạy image tag khác tfvars:"
    hs_warn "    revision đang chạy: ${running}   (${td##*/})"
    hs_warn "    tfvars image_tag  : ${want}"
    hs_warn "  apply sẽ ĐĂNG KÝ revision mới nhưng KHÔNG trỏ service sang —"
    hs_warn "  ignore_changes = [task_definition] là cố ý (nếu thiếu, mỗi apply là"
    hs_warn "  một lần rollback ngầm bản do CI deploy). Muốn đổi thật thì trỏ tay:"
    hs_warn "    aws ecs update-service --cluster ${HS_CLUSTER} --service ${svc} --task-definition ${svc} --profile ${HS_PROFILE}"
  done
}

# ── Chờ có tiến độ nhìn thấy được ───────────────────────────────
# hs_wait_until "<nhãn>" <timeout> <gợi ý thường mất> <lệnh kiểm tra...>
# Lệnh kiểm tra trả 0 = xong. In ra đồng hồ đếm lên để biết nó còn sống.
hs_wait_until() {
  label="$1"; timeout="$2"; hint="$3"; shift 3
  t0=$SECONDS
  while :; do
    if "$@" >/dev/null 2>&1; then
      printf '\r%-78s\r' ' '
      hs_ok "${label} — $(hs_hms $((SECONDS - t0)))"
      return 0
    fi
    if [ $((SECONDS - t0)) -ge "$timeout" ]; then
      printf '\r%-78s\r' ' '
      hs_warn "${label} — HẾT THỜI GIAN CHỜ sau $(hs_hms "$timeout")"
      return 1
    fi
    printf '\r  %-74s' "$(hs_hms $((SECONDS - t0)))  ${label}  ${C_DIM}(thường ${hint})${C_RESET}"
    sleep 10
  done
}

# ── Mốc cửa sổ tính phí ─────────────────────────────────────────
hs_window_open() {
  mkdir -p "$HS_LOCAL_DIR"
  date +%s > "$HS_WINDOW_FILE"
}

hs_window_seconds() {
  [ -f "$HS_WINDOW_FILE" ] || { echo ""; return 0; }
  start="$(cat "$HS_WINDOW_FILE" 2>/dev/null || echo)"
  case "$start" in ''|*[!0-9]*) echo ""; return 0 ;; esac
  echo $(( $(date +%s) - start ))
}

hs_window_close() { rm -f "$HS_WINDOW_FILE"; }

# ── Chi phí THẬT của những gì ĐANG SỐNG, đo tại thời điểm gọi ────
#
# 🚨 VÌ SAO KHÔNG NHÂN "CỬA SỔ × ĐƠN GIÁ CẢ STACK" — cách cũ in ra $75.50 CHO
# MỘT CỬA SỔ TỐN VÀI XU. Bản trước của down.sh dùng `hs_cost "$WINDOW" 0.2544`,
# tức thời gian kể từ lần up.sh gần nhất nhân đơn giá TOÀN BỘ stack. Cả hai thừa
# số đều sai:
#   · Cửa sổ KHÔNG phải thời gian tính tiền. Marker chỉ bị xoá ở DÒNG CUỐI của
#     down.sh, nên mỗi lần down.sh chết giữa chừng là nó sống thêm. Đo được: nó
#     tích 12 ngày 8 giờ trong khi stack tắt gần như suốt — và nó tích được đúng
#     vì down.sh đang chết im lặng ở bước 1 (lỗi pipefail trong hs_apply).
#     Hai lỗi cộng hưởng: cái này làm cái kia trông tệ gấp trăm lần.
#   · Đơn giá gộp cả 2 NAT + ALB + EC2, mà trong cửa sổ đó KHÔNG cái nào từng
#     tồn tại (enable_nat=false, enable_alb=false, instance_count=0 suốt).
#
# Dòng cũ có ghi "chặn trên", và về mặt kỹ thuật thì đúng. Nhưng một chặn trên
# cao gấp trăm lần không phải thông tin — nó là tiếng ồn, và tiếng ồn ở dòng
# tiền thì lần sau không ai đọc nữa, kể cả khi nó báo đúng.
#
# Hàm này đo TỪNG resource theo tuổi THẬT của chính nó, cùng cách status.sh làm.
# PHẢI gọi TRƯỚC khi destroy — sau đó không còn gì để hỏi.
hs_spend_now() {
  pairs=""

  albt="$(aws elbv2 describe-load-balancers --query 'LoadBalancers[0].CreatedTime' "${AWSQT[@]}" 2>/dev/null || echo None)"
  # ⚠️ DÙNG `if`, KHÔNG DÙNG `[ ... ] && ...`. Đã đo: một AND-list thất bại mà
  # đứng CUỐI một hàm sẽ làm hàm trả 1, và dưới `set -e` điều đó giết script gọi
  # nó — không in gì. Nó chỉ sống sót khi tình cờ có lệnh khác đứng sau. Đó đúng
  # là loại phụ thuộc ngầm vừa làm down.sh chết im lặng ở hs_apply, nên ở đây
  # viết dài hơn vài dòng để không phải nhớ luật đó nữa.
  a="$(hs_age "$albt")"
  if [ -n "${a:-}" ]; then pairs="${pairs}${a} ${HS_RATE_ALB}
"; fi

  # Mỗi NAT tính riêng — 2 NAT là hai dòng, không phải một dòng nhân đôi.
  for t in $(aws ec2 describe-nat-gateways --filter "Name=state,Values=pending,available" \
      --query 'NatGateways[].CreateTime' "${AWSQT[@]}" 2>/dev/null || true); do
    a="$(hs_age "$t")"
    if [ -n "${a:-}" ]; then pairs="${pairs}${a} ${HS_RATE_NAT}
"; fi
  done

  for t in $(aws ec2 describe-instances \
      --filters "Name=tag:Project,Values=${HS_PROJECT}" "Name=instance-state-name,Values=running" \
      --query 'Reservations[].Instances[].LaunchTime' "${AWSQT[@]}" 2>/dev/null || true); do
    a="$(hs_age "$t")"
    if [ -n "${a:-}" ]; then pairs="${pairs}${a} ${HS_RATE_EC2}
"; fi
  done

  # RDS chỉ tính khi `available`. Mốc bắt đầu lấy CÁI MUỘN HƠN giữa
  # InstanceCreateTime và sự kiện start gần nhất — cùng lý do đã vá ở status.sh:
  # identifier được tái sử dụng nên describe-events còn trả lịch sử của instance
  # đã bị xoá.
  rst="$(aws rds describe-db-instances --db-instance-identifier "$HS_DB" \
    --query 'DBInstances[0].DBInstanceStatus' "${AWSQT[@]}" 2>/dev/null || echo none)"
  if [ "$rst" = "available" ]; then
    rcrt="$(aws rds describe-db-instances --db-instance-identifier "$HS_DB" \
      --query 'DBInstances[0].InstanceCreateTime' "${AWSQT[@]}" 2>/dev/null || echo None)"
    rev="$(aws rds describe-events --source-identifier "$HS_DB" --source-type db-instance \
      --duration 20160 --query 'Events[?Message==`DB instance started`].Date | [-1]' \
      "${AWSQT[@]}" 2>/dev/null || echo None)"
    a1="$(hs_age "$rcrt")"; a2="$(hs_age "$rev")"
    a="${a1:-}"
    if [ -n "${a2:-}" ] && { [ -z "${a:-}" ] || [ "$a2" -lt "$a" ]; }; then a="$a2"; fi
    if [ -n "${a:-}" ]; then pairs="${pairs}${a} ${HS_RATE_RDS_UP}
"; fi
  fi

  printf '%s' "$pairs" | awk '{ t += $1 / 3600 * $2 } END { printf "%.4f", t + 0 }'
}
