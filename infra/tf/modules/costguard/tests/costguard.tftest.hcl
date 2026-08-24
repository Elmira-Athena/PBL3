# ─── VÌ SAO PROVIDER THẬT, KHÔNG PHẢI mock_provider ───────────────
# Module này đọc data.aws_caller_identity.current và data.aws_region.current, và
# hai giá trị đó KHÔNG phải thứ thay thế được bằng mock: toàn bộ ARN trong IAM
# policy (service, ASG, RDS, SNS topic, Lambda, log group) được dựng bằng chuỗi
# từ account id và region — xem locals trong main.tf, và xem lý do phải dựng
# bằng chuỗi ở đó (tham chiếu resource là "known after apply", nó làm cả
# aws_iam_policy_document thành unknown và mọi assert dưới đây đọc được rỗng).
#
# mock_provider CÓ chạy được nếu thêm `override_data` cho hai data source đó,
# nhưng nó đổi lấy một thứ đắt hơn nhiều so với việc chạy offline: các assert sẽ
# so khớp ARN dựng từ một account id BỊA. Lúc đó test không còn phân biệt được
# "policy trỏ đúng account của mình" với "policy trỏ vào account khác" — đúng
# lớp lỗi mà một lần copy-paste ARN từ tài liệu sẽ tạo ra. Với provider thật,
# `data.aws_caller_identity.current.account_id` trong assert là account THẬT.
#
# ĐÁNH ĐỔI phải biết: bộ test này cần SSO session còn hiệu lực (profile
# hushstore). Nó KHÔNG tốn tiền — mọi run đều `command = plan`, và ba read của
# provider (GetCallerIdentity, DescribeRegions/region lookup) đều miễn phí. Đây
# cũng là cách 6 module còn lại làm; chỉ modules/alb dùng mock_provider vì nó
# không có data source nào (xem comment đầu tests/alb.tftest.hcl).
provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project = "hushstore-tftest"

  # Email TEST, không phải email thật của dự án. Địa chỉ cảnh báo thật đến từ
  # var.alert_email ở envs/prod và không bao giờ được hardcode ở đâu.
  alert_email = "costguard-tftest@example.com"

  cluster_name   = "hushstore"
  asg_name       = "hushstore-asg"
  rds_identifier = "hushstore-db-tf"
  service_names  = ["hushstore-web", "hushstore-api"]
}

# ─────────────────────────────────────────────────────────────────
# TASK 1 — SNS. Hai tính chất: gửi đúng nơi, và không ai ngoài account publish
# được vào đó.
# ─────────────────────────────────────────────────────────────────
run "sns_subscription_tro_dung_bien_alert_email_va_di_bang_email" {
  command = plan

  # So với var.alert_email, KHÔNG so với một chuỗi literal: assert dạng literal
  # sẽ vẫn xanh khi có người hardcode một địa chỉ vào main.tf và bỏ qua biến.
  assert {
    condition     = aws_sns_topic_subscription.alert_email.endpoint == var.alert_email
    error_message = "Subscription phải trỏ tới var.alert_email. Đừng hardcode địa chỉ nào vào module — email cảnh báo của dự án được cấu hình ở envs/prod/terraform.tfvars, và một địa chỉ hardcode sẽ đi theo mọi ai copy module này."
  }

  assert {
    condition     = aws_sns_topic_subscription.alert_email.protocol == "email"
    error_message = "Protocol phải là email. Nếu đổi sang https/lambda thì phải sửa cả comment về pending_confirmation trong main.tf — chỉ protocol email mới cần người bấm link xác nhận."
  }

  assert {
    condition     = endswith(aws_sns_topic.costguard.name, "-costguard-alerts")
    error_message = "Tên topic phải kết thúc bằng -costguard-alerts để phân biệt với mọi topic khác trong account (account này còn người khác dùng làm lab)."
  }
}

