#!/usr/bin/env bash
# HushStore — chờ container instance đăng ký vào ECS cluster sau khi apply.
#
# TẠI SAO CẦN SCRIPT NÀY
# `terraform apply` xanh KHÔNG có nghĩa là instance đã vào cluster. ASG dùng
# health_check_type = "EC2", nên nó chỉ hỏi "EC2 có running không", không hỏi
# "ECS agent có đăng ký không". Một instance boot xong mà agent chết vẫn làm
# apply xanh và ASG báo Healthy. Đó chính là chế độ lỗi đã xảy ra hai lần:
# user_data gọi `systemctl enable --now ecs` khoá chết với cloud-final, instance
# chạy nhưng cluster rỗng, và không có tín hiệu nào ở tầng Terraform.
#
# Vậy nên: sau MỌI lần bật instance_count = 1, chạy script này. Nó là gate duy
# nhất phân biệt "hạ tầng đã dựng" với "hạ tầng đã dựng và dùng được".
#
# Dùng:
#   bash infra/tf/scripts/wait-for-capacity.sh [cluster] [timeout_giây]
#
# Mặc định lấy tên cluster từ `terraform output`, timeout 300 giây.
# Script CHỈ gọi API đọc (describe/list) — không tạo, không sửa, không xoá gì.

set -euo pipefail

PROFILE="${AWS_PROFILE:-hushstore}"
REGION="${AWS_REGION:-ap-southeast-1}"
TIMEOUT="${2:-300}"
INTERVAL=10

TF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../envs/prod" && pwd)"

CLUSTER="${1:-}"
if [ -z "$CLUSTER" ]; then
  CLUSTER=$(terraform -chdir="$TF_DIR" output -raw ecs_cluster_name)
fi

echo "Cluster : $CLUSTER"
echo "Region  : $REGION   Profile: $PROFILE"
echo "Timeout : ${TIMEOUT}s"
echo

DEADLINE=$((SECONDS + TIMEOUT))
while :; do
  # Chỉ đếm instance ACTIVE. Một instance ở DRAINING không nhận task mới nên
  # không tính là capacity dùng được.
  ARNS=$(aws ecs list-container-instances \
    --cluster "$CLUSTER" --status ACTIVE \
    --query 'containerInstanceArns' --output text \
    --profile "$PROFILE" --region "$REGION" --no-cli-pager)

  if [ -n "$ARNS" ] && [ "$ARNS" != "None" ]; then
    echo "Đã đăng ký. Chi tiết:"
    # $ARNS KHÔNG được quote một cách CỐ Ý: `--output text` trả danh sách ARN
    # phân tách bằng tab, và `--container-instances` cần chúng là các argument
    # riêng. Quote lại thành một chuỗi là AWS CLI báo lỗi ARN không hợp lệ.
    # Backtick trong --query là cú pháp literal của JMESPath, không phải shell.
    # shellcheck disable=SC2086,SC2016
    aws ecs describe-container-instances \
      --cluster "$CLUSTER" --container-instances $ARNS \
      --query 'containerInstances[].{instance:ec2InstanceId,status:status,agent:agentConnected,version:versionInfo.agentVersion,cpu:remainingResources[?name==`CPU`].integerValue|[0],mem:remainingResources[?name==`MEMORY`].integerValue|[0]}' \
      --output table --profile "$PROFILE" --region "$REGION" --no-cli-pager

    # agentConnected = false nghĩa là instance có trong cluster nhưng agent đã
    # mất kết nối — ECS sẽ không xếp task lên đó. Vẫn phải coi là thất bại.
    # shellcheck disable=SC2086,SC2016
    DISCONNECTED=$(aws ecs describe-container-instances \
      --cluster "$CLUSTER" --container-instances $ARNS \
      --query 'length(containerInstances[?agentConnected==`false`])' \
      --output text --profile "$PROFILE" --region "$REGION" --no-cli-pager)
    if [ "$DISCONNECTED" != "0" ]; then
      echo "LỖI: có $DISCONNECTED instance mà agentConnected = false." >&2
      exit 1
    fi
    echo
    echo "OK — cluster có capacity dùng được."
    exit 0
  fi

  if [ "$SECONDS" -ge "$DEADLINE" ]; then
    echo "HẾT THỜI GIAN CHỜ sau ${TIMEOUT}s: cluster $CLUSTER vẫn không có container instance nào." >&2
    echo >&2
    echo "Chẩn đoán theo đúng thứ tự này:" >&2
    echo "  1. Instance có chạy không?" >&2
    echo "     aws autoscaling describe-auto-scaling-groups --auto-scaling-group-names \\" >&2
    echo "       \$(terraform -chdir=$TF_DIR output -raw asg_name) \\" >&2
    echo "       --query 'AutoScalingGroups[0].Instances' --profile $PROFILE" >&2
    echo "  2. NAT Gateway có bật không? Không có egress thì agent không tới được" >&2
    echo "     ECS control plane, và SSM Session Manager cũng mất kết nối." >&2
    echo "     grep enable_nat $TF_DIR/terraform.tfvars" >&2
    echo "  3. Vào host bằng SSM rồi kiểm ĐÚNG BA thứ này:" >&2
    echo "     aws ssm start-session --target <instance-id> --profile $PROFILE" >&2
    echo "       cloud-init status --long      # 'running' = user_data còn treo" >&2
    echo "       systemctl is-active ecs       # 'inactive (dead)' = agent chưa lên" >&2
    echo "       ps -ef | grep -c systemctl    # systemctl treo = deadlock quay lại" >&2
    echo "       sudo cat /var/log/ecs/ecs-agent.log | tail -50" >&2
    echo "     Nếu thấy systemctl treo dưới cloud-init: user_data lại gọi" >&2
    echo "     systemctl start ecs. Xem comment trong modules/ecs/user_data.sh.tftpl." >&2
    exit 1
  fi

  echo "chưa có instance nào — chờ ${INTERVAL}s (còn $((DEADLINE - SECONDS))s)"
  sleep "$INTERVAL"
done
