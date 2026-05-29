#!/usr/bin/env bash
# =============================================================
# HushStore — AWS Infrastructure Setup (complete)
#
# Usage: bash infra/setup.sh
# Requires: AWS CLI v2, jq, config.json điền đủ
# =============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CONFIG_FILE="$SCRIPT_DIR/../config.json"
RESOURCES_FILE="$SCRIPT_DIR/../resources.env"

# ─── ĐỌC CONFIG ───────────────────────────────────────────────
if [[ ! -f "$CONFIG_FILE" ]]; then
  echo "ERROR: $CONFIG_FILE không tồn tại."
  echo "  cp devops/config.example.json devops/config.json && nano devops/config.json"
  exit 1
fi

cfg()  { jq -r ".$1" "$CONFIG_FILE"; }

PROJECT=$(cfg project)
REGION=$(cfg region)
AZ1="${REGION}a"
AZ2="${REGION}b"
KEY_PAIR_NAME=$(cfg keyPairName)
DB_USERNAME=$(cfg dbUsername)
DB_PASSWORD=$(cfg dbPassword)
ALERT_EMAIL=$(cfg alertEmail)
KEY_FILE="$HOME/.ssh/$KEY_PAIR_NAME.pem"

# ─── VALIDATE ─────────────────────────────────────────────────
if [[ "$DB_PASSWORD" == "REPLACE_ME"* ]]; then
  echo "ERROR: Chưa đặt dbPassword trong $CONFIG_FILE"; exit 1
fi

if [[ -f "$RESOURCES_FILE" ]]; then
  echo "ERROR: $RESOURCES_FILE đã tồn tại — infrastructure đã được tạo."
  echo "  Chạy teardown.sh trước nếu muốn tạo lại."
  exit 1
fi

AWS="aws --region $REGION --profile hushstore --no-cli-pager"
TAGS="Key=Project,Value=$PROJECT"

ACCOUNT_ID=$(aws sts get-caller-identity \
  --profile hushstore --no-cli-pager \
  --query 'Account' --output text)

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  HushStore — AWS Infrastructure Setup"
echo "  Account: $ACCOUNT_ID  |  Region: $REGION"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# ─── 1. KEY PAIR ──────────────────────────────────────────────
echo "[1/11] Key Pair..."
if $AWS ec2 describe-key-pairs --key-names "$KEY_PAIR_NAME" &>/dev/null; then
  if [[ ! -f "$KEY_FILE" ]]; then
    echo "  ERROR: Key pair '$KEY_PAIR_NAME' tồn tại trên AWS nhưng không có $KEY_FILE"
    echo "  Xóa key pair trên AWS rồi chạy lại, hoặc đặt file .pem vào $KEY_FILE"
    exit 1
  fi
  echo "  Key pair '$KEY_PAIR_NAME' đã tồn tại, dùng lại."
else
  $AWS ec2 create-key-pair \
    --key-name "$KEY_PAIR_NAME" \
    --tag-specifications "ResourceType=key-pair,Tags=[{$TAGS},{Key=Name,Value=$KEY_PAIR_NAME}]" \
    --query 'KeyMaterial' --output text > "$KEY_FILE"
  chmod 400 "$KEY_FILE"
  echo "  Created → $KEY_FILE"
fi

# ─── 2. VPC ───────────────────────────────────────────────────
echo "[2/11] VPC..."
VPC_ID=$($AWS ec2 create-vpc \
  --cidr-block 10.0.0.0/16 \
  --tag-specifications "ResourceType=vpc,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-vpc}]" \
  --query 'Vpc.VpcId' --output text)
$AWS ec2 modify-vpc-attribute --vpc-id "$VPC_ID" --enable-dns-support    '{"Value":true}'
$AWS ec2 modify-vpc-attribute --vpc-id "$VPC_ID" --enable-dns-hostnames  '{"Value":true}'
echo "  $VPC_ID"

# ─── 3. INTERNET GATEWAY ──────────────────────────────────────
echo "[3/11] Internet Gateway..."
IGW_ID=$($AWS ec2 create-internet-gateway \
  --tag-specifications "ResourceType=internet-gateway,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-igw}]" \
  --query 'InternetGateway.InternetGatewayId' --output text)
$AWS ec2 attach-internet-gateway --internet-gateway-id "$IGW_ID" --vpc-id "$VPC_ID"
echo "  $IGW_ID → $VPC_ID"