run "sns_topic_policy_khong_cho_principal_ngoai_account_publish" {
  command = plan

  # Tính chất canh ở đây là KHÔNG CÓ Principal = "*", chứ không phải "có
  # Condition đúng". Một statement `Principal = "*"` kèm Condition trông cũng an
  # toàn nhưng nó chỉ an toàn đúng bằng condition key đó: viết sai tên key, hay
  # dùng một key vắng mặt với StringNotEquals, là mở topic cho cả thế giới
  # publish mà policy vẫn "trông có kiểm soát".
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.sns_topic.json).Statement :
      alltrue([
        for p in flatten([try(s.Principal.AWS, []), try(s.Principal.Service, [])]) :
        p != "*"
      ])
    ])
    error_message = "Topic policy KHÔNG được có Principal = \"*\", kể cả kèm Condition. Chặn cross-account publish bằng cách không cấp Allow nào cho principal ngoài account là cách duy nhất không phụ thuộc vào việc viết đúng tên một condition key."
  }

  # Và principal duy nhất được phép đúng là root của account NÀY. So với
  # data.aws_caller_identity thay vì một chuỗi số: một ARN copy từ tài liệu (hay
  # từ account của người khác) là đỏ ngay.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.sns_topic.json).Statement :
      alltrue([
        for p in flatten([try(s.Principal.AWS, [])]) :
        p == "arn:aws:iam::${data.aws_caller_identity.current.account_id}:root"
      ])
    ])
    error_message = "Principal duy nhất trong topic policy phải là root của CHÍNH account này. Một ARN account khác ở đây là cấp quyền publish/quản lý topic cho người ngoài."
  }

  # Thiếu hai action này thì Terraform tự khoá mình ra ngoài: refresh không đọc
  # được topic, và không sửa được chính policy đã khoá.
  assert {
    condition = length(setsubtract(
      ["SNS:GetTopicAttributes", "SNS:SetTopicAttributes"],
      flatten([
        for s in jsondecode(data.aws_iam_policy_document.sns_topic.json).Statement :
        flatten([s.Action])
      ])
    )) == 0
    error_message = "Topic policy phải cấp SNS:GetTopicAttributes và SNS:SetTopicAttributes cho account chủ. Thiếu chúng thì terraform không refresh được topic và không sửa lại được chính policy này — phải vào console dọn tay."
  }
}

# ─────────────────────────────────────────────────────────────────
# TASK 3 — Đây là run block quan trọng nhất của module. Toàn bộ lý do Lambda
# này tồn tại sụp đổ nếu nó bật được thứ gì.
# ─────────────────────────────────────────────────────────────────
run "lambda_khong_the_bat_va_khong_the_xoa_bat_cu_thu_gi" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      alltrue([
        for a in flatten([s.Action]) :
        !contains([
          "rds:StartDBInstance",
          "rds:CreateDBInstance",
          "rds:ModifyDBInstance",
          "rds:RestoreDBInstanceFromDBSnapshot",
          "autoscaling:UpdateAutoScalingGroup",
          "autoscaling:CreateAutoScalingGroup",
          "autoscaling:ResumeProcesses",
          "ec2:RunInstances",
          "ec2:CreateNatGateway",
          "ec2:AllocateAddress",
          "elasticloadbalancing:CreateLoadBalancer",
          "ecs:CreateService",
          "ecs:RunTask",
          "lambda:InvokeFunction",
          "iam:PassRole",
        ], a)
      ])
    ])
    error_message = "Role của Lambda cost guard KHÔNG được có quyền bật hạ tầng tính tiền theo giờ. Một cost guard có quyền bật là một cost guard có thể gây ra đúng thứ nó tồn tại để chặn — và nó chạy tự động lúc 0 giờ, không có ai xem."
  }

  # rds:Start* bị chặn theo TIỀN TỐ, không chỉ theo tên đầy đủ: họ action này có
  # nhiều thành viên (StartDBInstance, StartDBCluster,
  # StartDBInstanceAutomatedBackupsReplication) và denylist tên-đầy-đủ ở trên bỏ
  # sót mọi cái chưa được liệt kê.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      alltrue([for a in flatten([s.Action]) : !startswith(a, "rds:Start")])
    ])
    error_message = "KHÔNG action nào bắt đầu bằng rds:Start được phép tồn tại trong policy này, dưới bất kỳ hình thức nào. Đây là ràng buộc cứng của Phase 3, không phải khuyến nghị."
  }

  # Lambda BÁO chứ không XOÁ. NAT Gateway và ALB nằm trong Terraform state; xoá
  # bằng API làm state lệch thực tế và lần apply sau xử lý sai (nhẹ thì tạo lại,
  # nặng thì lỗi giữa apply và để hạ tầng nửa vời). Chặn ở tầng IAM vì comment
  # trong Python thì ai cũng sửa được.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      alltrue([
        for a in flatten([s.Action]) :
        !anytrue([
          for verb in ["Delete", "Terminate", "Deregister", "Detach", "Release", "Reboot"] :
          startswith(split(":", a)[1], verb)
        ])
      ])
    ])
    error_message = "Policy của Lambda không được chứa action xoá/gỡ nào. Việc tắt NAT và ALB thuộc về `bash infra/tf/scripts/down.sh` do người chạy — xoá resource Terraform quản lý bằng API là state drift và làm hỏng lần apply sau."
  }

  # Hai assert trên MÙ VỚI WILDCARD, và đó là lỗ đủ to để vô hiệu hoá cả hai:
  # "rds:*" không khớp chuỗi nào trong denylist, nhưng nó cấp đúng
  # rds:StartDBInstance. Cùng lập luận đã ghi ở tests/cicd.tftest.hcl.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      alltrue([for a in flatten([s.Action]) : !strcontains(a, "*")])
    ])
    error_message = "Action trong policy phải là tên ĐẦY ĐỦ, không được chứa dấu *. Một wildcard theo service (rds:*, ec2:*) làm mọi assert denylist phía trên vô hiệu trong khi vẫn cấp đúng thứ chúng cấm. Nếu thật sự cần thêm quyền: liệt kê tường minh từng action."
  }

  # Mọi statement phải là Allow. Một Deny lọt vào đây không nguy hiểm về quyền
  # nhưng nó làm cả bộ assert phía trên đọc sai nghĩa: "policy có chứa
  # rds:StartDBInstance" sẽ đỏ dù statement đó là Deny, và người sửa sẽ đi tìm
  # sai chỗ. Giữ policy này chỉ-Allow để mọi assert đọc theo đúng một nghĩa.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      s.Effect == "Allow"
    ])
    error_message = "Policy của Lambda chỉ được chứa statement Allow. Cần chặn thêm gì thì bỏ action ra khỏi Allow, đừng thêm Deny — Deny ở đây làm các assert denylist phía trên đọc sai nghĩa."
  }
}

