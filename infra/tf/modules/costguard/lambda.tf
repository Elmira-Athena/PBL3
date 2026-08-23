# ─── ĐÓNG GÓI MÃ NGUỒN ───────────────────────────────────────────
# Zip trực tiếp thư mục src/ thay vì build artifact rồi upload lên S3: mã chỉ
# dùng boto3 (có sẵn trong runtime Python 3.13 của Lambda) nên không có
# dependency nào để cài, không cần layer, không cần bước build trong CI. Đổi
# một dòng Python là `terraform apply` deploy được ngay.
#
# `excludes` bỏ __pycache__: lệnh kiểm `python3 -m py_compile src/cost_guard.py`
# sinh thư mục đó ngay trong src/, và nếu nó vào zip thì source_code_hash đổi
# theo phiên bản CPython của máy chạy apply — tức mỗi người apply lại thấy Lambda
# "có thay đổi" dù mã y nguyên.
data "archive_file" "cost_guard" {
  type        = "zip"
  source_dir  = "${path.module}/src"
  output_path = "${path.module}/.build/cost_guard.zip"
  excludes    = ["__pycache__"]
}

# ─── LOG GROUP ───────────────────────────────────────────────────
# Tạo bằng Terraform, KHÔNG để Lambda tự tạo. Log group do Lambda tự tạo lúc
# chạy lần đầu có retention = "Never expire": log giữ vĩnh viễn và trả tiền
# storage vĩnh viễn, cho một Lambda in ra JSON mỗi đêm. Đây đúng là lập luận đã
# dùng cho 4 log group của ECS ở Phase 1 (modules/ecs/cluster.tf).
#
# 30 NGÀY, KHÔNG PHẢI 3 — và log này khác hẳn 4 log group của ECS ở Phase 1.
# Quy ước 3 ngày bên đó tồn tại vì log ứng dụng có VOLUME LỚN: mỗi request một
# dòng, và tiền ingest + storage tăng theo lưu lượng. Log của cost guard đi
# ngược lại trên cả hai chiều. Về lượng: đúng một dòng JSON ~1KB mỗi đêm, nên 30
# ngày là ~30KB, tức bằng không. Về giá trị: đây là bản ghi DUY NHẤT về việc lưới
# an toàn có chạy hay không — Lambda im lặng khi khoẻ (không email, không alarm,
# xem src/cost_guard.py), nên dòng JSON này là bằng chứng duy nhất tồn tại. Với
# retention 3 ngày thì câu hỏi "tuần trước guard có chạy đêm nào không" KHÔNG
# TRẢ LỜI ĐƯỢC, và nó đúng là câu hỏi người ta hỏi khi thấy một khoản chi lạ.
# Đồng hồ "guard chạy lần cuối bao giờ" trong status.sh cũng đọc log stream này.
#
# Hệ quả về thứ tự: Lambda phải phụ thuộc log group. Nếu Lambda chạy trước khi
# log group tồn tại thì nó tự tạo group (không retention) và Terraform sau đó
# apply sẽ vướng ResourceAlreadyExistsException.
resource "aws_cloudwatch_log_group" "cost_guard" {
  name              = local.lambda_log_group_name
  retention_in_days = 30

  tags = { Name = local.lambda_log_group_name }
}

