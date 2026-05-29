#!/usr/bin/env bash
# =============================================================
# HushStore — Start EC2 + RDS
#
# Usage: bash infra/start.sh
# Sau khi chạy: cập nhật Cloudflare DNS nếu IP thay đổi.
# =============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
RESOURCES_FILE="$SCRIPT_DIR/../resources.env"

[[ ! -f "$RESOURCES_FILE" ]] && echo "ERROR: $RESOURCES_FILE không tồn tại." && exit 1

# shellcheck source=/dev/null
source "$RESOURCES_FILE"
AWS="aws --region $REGION --profile hushstore --no-cli-pager"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  HushStore — Start  |  EC2: $EC2_ID  RDS: $RDS_ID"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# ─── RDS ──────────────────────────────────────────────────────
RDS_STATE=$($AWS rds describe-db-instances \
  --db-instance-identifier "$RDS_ID" \
  --query 'DBInstances[0].DBInstanceStatus' --output text)

if [[ "$RDS_STATE" == "available" ]]; then
  echo "[1/2] RDS đã running."
else
  echo "[1/2] Starting RDS (đợi ~5 phút)..."
  $AWS rds start-db-instance --db-instance-identifier "$RDS_ID" > /dev/null
  $AWS rds wait db-instance-available --db-instance-identifier "$RDS_ID"
  echo "  Done."
fi

# ─── EC2 ──────────────────────────────────────────────────────
EC2_STATE=$($AWS ec2 describe-instances \
  --instance-ids "$EC2_ID" \
  --query 'Reservations[0].Instances[0].State.Name' --output text)

if [[ "$EC2_STATE" == "running" ]]; then
  echo "[2/2] EC2 đã running."
else
  echo "[2/2] Starting EC2..."
  $AWS ec2 start-instances --instance-ids "$EC2_ID" > /dev/null
  $AWS ec2 wait instance-running --instance-ids "$EC2_ID"
  echo "  Done."
fi

# ─── IP MỚI ───────────────────────────────────────────────────
NEW_IP=$($AWS ec2 describe-instances \
  --instance-ids "$EC2_ID" \
  --query 'Reservations[0].Instances[0].PublicIpAddress' --output text)

OLD_IP="$PUBLIC_IP"
sed -i.bak "s/^PUBLIC_IP=.*/PUBLIC_IP=$NEW_IP/" "$RESOURCES_FILE"
rm -f "${RESOURCES_FILE}.bak"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  ✓ Hệ thống đã bật."
echo ""
if [[ "$NEW_IP" != "$OLD_IP" ]]; then
  echo "  IP thay đổi: $OLD_IP → $NEW_IP"
  echo ""
  echo "  Cập nhật Cloudflare DNS:"
  echo "    A  @    → $NEW_IP"
  echo "    A  api  → $NEW_IP"
else
  echo "  IP không đổi: $NEW_IP"
fi
echo ""
echo "  SSH: ssh -i ${KEY_FILE:-~/.ssh/hushstore-key.pem} ubuntu@$NEW_IP"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