run "lambda_chi_dung_resource_sao_o_dung_bon_action_chi_doc" {
  command = plan

  # Năm Sid dưới đây là những chỗ AWS KHÔNG hỗ trợ resource-level authorization,
  # và cả năm đều CHỈ ĐỌC. Mọi Resource = "*" mới xuất hiện phải đi qua đây —
  # kể cả khi người thêm nó quên viết lý do vào lambda.tf.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      !contains(flatten([s.Resource]), "*") ||
      contains([
        "AutoscalingDescribeNoResourceLevelSupport",
        "RdsDescribeNoResourceLevelSupport",
        "Ec2DescribeNatGatewaysNoResourceLevelSupport",
        "ElbDescribeLoadBalancersNoResourceLevelSupport",
        "Ec2DescribeAddressesNoResourceLevelSupport",
      ], s.Sid)
    ])
    error_message = "Statement có Resource = \"*\" phải nằm trong danh sách năm Sid mà AWS không hỗ trợ resource-level (cả năm đều chỉ đọc). Thêm statement rộng mới thì phải sửa cả danh sách này VÀ giải thích trong lambda.tf vì sao AWS không cho hẹp hơn."
  }

  # Và cả năm ngoại lệ đó phải thật sự chỉ đọc: một action ghi trốn vào một
  # statement Resource = "*" là cách thầm lặng nhất để nới quyền ra cả account.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      !contains(flatten([s.Resource]), "*") ||
      alltrue([for a in flatten([s.Action]) : startswith(split(":", a)[1], "Describe")])
    ])
    error_message = "Mọi statement dùng Resource = \"*\" chỉ được chứa action Describe*. Một action ghi trên Resource = \"*\" là quyền ghi trên toàn account."
  }

  # Chiều ngược lại: ba action GHI phải bị ghim theo ARN cụ thể, không được lọt
  # vào một statement rộng.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      alltrue([
        for a in flatten([s.Action]) :
        !contains(["ecs:UpdateService", "autoscaling:SetDesiredCapacity", "rds:StopDBInstance"], a) ||
        !contains(flatten([s.Resource]), "*")
      ])
    ])
    error_message = "Ba action ghi (ecs:UpdateService, autoscaling:SetDesiredCapacity, rds:StopDBInstance) phải ghim theo ARN cụ thể. Resource = \"*\" ở đây nghĩa là Lambda tắt được mọi service, mọi ASG và mọi database trong account."
  }

  # SetDesiredCapacity không siết được theo GIÁ TRỊ (AWS không cho condition key
  # nào về tham số DesiredCapacity cho action này — xem lambda.tf), nên thứ duy
  # nhất còn siết được là RESOURCE. Assert này canh đúng chỗ đó: dấu * trong ARN
  # chỉ được phép ở đoạn uuid, và tên group phải khớp chính xác.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      !contains(flatten([s.Action]), "autoscaling:SetDesiredCapacity") ||
      alltrue([
        for r in flatten([s.Resource]) :
        endswith(r, ":autoScalingGroupName/${var.asg_name}")
      ])
    ])
    error_message = "ARN của autoscaling:SetDesiredCapacity phải kết thúc bằng :autoScalingGroupName/<asg_name>. Đoạn uuid ở giữa buộc phải là * (AWS sinh lúc tạo group), nhưng TÊN group phải khớp chính xác — đây là lớp siết duy nhất còn lại vì AWS không hỗ trợ condition theo giá trị DesiredCapacity."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      !contains(flatten([s.Action]), "rds:StopDBInstance") ||
      alltrue([for r in flatten([s.Resource]) : endswith(r, ":db:${var.rds_identifier}")])
    ])
    error_message = "rds:StopDBInstance phải ghim vào đúng ARN của var.rds_identifier."
  }
}

