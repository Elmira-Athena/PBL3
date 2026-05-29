#!/usr/bin/env bash
# =============================================================
# HushStore — Stop EC2 + RDS
#
# Usage: bash infra/stop.sh
# Lưu ý: IP EC2 thay đổi khi Start lại. RDS tự bật sau 7 ngày.
# =============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
RESOURCES_FILE="$SCRIPT_DIR/../resources.env"

[[ ! -f "$RESOURCES_FILE" ]] && echo "ERROR: $RESOURCES_FILE không tồn tại." && exit 1

# shellcheck source=/dev/null
source "$RESOURCES_FILE"
AWS="aws --region $REGION --profile hushstore --no-cli-pager"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  HushStore — Stop  |  EC2: $EC2_ID  RDS: $RDS_ID"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# ─── EC2 ──────────────────────────────────────────────────────
EC2_STATE=$($AWS ec2 describe-instances \
  --instance-ids "$EC2_ID" \
  --query 'Reservations[0].Instances[0].State.Name' --output text)

if [[ "$EC2_STATE" == "stopped" ]]; then
  echo "[1/2] EC2 đã stopped."
else
  echo "[1/2] Stopping EC2..."
  $AWS ec2 stop-instances --instance-ids "$EC2_ID" > /dev/null
  $AWS ec2 wait instance-stopped --instance-ids "$EC2_ID"
  echo "  Done."
fi

# ─── RDS ──────────────────────────────────────────────────────
RDS_STATE=$($AWS rds describe-db-instances \
  --db-instance-identifier "$RDS_ID" \
  --query 'DBInstances[0].DBInstanceStatus' --output text)

if [[ "$RDS_STATE" == "stopped" ]]; then
  echo "[2/2] RDS đã stopped."
else
  echo "[2/2] Stopping RDS (đợi ~5 phút)..."
  $AWS rds stop-db-instance --db-instance-identifier "$RDS_ID" > /dev/null
  until [[ "$($AWS rds describe-db-instances \
      --db-instance-identifier "$RDS_ID" \
      --query 'DBInstances[0].DBInstanceStatus' --output text)" == "stopped" ]]; do
    printf "  ."; sleep 30
  done
  echo ""
  echo "  Done."
fi

echo ""
echo "✓ Hệ thống đã tắt. IP EC2 sẽ thay đổi khi start lại."
echo "  Để bật lại: bash infra/start.sh"
