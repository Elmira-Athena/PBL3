#!/usr/bin/env bash
# =============================================================
# HushStore — AWS Infrastructure Teardown (complete)
# Xóa toàn bộ resources kể cả key pair
#
# Usage: bash infra/teardown.sh
# =============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
RESOURCES_FILE="$SCRIPT_DIR/../resources.env"

if [[ ! -f "$RESOURCES_FILE" ]]; then
  echo "ERROR: $RESOURCES_FILE không tồn tại. Không có gì để xóa."
  exit 1
fi

# shellcheck source=/dev/null
source "$RESOURCES_FILE"
AWS="aws --region $REGION --profile hushstore --no-cli-pager"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  HushStore — Teardown"
echo ""
echo "  Sẽ xóa TOÀN BỘ:"
echo "  · EC2:       $EC2_ID  ($PUBLIC_IP)"
echo "  · RDS:       $RDS_ID"
echo "  · VPC:       $VPC_ID (subnets, SGs, RTs, IGW)"
echo "  · Key Pair:  $KEY_PAIR_NAME  ($KEY_FILE)"
echo "  · Budget:    $BUDGET_NAME"
echo ""
read -rp "  Gõ 'yes' để xác nhận: " CONFIRM
[[ "$CONFIRM" != "yes" ]] && echo "Cancelled." && exit 0
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# ─── 1. BUDGET ────────────────────────────────────────────────
echo "[1/11] Deleting Budget..."
aws budgets delete-budget \
  --account-id "$ACCOUNT_ID" \
  --budget-name "$BUDGET_NAME" \
  --profile hushstore --no-cli-pager 2>/dev/null \
  && echo "  Done." || echo "  (không tồn tại, bỏ qua)"

# ─── 2. EC2 ───────────────────────────────────────────────────
echo "[2/11] Terminating EC2..."
$AWS ec2 terminate-instances --instance-ids "$EC2_ID" > /dev/null
echo "  Waiting..."
$AWS ec2 wait instance-terminated --instance-ids "$EC2_ID"
echo "  Done. (EBS xóa tự động)"

# ─── 3. RDS ───────────────────────────────────────────────────
echo "[3/11] Deleting RDS (no final snapshot, đợi ~10 phút)..."
$AWS rds delete-db-instance \
  --db-instance-identifier "$RDS_ID" \
  --skip-final-snapshot > /dev/null
$AWS rds wait db-instance-deleted --db-instance-identifier "$RDS_ID"
echo "  Done."

# ─── 4. DB SUBNET GROUP ───────────────────────────────────────
echo "[4/11] Deleting DB Subnet Group..."
$AWS rds delete-db-subnet-group \
  --db-subnet-group-name "$PROJECT-db-subnet-group"
echo "  Done."

# ─── 5. SECURITY GROUPS ───────────────────────────────────────
echo "[5/11] Deleting Security Groups..."
$AWS ec2 delete-security-group --group-id "$SG_RDS"
$AWS ec2 delete-security-group --group-id "$SG_EC2"
echo "  Done."

# ─── 6. ROUTE TABLES ──────────────────────────────────────────
echo "[6/11] Deleting Route Tables..."
for ASSOC in "$RT_PUBLIC_ASSOC_A" "$RT_PUBLIC_ASSOC_B" \
             "$RT_PRIVATE_ASSOC_A" "$RT_PRIVATE_ASSOC_B"; do
  $AWS ec2 disassociate-route-table --association-id "$ASSOC" 2>/dev/null || true
done
$AWS ec2 delete-route-table --route-table-id "$RT_PUBLIC"
$AWS ec2 delete-route-table --route-table-id "$RT_PRIVATE"
echo "  Done."

# ─── 7. SUBNETS ───────────────────────────────────────────────
echo "[7/11] Deleting Subnets..."
for S in "$SUBNET_PUBLIC_A" "$SUBNET_PUBLIC_B" \
         "$SUBNET_PRIVATE_A" "$SUBNET_PRIVATE_B"; do
  $AWS ec2 delete-subnet --subnet-id "$S"
done
echo "  Done."

# ─── 8. INTERNET GATEWAY ──────────────────────────────────────
echo "[8/11] Detaching and deleting IGW..."
$AWS ec2 detach-internet-gateway \
  --internet-gateway-id "$IGW_ID" --vpc-id "$VPC_ID"
$AWS ec2 delete-internet-gateway --internet-gateway-id "$IGW_ID"
echo "  Done."

# ─── 9. VPC ───────────────────────────────────────────────────
echo "[9/11] Deleting VPC..."
$AWS ec2 delete-vpc --vpc-id "$VPC_ID"
echo "  Done."

# ─── 10. KEY PAIR ─────────────────────────────────────────────
echo "[10/11] Deleting Key Pair..."
$AWS ec2 delete-key-pair --key-name "$KEY_PAIR_NAME"
if [[ -f "$KEY_FILE" ]]; then
  rm -f "$KEY_FILE"
  echo "  Deleted: $KEY_FILE"
fi
echo "  Done."

# ─── 11. ARCHIVE ──────────────────────────────────────────────
echo "[11/11] Archiving resources file..."
ARCHIVE="$SCRIPT_DIR/../resources-deleted-$(date '+%Y%m%d-%H%M%S').env"
mv "$RESOURCES_FILE" "$ARCHIVE"
echo "  Archived → $ARCHIVE"

echo ""
echo "✓ Toàn bộ AWS resources đã được xóa."