run "sns_publish_gioi_han_dung_mot_topic_cua_module_nay" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      !contains(flatten([s.Action]), "sns:Publish") ||
      length(flatten([s.Resource])) == 1
    ])
    error_message = "sns:Publish phải giới hạn vào ĐÚNG MỘT ARN. Nhiều ARN (hay \"*\") nghĩa là Lambda gửi được tin nhắn vào đường cảnh báo của người khác dùng chung account này."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      !contains(flatten([s.Action]), "sns:Publish") ||
      alltrue([
        for r in flatten([s.Resource]) :
        r == "arn:aws:sns:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:${aws_sns_topic.costguard.name}"
      ])
    ])
    error_message = "ARN trong statement sns:Publish phải là chính topic mà module này tạo. Lệch nhau nghĩa là Lambda publish vào một topic không ai đăng ký, và mọi cảnh báo rơi vào hư không mà không có triệu chứng nào."
  }
}

run "lambda_chi_ghi_duoc_log_group_cua_chinh_no_va_log_co_retention" {
  command = plan

  # KHÔNG dùng managed policy AWSLambdaBasicExecutionRole: nó cấp
  # logs:CreateLogGroup/CreateLogStream/PutLogEvents trên "*", tức Lambda ghi
  # được vào log group của MỌI service — kể cả /ecs/hushstore-api, nơi có thể có
  # dữ liệu người dùng.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      alltrue([
        for a in flatten([s.Action]) :
        !startswith(a, "logs:") || alltrue([
          for r in flatten([s.Resource]) :
          strcontains(r, ":log-group:/aws/lambda/${aws_lambda_function.cost_guard.function_name}")
        ])
      ])
    ])
    error_message = "Quyền logs:* phải ghim vào đúng log group của Lambda này. Đừng gắn managed policy AWSLambdaBasicExecutionRole — nó cấp trên Resource = \"*\", tức ghi được vào log group của api nơi có thể có dữ liệu người dùng."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
      !contains(flatten([s.Action]), "logs:CreateLogGroup")
    ])
    error_message = "KHÔNG cấp logs:CreateLogGroup. Log group đã do Terraform tạo với retention 30 ngày; cấp quyền tạo nghĩa là nếu group bị xoá tay thì Lambda tự tạo lại một group KHÔNG retention (giữ log vĩnh viễn, trả tiền vĩnh viễn) mà terraform plan vẫn xanh."
  }

  # Assert này TỪNG là `retention_in_days > 0`, và phép so đó không canh đúng
  # thứ nó nói: 3 → 30 → 365 đều xanh, nên con số thật muốn giữ không được canh
  # ở đâu cả. `== 30` là con số có lý do, không phải con số tuỳ ý — xem comment
  # dài ở aws_cloudwatch_log_group trong lambda.tf. Ngắn hơn thì câu "tuần trước
  # guard có chạy không" không trả lời được, mà đây là bản ghi DUY NHẤT trả lời
  # được nó (Lambda im lặng khi khoẻ, và không có CloudWatch alarm nào).
  assert {
    condition     = aws_cloudwatch_log_group.cost_guard.retention_in_days == 30
    error_message = "Log group phải có retention đúng 30 ngày. 0 nghĩa là \"Never expire\" (trả tiền storage vĩnh viễn). Ngắn hơn 30 — ví dụ 3 ngày bê từ quy ước log ứng dụng của Phase 1 — thì mất bản ghi DUY NHẤT về việc lưới an toàn có chạy hay không: guard im lặng khi khoẻ, nên dòng JSON mỗi đêm là bằng chứng duy nhất tồn tại, và ở ~1KB/đêm thì 30 ngày tốn ~30KB tức bằng không. Đồng hồ 'guard chạy lần cuối' trong status.sh cũng đọc log stream này."
  }
}