# ─── IAM ROLE CỦA LAMBDA ─────────────────────────────────────────
data "aws_iam_policy_document" "lambda_assume" {
  statement {
    sid     = "LambdaServiceAssume"
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

# `description` của IAM role viết KHÔNG DẤU, khác với mọi comment và message
# khác trong dự án. Đây là ràng buộc của AWS chứ không phải bỏ sót: field này chỉ
# nhận ASCII in được, IAM trả ValidationError với ký tự có dấu. Cùng quy ước đã
# dùng cho 2 role của modules/cicd. Mọi nội dung hướng tới người đọc thật (email
# của Lambda, comment, error_message của test) vẫn là tiếng Việt có dấu.
resource "aws_iam_role" "cost_guard" {
  name        = "${local.lambda_function_name}-role"
  description = "Cost guard: chi scale/stop, KHONG xoa va KHONG bat duoc gi"

  assume_role_policy = data.aws_iam_policy_document.lambda_assume.json

  tags = { Name = "${local.lambda_function_name}-role" }
}

# ─── POLICY: ĐÚNG NHỮNG QUYỀN CẦN, KHÔNG HƠN ─────────────────────
#
# NĂM CHỖ BUỘC PHẢI DÙNG Resource = "*", và vì sao AWS không cho hẹp hơn:
#
#   1. autoscaling:DescribeAutoScalingGroups — mọi action Describe* của EC2 Auto
#      Scaling đều không hỗ trợ resource-level authorization; tên group đi trong
#      request parameter, không phải trong Resource.
#   2. rds:DescribeDBInstances — RDS không hỗ trợ resource-level cho Describe.
#      (Cùng kết luận đã ghi ở modules/cicd/policy.tf.)
#   3. ec2:DescribeNatGateways — toàn bộ họ ec2:Describe* không hỗ trợ
#      resource-level; đây là giới hạn của EC2 API, không phải lựa chọn ở đây.
#   4. elasticloadbalancing:DescribeLoadBalancers — không hỗ trợ resource-level.
#   5. ec2:DescribeAddresses — cùng lý do như (3). Cần để phát hiện Elastic IP
#      không còn gắn vào gì: IPv4 công cộng tính $0,005/giờ dù gắn hay không, và
#      một NAT Gateway bị xoá ngoài Terraform để lại đúng cái đó — $0,12/ngày
#      chạy vô thời hạn mà không resource nào còn để status.sh đếm giờ.
#
# Cả năm đều CHỈ ĐỌC. Bán kính thiệt hại nếu role này bị chiếm: đọc được danh
# sách ASG / DB instance / NAT / load balancer / Elastic IP của account. Không
# action nào trong năm cái đó sửa, xoá hay bật được gì.
#
# BA CHỖ GHI, cả ba ghim theo ARN cụ thể:
#   ecs:UpdateService            → đúng các service trong var.service_names
#   autoscaling:SetDesiredCapacity → đúng var.asg_name
#   rds:StopDBInstance           → đúng var.rds_identifier
#
# KHÔNG CÓ, và đây là phần quan trọng nhất của cả module:
#   • rds:StartDBInstance — dưới mọi hình thức. Một cost guard có quyền bật là
#     một cost guard có thể gây ra đúng thứ nó tồn tại để chặn.
#   • Mọi action xoá (rds:Delete*, ec2:Delete*, elasticloadbalancing:Delete*,
#     ecs:DeleteService). NAT Gateway và ALB do Terraform quản lý; xoá chúng
#     bằng API làm state lệch thực tế và lần apply sau xử lý sai. Lambda BÁO,
#     con người XOÁ bằng down.sh.
#   • autoscaling:UpdateAutoScalingGroup — xem giới hạn của
#     SetDesiredCapacity ngay dưới đây.
#   • AWSLambdaBasicExecutionRole (managed policy). Nó cấp logs:CreateLogGroup,
#     CreateLogStream và PutLogEvents trên "*", tức Lambda ghi được vào log
#     group của MỌI service — kể cả log của api, nơi có thể có dữ liệu người
#     dùng. Ba dòng ở statement cuối thay được nó và hẹp hơn hẳn.
#
# GIỚI HẠN THẬT CỦA autoscaling:SetDesiredCapacity — ĐỌC TRƯỚC KHI SỬA:
# Plan dự tính siết action này bằng một IAM condition để nó chỉ nhận giá trị 0.
# ĐIỀU ĐÓ KHÔNG LÀM ĐƯỢC. Service Authorization Reference của EC2 Auto Scaling
# chỉ cho SetDesiredCapacity hai condition key: aws:ResourceTag/${TagKey} và
# autoscaling:ResourceTag/${TagKey} — cả hai đều nói về resource, không nói về
# tham số. Key `autoscaling:DesiredCapacity` CÓ tồn tại nhưng chỉ áp dụng cho
# CreateAutoScalingGroup và UpdateAutoScalingGroup, không cho SetDesiredCapacity.
# Đừng thêm nó vào đây: một condition key không được action hỗ trợ thì không bao
# giờ khớp, nên statement thành vô hiệu và Lambda mất luôn quyền hạ ASG — hỏng
# âm thầm, chỉ lộ ra ở lần chạy đêm dưới dạng AccessDenied.
#
# Bù bằng hai lớp thật:
#   • Không cấp autoscaling:UpdateAutoScalingGroup, nên Lambda KHÔNG nới được
#     max_size. max_size = 1 do Terraform đặt trên
#     aws_autoscaling_group.this trong modules/ecs/cluster.tf, nên trần thiệt
#     hại tuyệt đối nếu ai đó sửa mã thành SetDesiredCapacity(N) là 1 instance
#     t3.micro = $0,0132/giờ.
#   • Mã Python chỉ truyền DesiredCapacity=0, và không có nhánh nào truyền khác.
#
# TRẦN $0,0132/GIỜ ĐANG DỰA VÀO MỘT THỨ Ở MODULE KHÁC — ĐỌC TRƯỚC KHI BẬT
# MANAGED SCALING:
# Lý do trần đó đứng vững KHÔNG phải là "muốn nâng capacity thì phải gọi
# SetDesiredCapacity, mà action đó bị ghim vào đúng một ASG". Lý do thật là
# `managed_scaling { status = "DISABLED" }` trong
# aws_ecs_capacity_provider.this ở modules/ecs/cluster.tf. (Hai chỗ trên cố ý
# dẫn theo TÊN RESOURCE chứ không theo số dòng: số dòng của file đó đã trôi một
# lần ngay trong lúc viết đoạn này.)
# Nếu có ai bật managed scaling lên thì ECS tự quản capacity của ASG, và lúc đó
# `ecs:UpdateService` với desiredCount > 0 MỘT MÌNH đủ để ECS nâng capacity —
# không cần SetDesiredCapacity, không cần UpdateAutoScalingGroup, tức đi vòng
# qua cả hai lớp bù phía trên. Trần vẫn còn nhờ max_size = 1, nhưng lập luận
# "Lambda không tự nâng được capacity" thì hết đúng.
# Đây là phụ thuộc chéo module và nó không hiện ra ở phía này: bật managed
# scaling là một dòng trong modules/ecs, `terraform plan` ở đó xanh, và không có
# test nào của module costguard đỏ. Ai sửa modules/ecs phải đọc lại chỗ này.
data "aws_iam_policy_document" "cost_guard" {

  # ── BƯỚC 1: ECS service về 0 ───────────────────────────────────
  statement {
    sid    = "EcsReadAndScaleDownOnlyOurServices"
    effect = "Allow"

    actions = [
      "ecs:DescribeServices",
      "ecs:UpdateService",
    ]

    resources = local.service_arns
  }

  # ── BƯỚC 2: ASG desired về 0 ───────────────────────────────────
  statement {
    sid       = "AutoscalingSetDesiredOnlyOurAsg"
    effect    = "Allow"
    actions   = ["autoscaling:SetDesiredCapacity"]
    resources = [local.asg_arn]
  }

  statement {
    sid       = "AutoscalingDescribeNoResourceLevelSupport"
    effect    = "Allow"
    actions   = ["autoscaling:DescribeAutoScalingGroups"]
    resources = ["*"]
  }

  # ── BƯỚC 3: stop RDS. KHÔNG có Start ───────────────────────────
  statement {
    sid       = "RdsStopOnlyOurInstance"
    effect    = "Allow"
    actions   = ["rds:StopDBInstance"]
    resources = [local.rds_instance_arn]
  }

  statement {
    sid       = "RdsDescribeNoResourceLevelSupport"
    effect    = "Allow"
    actions   = ["rds:DescribeDBInstances"]
    resources = ["*"]
  }

  # ── BƯỚC 4: chỉ đọc, để BÁO chứ không để XOÁ ───────────────────
  statement {
    sid       = "Ec2DescribeNatGatewaysNoResourceLevelSupport"
    effect    = "Allow"
    actions   = ["ec2:DescribeNatGateways"]
    resources = ["*"]
  }

  statement {
    sid       = "ElbDescribeLoadBalancersNoResourceLevelSupport"
    effect    = "Allow"
    actions   = ["elasticloadbalancing:DescribeLoadBalancers"]
    resources = ["*"]
  }

  # DescribeAddresses, không phải ReleaseAddress. Lambda BÁO một EIP rảnh chứ
  # tuyệt đối không release nó: aws_eip.nat nằm trong Terraform state, và
  # release bằng API là state drift đúng kiểu đã cấm ở đầu file này.
  statement {
    sid       = "Ec2DescribeAddressesNoResourceLevelSupport"
    effect    = "Allow"
    actions   = ["ec2:DescribeAddresses"]
    resources = ["*"]
  }

  # ── Cảnh báo ───────────────────────────────────────────────────
  # Đúng MỘT topic. Không phải "*": sns:Publish trên "*" cho phép gửi tin nhắn
  # tới mọi topic của account, và ở đây điều đó nghĩa là gửi được vào đường
  # cảnh báo của người khác dùng chung account.
  statement {
    sid       = "SnsPublishOnlyCostguardTopic"
    effect    = "Allow"
    actions   = ["sns:Publish"]
    resources = [local.sns_topic_arn]
  }

  # ── Log ────────────────────────────────────────────────────────
  # KHÔNG có logs:CreateLogGroup: group đã do Terraform tạo với retention 3
  # ngày. Cấp quyền tạo group nghĩa là nếu ai đó xoá group thì Lambda tự tạo lại
  # một group KHÔNG retention, và cấu hình retention biến mất mà plan vẫn xanh.
  statement {
    sid    = "WriteOnlyItsOwnLogStreams"
    effect = "Allow"

    actions = [
      "logs:CreateLogStream",
      "logs:PutLogEvents",
    ]

    resources = [
      local.lambda_log_group_arn,
      "${local.lambda_log_group_arn}:log-stream:*",
    ]
  }
}

resource "aws_iam_role_policy" "cost_guard" {
  name   = "${local.lambda_function_name}-policy"
  role   = aws_iam_role.cost_guard.id
  policy = data.aws_iam_policy_document.cost_guard.json
}

# ─── LAMBDA ──────────────────────────────────────────────────────
resource "aws_lambda_function" "cost_guard" {
  function_name = local.lambda_function_name
  description   = "Ha ECS/ASG ve 0 va stop RDS moi dem. Khong xoa gi, khong bat duoc gi."
  role          = aws_iam_role.cost_guard.arn

  filename         = data.archive_file.cost_guard.output_path
  source_code_hash = data.archive_file.cost_guard.output_base64sha256
  handler          = "cost_guard.lambda_handler"
  runtime          = "python3.13"

  # arm64 (Graviton): rẻ hơn x86 khoảng 20% mỗi GB-giây. Ở quy mô này cả hai
  # đều nằm trong free tier nên khoản tiết kiệm là $0 — chọn arm64 vì không có
  # lý do gì để không, mã thuần Python không có binary dependency nào.
  architectures = ["arm64"]

  # 120 giây: bước chậm nhất là describe_services (vài giây). StopDBInstance
  # trả về ngay sau khi nhận lệnh, Lambda KHÔNG chờ RDS về 'stopped' (mất ~5
  # phút) — chờ là trả tiền Lambda để ngồi xem, và trạng thái cuối kiểm được
  # bằng status.sh.
  timeout     = 120
  memory_size = 256

  # ─── VÌ SAO KHÔNG CÓ reserved_concurrent_executions = 1 ─────────
  # Plan yêu cầu đặt 1 làm bảo hiểm chống chạy chồng. KHÔNG ĐẶT ĐƯỢC trên
  # account này, và đây là số đo thật, không phải phỏng đoán:
  #
  #   aws lambda get-account-settings --profile hushstore --region ap-southeast-1
  #   → ConcurrentExecutions: 10, UnreservedConcurrentExecutions: 10
  #
  # AWS luôn giữ tối thiểu 10 concurrency KHÔNG được reserve cho mỗi account.
  # Trần của account này đúng bằng 10, nên reserve 1 sẽ đẩy phần unreserved về 9
  # và apply chết với:
  #   InvalidParameterValueException: Specified ConcurrentExecutions for function
  #   decreases account's UnreservedConcurrentExecution below its minimum value of [10]
  # Tức là ship giá trị 1 ở đây không phải "chặt hơn", nó là làm `up.sh` không
  # apply được. Trần 10 là hạn mức mặc định của account mới; nó tăng lên 1000
  # sau khi account "trưởng thành" hoặc sau một service quota request.
  #
  # Rủi ro chạy chồng được bù ở tầng mã, không bỏ ngỏ:
  #   • Scheduler dùng flexible_time_window mode = OFF và chỉ retry KHI LỖI, nên
  #     hai lần chạy song song chỉ xảy ra nếu có người invoke tay đúng lúc
  #     0:00 sáng.
  #   • Cả ba bước ghi đều đọc trạng thái trước khi ghi, và bước RDS bắt riêng
  #     InvalidDBInstanceState rồi coi là thành công (xem _stop_rds trong
  #     src/cost_guard.py) — đúng cái lỗi giả mà reserve concurrency định chặn.
  # Khi trần account lên 1000 thì thêm lại dòng reserved_concurrent_executions = 1
  # là một thay đổi an toàn.

  environment {
    variables = {
      PROJECT        = var.project
      CLUSTER_NAME   = var.cluster_name
      ASG_NAME       = var.asg_name
      RDS_IDENTIFIER = var.rds_identifier
      # Truyền dạng chuỗi phân tách bởi dấu phẩy: biến môi trường của Lambda chỉ
      # nhận string, và join ở đây giữ cho mã Python không phải parse JSON cho
      # một danh sách hai phần tử.
      SERVICE_NAMES = join(",", var.service_names)
      SNS_TOPIC_ARN = local.sns_topic_arn
    }
  }

  # Log group phải tồn tại TRƯỚC lần chạy đầu, nếu không Lambda tự tạo một group
  # không có retention và Terraform sau đó vướng ResourceAlreadyExistsException.
  # Quan hệ này không tự suy ra được vì cả hai bên chỉ nối với nhau qua tên.
  depends_on = [
    aws_cloudwatch_log_group.cost_guard,
    aws_iam_role_policy.cost_guard,
  ]

  tags = { Name = local.lambda_function_name }
}