# ─── 4. SUBNETS ───────────────────────────────────────────────
echo "[4/11] Subnets (2 public + 2 private)..."
SUBNET_PUBLIC_A=$($AWS ec2 create-subnet \
  --vpc-id "$VPC_ID" --cidr-block 10.0.0.0/24 --availability-zone "$AZ1" \
  --tag-specifications "ResourceType=subnet,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-public-a}]" \
  --query 'Subnet.SubnetId' --output text)
$AWS ec2 modify-subnet-attribute --subnet-id "$SUBNET_PUBLIC_A" --map-public-ip-on-launch

SUBNET_PUBLIC_B=$($AWS ec2 create-subnet \
  --vpc-id "$VPC_ID" --cidr-block 10.0.1.0/24 --availability-zone "$AZ2" \
  --tag-specifications "ResourceType=subnet,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-public-b}]" \
  --query 'Subnet.SubnetId' --output text)
$AWS ec2 modify-subnet-attribute --subnet-id "$SUBNET_PUBLIC_B" --map-public-ip-on-launch

SUBNET_PRIVATE_A=$($AWS ec2 create-subnet \
  --vpc-id "$VPC_ID" --cidr-block 10.0.10.0/24 --availability-zone "$AZ1" \
  --tag-specifications "ResourceType=subnet,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-private-a}]" \
  --query 'Subnet.SubnetId' --output text)

SUBNET_PRIVATE_B=$($AWS ec2 create-subnet \
  --vpc-id "$VPC_ID" --cidr-block 10.0.11.0/24 --availability-zone "$AZ2" \
  --tag-specifications "ResourceType=subnet,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-private-b}]" \
  --query 'Subnet.SubnetId' --output text)

echo "  public-a=$SUBNET_PUBLIC_A ($AZ1)  public-b=$SUBNET_PUBLIC_B ($AZ2)"
echo "  private-a=$SUBNET_PRIVATE_A ($AZ1)  private-b=$SUBNET_PRIVATE_B ($AZ2)"

# ─── 5. ROUTE TABLES ──────────────────────────────────────────
echo "[5/11] Route Tables..."
RT_PUBLIC=$($AWS ec2 create-route-table \
  --vpc-id "$VPC_ID" \
  --tag-specifications "ResourceType=route-table,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-rt-public}]" \
  --query 'RouteTable.RouteTableId' --output text)
$AWS ec2 create-route \
  --route-table-id "$RT_PUBLIC" \
  --destination-cidr-block 0.0.0.0/0 --gateway-id "$IGW_ID" > /dev/null
RT_PUBLIC_ASSOC_A=$($AWS ec2 associate-route-table \
  --route-table-id "$RT_PUBLIC" --subnet-id "$SUBNET_PUBLIC_A" \
  --query 'AssociationId' --output text)
RT_PUBLIC_ASSOC_B=$($AWS ec2 associate-route-table \
  --route-table-id "$RT_PUBLIC" --subnet-id "$SUBNET_PUBLIC_B" \
  --query 'AssociationId' --output text)

RT_PRIVATE=$($AWS ec2 create-route-table \
  --vpc-id "$VPC_ID" \
  --tag-specifications "ResourceType=route-table,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-rt-private}]" \
  --query 'RouteTable.RouteTableId' --output text)
RT_PRIVATE_ASSOC_A=$($AWS ec2 associate-route-table \
  --route-table-id "$RT_PRIVATE" --subnet-id "$SUBNET_PRIVATE_A" \
  --query 'AssociationId' --output text)
RT_PRIVATE_ASSOC_B=$($AWS ec2 associate-route-table \
  --route-table-id "$RT_PRIVATE" --subnet-id "$SUBNET_PRIVATE_B" \
  --query 'AssociationId' --output text)
echo "  public=$RT_PUBLIC (→ IGW)  private=$RT_PRIVATE (local only)"

# ─── 6. SECURITY GROUPS ───────────────────────────────────────
echo "[6/11] Security Groups..."
SG_EC2=$($AWS ec2 create-security-group \
  --group-name "$PROJECT-ec2-sg" \
  --description "HushStore EC2: nginx + Docker API" \
  --vpc-id "$VPC_ID" \
  --tag-specifications "ResourceType=security-group,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-ec2-sg}]" \
  --query 'GroupId' --output text)