run "lambda_cap_du_quyen_cho_moi_api_ma_cost_guard_py_goi" {
  command = plan

  # Ba run block trên chỉ assert điều PHỦ ĐỊNH. Chúng không bắt được lỗi ngược
  # chiều: một lần trim quyền quá tay. Lỗi đó không hiện ra ở fmt/validate/plan;
  # nó hiện ra lúc 0 giờ sáng dưới dạng AccessDenied, và triệu chứng duy nhất là
  # một email lỗi mà không ai đọc — hoặc tệ hơn, RDS không bị stop và không ai
  # biết. Danh sách dưới đây là hợp của mọi lời gọi boto3 trong src/cost_guard.py.
  assert {
    condition = length(setsubtract(
      [
        "ecs:DescribeServices",                       # _stop_ecs_services: đọc desiredCount
        "ecs:UpdateService",                          # _stop_ecs_services: hạ về 0
        "autoscaling:DescribeAutoScalingGroups",      # _scale_asg_to_zero: đọc DesiredCapacity
        "autoscaling:SetDesiredCapacity",             # _scale_asg_to_zero: hạ về 0
        "rds:DescribeDBInstances",                    # _stop_rds: chỉ stop khi status = available
        "rds:StopDBInstance",                         # _stop_rds
        "ec2:DescribeNatGateways",                    # _detect_terraform_owned_leftovers
        "elasticloadbalancing:DescribeLoadBalancers", # _detect_terraform_owned_leftovers
        "ec2:DescribeAddresses",                      # _detect_terraform_owned_leftovers: EIP rảnh
        "sns:Publish",                                # lambda_handler
        "logs:CreateLogStream",                       # runtime Lambda
        "logs:PutLogEvents",                          # runtime Lambda + print()
      ],
      flatten([
        for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement :
        flatten([s.Action])
      ])
    )) == 0
    error_message = "Policy THIẾU action mà src/cost_guard.py gọi tới. Policy hiện cấp: ${join(", ", sort(flatten([for s in jsondecode(data.aws_iam_policy_document.cost_guard.json).Statement : flatten([s.Action])])))}. So với danh sách trong assert này để tìm cái thiếu. Nếu bạn vừa xoá một action vì cho là không dùng: nó CÓ được gọi — sửa src/cost_guard.py trước, rồi mới sửa danh sách này."
  }
}

run "lambda_arm64_python313_va_truyen_du_bien_moi_truong" {
  command = plan

  assert {
    condition = alltrue([
      aws_lambda_function.cost_guard.runtime == "python3.13",
      contains(aws_lambda_function.cost_guard.architectures, "arm64"),
      aws_lambda_function.cost_guard.handler == "cost_guard.lambda_handler",
    ])
    error_message = "Lambda phải là python3.13 / arm64 và handler cost_guard.lambda_handler (tên file src/cost_guard.py + tên hàm)."
  }

  assert {
    condition = alltrue([
      aws_lambda_function.cost_guard.timeout == 120,
      aws_lambda_function.cost_guard.memory_size == 256,
    ])
    error_message = "timeout 120s / memory 256MB. Đừng hạ timeout xuống dưới thời gian của describe_services + describe_db_instances — hết timeout giữa bước 2 nghĩa là RDS không bao giờ được stop, tức mất đúng lý do Lambda tồn tại."
  }

  # src/cost_guard.py đọc os.environ[...] ở tầng module (không phải .get), nên
  # thiếu một biến là Lambda chết ở init với KeyError. Assert ở đây để lỗi đó
  # lộ ra lúc plan chứ không phải lúc 0 giờ sáng.
  assert {
    condition = length(setsubtract(
      ["PROJECT", "CLUSTER_NAME", "ASG_NAME", "RDS_IDENTIFIER", "SERVICE_NAMES", "SNS_TOPIC_ARN"],
      keys(aws_lambda_function.cost_guard.environment[0].variables)
    )) == 0
    error_message = "Thiếu biến môi trường mà src/cost_guard.py đọc bằng os.environ[...] ở tầng module. Thiếu một biến nghĩa là Lambda chết ngay ở init với KeyError, và nó chỉ lộ ra ở lần chạy đêm."
  }

  # SERVICE_NAMES đi qua join(",") rồi split(",") ở Python. Kiểm cả hai tên có
  # mặt: một dấu phẩy sai chỗ làm Lambda tắt đúng một service và để service kia
  # sống, mà log thì báo thành công.
  assert {
    condition = alltrue([
      for name in var.service_names :
      strcontains(aws_lambda_function.cost_guard.environment[0].variables["SERVICE_NAMES"], name)
    ])
    error_message = "Biến SERVICE_NAMES phải chứa mọi tên trong var.service_names, phân tách bởi dấu phẩy."
  }

  # reserved_concurrent_executions = -1 (không reserve) là một ĐỘ LỆCH có chủ ý
  # so với plan, dựa trên số đo thật của account. Assert này giữ cho việc "sửa
  # lại cho đúng plan" không âm thầm làm apply chết.
  assert {
    condition     = aws_lambda_function.cost_guard.reserved_concurrent_executions == -1
    error_message = "reserved_concurrent_executions phải là -1 trên account này. `aws lambda get-account-settings` cho ConcurrentExecutions = 10, và AWS luôn giữ tối thiểu 10 concurrency không được reserve, nên đặt 1 sẽ làm apply chết với InvalidParameterValueException. Trước khi đổi giá trị này: chạy lại get-account-settings, và chỉ đổi nếu trần đã lên 1000. Xem comment dài trong lambda.tf."
  }
}