MY_IP=$(curl -sf https://checkip.amazonaws.com)
$AWS ec2 authorize-security-group-ingress --group-id "$SG_EC2" \
  --protocol tcp --port 22  --cidr "${MY_IP}/32" > /dev/null
$AWS ec2 authorize-security-group-ingress --group-id "$SG_EC2" \
  --protocol tcp --port 80  --cidr 0.0.0.0/0   > /dev/null
$AWS ec2 authorize-security-group-ingress --group-id "$SG_EC2" \
  --protocol tcp --port 443 --cidr 0.0.0.0/0   > /dev/null

SG_RDS=$($AWS ec2 create-security-group \
  --group-name "$PROJECT-rds-sg" \
  --description "HushStore RDS: port 1433, EC2 only" \
  --vpc-id "$VPC_ID" \
  --tag-specifications "ResourceType=security-group,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-rds-sg}]" \
  --query 'GroupId' --output text)
$AWS ec2 authorize-security-group-ingress --group-id "$SG_RDS" \
  --protocol tcp --port 1433 --source-group "$SG_EC2" > /dev/null
echo "  ec2=$SG_EC2  rds=$SG_RDS (1433 from EC2 only)"

# ─── 7. DB SUBNET GROUP ───────────────────────────────────────
echo "[7/11] DB Subnet Group..."
$AWS rds create-db-subnet-group \
  --db-subnet-group-name "$PROJECT-db-subnet-group" \
  --db-subnet-group-description "HushStore RDS - private subnets AZ1+AZ2" \
  --subnet-ids "$SUBNET_PRIVATE_A" "$SUBNET_PRIVATE_B" \
  --tags "Key=Project,Value=$PROJECT" > /dev/null
echo "  $PROJECT-db-subnet-group"

# ─── 8. RDS ───────────────────────────────────────────────────
echo "[8/11] RDS SQL Server Express (đợi ~10 phút)..."
RDS_ID="$PROJECT-db"
RDS_ENGINE_VERSION=$($AWS rds describe-db-engine-versions \
  --engine sqlserver-ex \
  --query 'sort_by(DBEngineVersions, &EngineVersion)[-1].EngineVersion' \
  --output text)
$AWS rds create-db-instance \
  --db-instance-identifier "$RDS_ID" \
  --db-instance-class db.t3.micro \
  --engine sqlserver-ex \
  --engine-version "$RDS_ENGINE_VERSION" \
  --master-username "$DB_USERNAME" \
  --master-user-password "$DB_PASSWORD" \
  --allocated-storage 20 \
  --storage-type gp2 \
  --license-model license-included \
  --vpc-security-group-ids "$SG_RDS" \
  --db-subnet-group-name "$PROJECT-db-subnet-group" \
  --availability-zone "$AZ1" \
  --no-multi-az \
  --no-publicly-accessible \
  --auto-minor-version-upgrade \
  --backup-retention-period 7 \
  --tags "Key=Project,Value=$PROJECT" "Key=Name,Value=$RDS_ID" > /dev/null
$AWS rds wait db-instance-available --db-instance-identifier "$RDS_ID"
RDS_ENDPOINT=$($AWS rds describe-db-instances \
  --db-instance-identifier "$RDS_ID" \
  --query 'DBInstances[0].Endpoint.Address' --output text)
echo "  $RDS_ID → $RDS_ENDPOINT"

# ─── 9. EC2 ───────────────────────────────────────────────────
echo "[9/11] EC2 (t3.micro, no EIP)..."
AMI_ID=$($AWS ec2 describe-images \
  --owners 099720109477 \
  --filters \
    "Name=name,Values=ubuntu/images/hvm-ssd/ubuntu-jammy-22.04-amd64-server-*" \
    "Name=state,Values=available" \
  --query 'sort_by(Images, &CreationDate)[-1].ImageId' --output text)
EC2_ID=$($AWS ec2 run-instances \
  --image-id "$AMI_ID" \
  --instance-type t3.micro \
  --key-name "$KEY_PAIR_NAME" \
  --subnet-id "$SUBNET_PUBLIC_A" \
  --security-group-ids "$SG_EC2" \
  --block-device-mappings \
    "DeviceName=/dev/sda1,Ebs={VolumeSize=20,VolumeType=gp2,DeleteOnTermination=true}" \
  --tag-specifications \
    "ResourceType=instance,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-server}]" \
    "ResourceType=volume,Tags=[{$TAGS},{Key=Name,Value=$PROJECT-server-vol}]" \
  --query 'Instances[0].InstanceId' --output text)
$AWS ec2 wait instance-running --instance-ids "$EC2_ID"
PUBLIC_IP=$($AWS ec2 describe-instances \
  --instance-ids "$EC2_ID" \
  --query 'Reservations[0].Instances[0].PublicIpAddress' --output text)
echo "  $EC2_ID  IP=$PUBLIC_IP"
echo "  CAUTION: IP thay đổi nếu Stop rồi Start — dùng Reboot thay thế."

# ─── 10. BUDGET ALERTS ────────────────────────────────────────
echo "[10/11] Budget alerts (\$1/\$2/\$5/\$10 → $ALERT_EMAIL)..."
BUDGET_NAME="$PROJECT-monthly-spend"
aws budgets create-budget \
  --account-id "$ACCOUNT_ID" \
  --profile hushstore --no-cli-pager \
  --budget "{
    \"BudgetName\": \"$BUDGET_NAME\",
    \"BudgetLimit\": {\"Amount\": \"10\", \"Unit\": \"USD\"},
    \"TimeUnit\": \"MONTHLY\",
    \"BudgetType\": \"COST\"
  }" \
  --notifications-with-subscribers "[
    {\"Notification\":{\"NotificationType\":\"ACTUAL\",\"ComparisonOperator\":\"GREATER_THAN\",\"Threshold\":1,\"ThresholdType\":\"ABSOLUTE_VALUE\"},\"Subscribers\":[{\"SubscriptionType\":\"EMAIL\",\"Address\":\"$ALERT_EMAIL\"}]},
    {\"Notification\":{\"NotificationType\":\"ACTUAL\",\"ComparisonOperator\":\"GREATER_THAN\",\"Threshold\":2,\"ThresholdType\":\"ABSOLUTE_VALUE\"},\"Subscribers\":[{\"SubscriptionType\":\"EMAIL\",\"Address\":\"$ALERT_EMAIL\"}]},
    {\"Notification\":{\"NotificationType\":\"ACTUAL\",\"ComparisonOperator\":\"GREATER_THAN\",\"Threshold\":5,\"ThresholdType\":\"ABSOLUTE_VALUE\"},\"Subscribers\":[{\"SubscriptionType\":\"EMAIL\",\"Address\":\"$ALERT_EMAIL\"}]},
    {\"Notification\":{\"NotificationType\":\"ACTUAL\",\"ComparisonOperator\":\"GREATER_THAN\",\"Threshold\":10,\"ThresholdType\":\"ABSOLUTE_VALUE\"},\"Subscribers\":[{\"SubscriptionType\":\"EMAIL\",\"Address\":\"$ALERT_EMAIL\"}]}
  ]"
echo "  Done."

# ─── 11. LƯU RESOURCES ────────────────────────────────────────
echo "[11/11] Saving $RESOURCES_FILE..."
cat > "$RESOURCES_FILE" <<EOF
# HushStore AWS Resources — $(date '+%Y-%m-%d %H:%M:%S')
REGION=$REGION
PROJECT=$PROJECT
ACCOUNT_ID=$ACCOUNT_ID
KEY_PAIR_NAME=$KEY_PAIR_NAME
KEY_FILE=$KEY_FILE

VPC_ID=$VPC_ID
IGW_ID=$IGW_ID

SUBNET_PUBLIC_A=$SUBNET_PUBLIC_A
SUBNET_PUBLIC_B=$SUBNET_PUBLIC_B
SUBNET_PRIVATE_A=$SUBNET_PRIVATE_A
SUBNET_PRIVATE_B=$SUBNET_PRIVATE_B

RT_PUBLIC=$RT_PUBLIC
RT_PUBLIC_ASSOC_A=$RT_PUBLIC_ASSOC_A
RT_PUBLIC_ASSOC_B=$RT_PUBLIC_ASSOC_B
RT_PRIVATE=$RT_PRIVATE
RT_PRIVATE_ASSOC_A=$RT_PRIVATE_ASSOC_A
RT_PRIVATE_ASSOC_B=$RT_PRIVATE_ASSOC_B

SG_EC2=$SG_EC2
SG_RDS=$SG_RDS

RDS_ID=$RDS_ID
RDS_ENDPOINT=$RDS_ENDPOINT

EC2_ID=$EC2_ID
PUBLIC_IP=$PUBLIC_IP

BUDGET_NAME=$BUDGET_NAME
ALERT_EMAIL=$ALERT_EMAIL
EOF

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  ✓ Done! Infrastructure sẵn sàng."
echo ""
echo "  EC2 Public IP : $PUBLIC_IP"
echo "  RDS Endpoint  : $RDS_ENDPOINT"
echo ""
echo "  Cloudflare DNS:"
echo "    A  @    → $PUBLIC_IP"
echo "    A  api  → $PUBLIC_IP"
echo ""
echo "  SSH: ssh -i $KEY_FILE ubuntu@$PUBLIC_IP"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