# ─────────────────────────────────────────────────────────────────
# TASK 4 — Scheduler. Sai timezone ở đây không làm gì đỏ: nó chỉ tắt hạ tầng
# lúc 7 giờ sáng, mỗi ngày, giữa lúc có người đang làm việc.
# ─────────────────────────────────────────────────────────────────
run "scheduler_chay_theo_gio_viet_nam_khong_phai_utc" {
  command = plan

  assert {
    condition     = aws_scheduler_schedule.nightly_stop[0].schedule_expression_timezone == "Asia/Ho_Chi_Minh"
    error_message = "Timezone phải là Asia/Ho_Chi_Minh. Để UTC (hoặc bỏ trống, vì Scheduler mặc định UTC) nghĩa là cron(0 0 ...) chạy lúc 7 giờ SÁNG giờ Việt Nam — tắt hạ tầng đúng lúc bắt đầu ngày làm việc. Nếu buộc phải dùng UTC thì cron phải là cron(0 17 * * ? *), nhưng đừng: con số 17 đó sai vào đúng ngày có người đọc lại mà không biết nó đã bị trừ 7 giờ."
  }

  assert {
    condition     = aws_scheduler_schedule.nightly_stop[0].schedule_expression == var.stop_cron
    error_message = "schedule_expression phải lấy từ var.stop_cron, không hardcode."
  }

  # mode = "OFF" là ràng buộc thật: cửa sổ FLEXIBLE cho AWS dịch lần chạy trong
  # một khoảng, và mục đích ở đây là cắt chi phí ngay khi sang ngày mới.
  assert {
    condition     = aws_scheduler_schedule.nightly_stop[0].flexible_time_window[0].mode == "OFF"
    error_message = "flexible_time_window phải là OFF để Lambda chạy đúng giờ hẹn."
  }

  assert {
    condition = alltrue([
      aws_scheduler_schedule.nightly_stop[0].target[0].retry_policy[0].maximum_retry_attempts == 2,
      aws_scheduler_schedule.nightly_stop[0].target[0].retry_policy[0].maximum_event_age_in_seconds == 3600,
    ])
    error_message = "retry_policy: 2 lần thử lại và event age tối đa 1 giờ. Mặc định event age là 24 giờ — một lần chạy được giao thành công 20 giờ sau giờ hẹn sẽ hạ ECS và stop RDS vào giữa buổi chiều làm việc."
  }

  # Marker trong input là thứ làm log phân biệt được lần chạy theo hẹn với lần
  # chạy do người gọi tay. Không có nó thì cả hai đều là `{}` trong log, và lập
  # luận cho việc không đặt reserved_concurrent_executions (lambda.tf) mãi mãi
  # không kiểm chứng được từ dữ liệu.
  assert {
    condition = try(
      jsondecode(aws_scheduler_schedule.nightly_stop[0].target[0].input).invoked_by,
      null
    ) == "eventbridge-scheduler"
    error_message = "target.input phải là JSON chứa invoked_by = \"eventbridge-scheduler\". Mã Python in `event` ra dòng JSON summary; thiếu marker này thì một lần chạy theo hẹn và một lần `aws lambda invoke` bằng tay hiện ra giống hệt nhau trong CloudWatch, và không có cách nào kiểm chứng lập luận về concurrency ở lambda.tf."
  }
}

run "scheduler_role_chi_goi_duoc_dung_lambda_nay_va_khong_gi_khac" {
  command = plan

  # Tập action phải ĐÚNG BẰNG {lambda:InvokeFunction} — dùng == chứ không dùng
  # contains: contains vẫn xanh khi có người thêm iam:PassRole hay lambda:* vào
  # cùng statement.
  assert {
    condition = toset(flatten([
      for s in jsondecode(data.aws_iam_policy_document.scheduler_invoke.json).Statement :
      flatten([s.Action])
    ])) == toset(["lambda:InvokeFunction"])
    error_message = "Role của Scheduler chỉ được có ĐÚNG action lambda:InvokeFunction. Đây là role riêng của Scheduler, không phải role của Lambda — trộn hai cái là cấp cho Lambda quyền tự gọi chính nó."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.scheduler_invoke.json).Statement :
      length(flatten([s.Resource])) == 1 &&
      endswith(flatten([s.Resource])[0], ":function:${aws_lambda_function.cost_guard.function_name}")
    ])
    error_message = "Resource phải là ĐÚNG MỘT ARN, và là function cost guard của module này. \"*\" ở đây cho Scheduler gọi mọi Lambda trong account."
  }

  # Trust policy: principal phải là service scheduler.amazonaws.com, và phải có
  # SourceAccount. Thiếu SourceAccount thì role tin bất kỳ schedule nào của
  # EventBridge Scheduler — kể cả ở account khác — miễn nó biết ARN role.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.scheduler_assume.json).Statement :
      flatten([s.Principal.Service]) == ["scheduler.amazonaws.com"]
    ])
    error_message = "Trust policy phải chỉ tin service principal scheduler.amazonaws.com. Đây KHÔNG phải role của Lambda: nếu ở đây là lambda.amazonaws.com thì Lambda tự gọi được chính nó."
  }

  # Chỉ canh `aws:SourceAccount`, KHÔNG canh `aws:SourceArn` — và đó là kết quả
  # của một phép đo, không phải một sự lơi lỏng. Thêm `ArnEquals` trên ARN của
  # schedule làm `CreateSchedule` fail với "The execution role you provide must
  # allow AWS EventBridge Scheduler to assume the role", vì lúc xác thực role thì
  # schedule chưa tồn tại nên không có SourceArn nào để so. Xem comment đầy đủ
  # trong schedule.tf.
  #
  # Nếu có ngày ai đó thêm lại `aws:SourceArn` cho "chặt hơn": apply sẽ chết, và
  # test này KHÔNG bắt được (nó chỉ đòi SourceAccount). Đó là giới hạn có ý thức
  # — một `terraform test` chạy ở mức plan không thể biết AWS sẽ từ chối gì lúc
  # create. Chỗ ghi lại kiến thức đó là comment ở schedule.tf.
  # So GIÁ TRỊ, không chỉ kiểm SỰ CÓ MẶT. Một assert `!= null` vẫn xanh khi giá
  # trị là account id của người khác — tức là confused deputy vẫn mở, chỉ mở
  # cho đúng một account thay vì mọi account. Đó chính là lớp lỗi mà assert của
  # SNS topic policy phía trên đã tránh bằng cách so với
  # data.aws_caller_identity.current.account_id, và ở đây phải làm y như vậy.
  # `length == 1` giữ luôn phần kiểm sự có mặt: một list rỗng làm vòng alltrue
  # bên trong xanh rỗng.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.scheduler_assume.json).Statement :
      length(flatten([try(s.Condition.StringEquals["aws:SourceAccount"], [])])) == 1 &&
      alltrue([
        for v in flatten([try(s.Condition.StringEquals["aws:SourceAccount"], [])]) :
        v == data.aws_caller_identity.current.account_id
      ])
    ])
    error_message = "Trust policy của role Scheduler phải có aws:SourceAccount, và giá trị phải là account id của CHÍNH account này. Thiếu nó thì một schedule ở account khác assume được role chỉ cần biết ARN của nó; điền sai account id thì cửa đó vẫn mở, chỉ hẹp hơn — và một assert chỉ kiểm sự có mặt sẽ xanh trong cả hai ca."
  }
}

run "tat_enable_auto_stop_thi_khong_con_schedule_nhung_lambda_van_con" {
  command = plan

  variables {
    enable_auto_stop = false
  }

  assert {
    condition     = length(aws_scheduler_schedule.nightly_stop) == 0
    error_message = "enable_auto_stop = false phải xoá schedule. Đây là cách CỐ TÌNH để stack chạy qua đêm — và lúc đó không còn lưới an toàn nào."
  }

  # Lambda, role và log group KHÔNG bị gate: cả ba đều $0 khi không chạy, và giữ
  # chúng lại nghĩa là vẫn gọi tay được (`aws lambda invoke`) trong lúc lưới tự
  # động đang tắt. Gate luôn cả Lambda thì lúc cần nhất lại không có gì để gọi.
  #
  # Assert này TỪNG là `function_name != ""` cho cả ba, và ba phép so đó không
  # canh được gì: cả ba resource đều không dùng `count`, nên không có đường nào
  # để tên của chúng thành rỗng — test xanh dù có chuyện gì xảy ra. Thứ đáng
  # canh ở đây là TÊN ĐÚNG, vì ba cái tên phải khớp nhau chính xác: log group
  # phải là /aws/lambda/<function_name> (nếu lệch, Lambda ghi vào một group khác
  # rồi tự tạo group không retention), và cả ba tên đều dẫn xuất từ var.project
  # nên một lần sửa quy ước đặt tên ở một chỗ sẽ đỏ ở đây.
  assert {
    condition = alltrue([
      aws_lambda_function.cost_guard.function_name == "${var.project}-cost-guard",
      aws_iam_role.cost_guard.name == "${var.project}-cost-guard-role",
      aws_cloudwatch_log_group.cost_guard.name == "/aws/lambda/${var.project}-cost-guard",
    ])
    error_message = "Lambda, IAM role và log group KHÔNG được gate theo enable_auto_stop (cả ba đều $0 khi không chạy, và giữ chúng lại cho phép gọi tay bằng `aws lambda invoke` trong lúc lưới tự động đang tắt), và cả ba tên phải dẫn xuất từ var.project theo đúng quy ước: <project>-cost-guard, <project>-cost-guard-role, /aws/lambda/<project>-cost-guard. Tên log group lệch khỏi /aws/lambda/<function_name> nghĩa là Lambda ghi vào một group khác và tự tạo lại một group KHÔNG retention."
  }
}

# ─────────────────────────────────────────────────────────────────
# Bốn biến tên-resource đi thẳng vào Resource của IAM policy. Một dấu * ở đó
# không làm gì đỏ, không làm plan khác đi — nó chỉ âm thầm nới quyền của Lambda
# ra mọi resource cùng loại trong account. Run block này giữ cho các block
# validation trong variables.tf không bị xoá lặng lẽ.
# ─────────────────────────────────────────────────────────────────
run "ten_service_chua_dau_sao_bi_variable_validation_tu_choi" {
  command = plan

  variables {
    service_names = ["hushstore-*"]
  }

  expect_failures = [var.service_names]
}

run "ten_asg_chua_dau_sao_bi_variable_validation_tu_choi" {
  command = plan

  variables {
    asg_name = "*"
  }

  expect_failures = [var.asg_name]
}

run "rds_identifier_chua_dau_sao_bi_variable_validation_tu_choi" {
  command = plan

  variables {
    rds_identifier = "hushstore-*"
  }

  expect_failures = [var.rds_identifier]
}

# Chiều ngược lại của dấu `*`, và nó im lặng y như vậy: truyền một ARN đầy đủ
# vào chỗ mong đợi TÊN. Kết quả là một ARN méo (arn:aws:rds:...:db:arn:aws:rds:
# ...) khớp không resource nào cả — Lambda mất quyền stop, `apply` vẫn xanh, và
# triệu chứng duy nhất là một AccessDenied lúc 0 giờ sáng trong một log không ai
# đọc. Account id trong ARN dưới đây là số 0 có chủ ý: không cần account thật để
# chứng minh dấu `:` bị từ chối.
run "arn_day_du_truyen_vao_cho_mong_doi_ten_bi_tu_choi" {
  command = plan

  variables {
    rds_identifier = "arn:aws:rds:ap-southeast-1:000000000000:db:hushstore-db-tf"
  }

  expect_failures = [var.rds_identifier]
}

run "ten_service_chua_dau_gach_cheo_bi_tu_choi" {
  command = plan

  variables {
    service_names = ["hushstore/hushstore-web"]
  }

  expect_failures = [var.service_names]
}

run "ten_asg_chua_khoang_trang_bi_tu_choi" {
  command = plan

  variables {
    asg_name = "hushstore asg"
  }

  expect_failures = [var.asg_name]
}

run "stop_cron_dang_rate_bi_variable_validation_tu_choi" {
  command = plan

  variables {
    stop_cron = "rate(1 day)"
  }

  expect_failures = [var.stop_cron]
}

# ─── enable_budget ────────────────────────────────────────────────
# AWS chỉ cho 2 budget miễn phí mỗi account. Khi hai slot đã bị người dùng chung
# account chiếm, budget của dự án là cái thứ 3 và tốn $0.02/ngày — nên nó phải
# tắt được. Hai run dưới canh CẢ HAI chiều của công tắc, vì một công tắc chỉ
# được kiểm một chiều thì chiều kia là chỗ lỗi trốn vào: `count = 0` cứng vẫn
# xanh nếu chỉ có run "false", và `count = 1` cứng vẫn xanh nếu chỉ có run "true".
run "enable_budget_false_thi_khong_tao_budget_nao" {
  command = plan

  variables {
    enable_budget = false
  }

  assert {
    condition     = length(aws_budgets_budget.monthly) == 0
    error_message = "enable_budget = false PHẢI không tạo budget nào — nếu vẫn tạo thì đây là budget thứ 3 của account và tốn $0.02/ngày."
  }
}

run "enable_budget_true_thi_tao_dung_mot_budget" {
  command = plan

  variables {
    enable_budget = true
  }

  assert {
    condition     = length(aws_budgets_budget.monthly) == 1
    error_message = "enable_budget = true phải tạo đúng 1 budget."
  }

  assert {
    condition     = aws_budgets_budget.monthly[0].limit_unit == "USD"
    error_message = "Budget phải tính bằng USD."
  }
}
