provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project = "hushstore-tftest"

  ecr_repository_arns = [
    "arn:aws:ecr:ap-southeast-1:000000000000:repository/hushstore-api",
    "arn:aws:ecr:ap-southeast-1:000000000000:repository/hushstore-web",
    "arn:aws:ecr:ap-southeast-1:000000000000:repository/hushstore-migrator",
    "arn:aws:ecr:ap-southeast-1:000000000000:repository/hushstore-seeder",
  ]

  cluster_arn             = "arn:aws:ecs:ap-southeast-1:000000000000:cluster/hushstore"
  cluster_name            = "hushstore"
  service_names           = ["hushstore-web", "hushstore-api"]
  migrator_taskdef_family = "hushstore-migrator"
  migrator_log_group_arn  = "arn:aws:logs:ap-southeast-1:000000000000:log-group:/ecs/hushstore-migrator"
  rds_instance_arn        = "arn:aws:rds:ap-southeast-1:000000000000:db:hushstore-db-tf"
  artifacts_bucket_arn    = "arn:aws:s3:::hushstore-artifacts"

  passable_role_arns = [
    "arn:aws:iam::000000000000:role/hushstore-task-execution-role",
    "arn:aws:iam::000000000000:role/hushstore-task-app-role",
    "arn:aws:iam::000000000000:role/hushstore-task-migrator-role",
  ]
}

# ─────────────────────────────────────────────────────────────────
# Trust policy là ranh giới duy nhất giữa "pipeline của chúng ta" và "bất kỳ ai
# trên GitHub". Bốn assertion dưới đây canh đúng bốn cách làm hỏng nó.
# ─────────────────────────────────────────────────────────────────
run "trust_policy_khong_duoc_dung_wildcard_tren_claim_sub" {
  command = plan

  # StringLike + wildcard là lỗi cấu hình OIDC phổ biến nhất: `repo:owner/repo:*`
  # khớp cả pull_request, cả mọi nhánh, cả mọi tag — tức mở role deploy cho bất
  # kỳ ai mở được PR vào repo này.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.assume_deploy.json).Statement :
      !can(s.Condition.StringLike)
    ])
    error_message = "Trust policy của role deploy KHÔNG được dùng StringLike — chỉ StringEquals trên giá trị sub đầy đủ. StringLike với wildcard sẽ khớp cả pull_request và mọi nhánh."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.assume_deploy.json).Statement :
      alltrue([
        for v in flatten([s.Condition.StringEquals["token.actions.githubusercontent.com:sub"]]) :
        !strcontains(v, "*")
      ])
    ])
    error_message = "Giá trị của claim sub không được chứa dấu * — nó phải là một nhánh cụ thể."
  }

  # Thiếu điều kiện aud thì một token GitHub ký cho audience khác vẫn thoả.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.assume_deploy.json).Statement :
      try(s.Condition.StringEquals["token.actions.githubusercontent.com:aud"], null) != null
    ])
    error_message = "Trust policy phải có điều kiện trên claim aud = sts.amazonaws.com."
  }

  # Hai role phải nhận HAI giá trị sub khác nhau. Nếu trùng nhau thì role plan
  # (được gắn ReadOnlyAccess) cũng assume được từ push main, và ngược lại một PR
  # assume được role deploy.
  assert {
    condition     = jsondecode(data.aws_iam_policy_document.assume_deploy.json).Statement[0].Condition.StringEquals["token.actions.githubusercontent.com:sub"] != jsondecode(data.aws_iam_policy_document.assume_plan.json).Statement[0].Condition.StringEquals["token.actions.githubusercontent.com:sub"]
    error_message = "Role deploy và role plan phải khớp hai giá trị sub KHÁC nhau (ref:refs/heads/main vs pull_request)."
  }
}

# ─────────────────────────────────────────────────────────────────
# Least privilege: Resource = "*" phải là ngoại lệ có tên, không phải mặc định.
# ─────────────────────────────────────────────────────────────────
run "deploy_role_chi_dung_resource_sao_o_dung_bon_cho_da_biet" {
  command = plan

  # Bốn Sid dưới đây là những chỗ AWS KHÔNG hỗ trợ resource-level. Mọi Resource
  # = "*" khác phải có Condition ghim lại. Test này đỏ khi có người thêm một
  # statement rộng mới — kể cả khi họ quên giải thích.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.deploy.json).Statement :
      !contains(flatten([s.Resource]), "*") ||
      can(s.Condition) ||
      contains([
        "EcrLoginAccountWide",
        "EcsDescribeTaskDefinition",
        "EcsRegisterTaskDefinitionNoResourceLevelSupport",
        "RdsDescribeNoResourceLevelSupport",
      ], s.Sid)
    ])
    error_message = "Statement có Resource = \"*\" mà không có Condition thì phải nằm trong danh sách bốn Sid mà AWS không hỗ trợ resource-level. Thêm statement rộng mới thì phải sửa cả danh sách này và giải thích trong policy.tf."
  }

  # PassRole không có condition PassedToService là đường leo thang đặc quyền:
  # role đi được vào EC2 hay Lambda, không chỉ vào ECS task.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.deploy.json).Statement :
      !contains(flatten([s.Action]), "iam:PassRole") ||
      try(s.Condition.StringEquals["iam:PassedToService"], null) != null
    ])
    error_message = "Statement iam:PassRole PHẢI có condition iam:PassedToService = ecs-tasks.amazonaws.com."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.deploy.json).Statement :
      !contains(flatten([s.Action]), "iam:PassRole") ||
      !contains(flatten([s.Resource]), "*")
    ])
    error_message = "iam:PassRole không được dùng Resource = \"*\" — phải liệt kê đúng các role ECS task."
  }

  # Xoá snapshot phải bị ghim theo tiền tố tên. Nếu Resource là "*" thì một bug
  # trong script CI xoá được cả snapshot người tạo tay.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.deploy.json).Statement :
      !contains(flatten([s.Action]), "rds:DeleteDBSnapshot") ||
      alltrue([for r in flatten([s.Resource]) : strcontains(r, ":snapshot:pre-migrate-")])
    ])
    error_message = "rds:DeleteDBSnapshot phải giới hạn theo ARN pattern snapshot:pre-migrate-* — pipeline không được xoá snapshot do người tạo tay."
  }

  # Ghi S3 phải giới hạn theo prefix, không phải cả bucket.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.deploy.json).Statement :
      !contains(flatten([s.Action]), "s3:PutObject") ||
      alltrue([for r in flatten([s.Resource]) : endswith(r, "/migrations/*")])
    ])
    error_message = "s3:PutObject phải giới hạn vào prefix migrations/, không phải toàn bucket artifacts."
  }
}

# ─────────────────────────────────────────────────────────────────
# Pipeline không được tự làm phát sinh chi phí. Đây là ràng buộc thiết kế, và
# nó chỉ đứng vững nếu IAM không cấp quyền — comment trong workflow thì ai cũng
# sửa được.
# ─────────────────────────────────────────────────────────────────
run "deploy_role_khong_the_tu_bat_ha_tang_ton_phi" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.deploy.json).Statement :
      alltrue([
        for a in flatten([s.Action]) :
        !contains([
          "autoscaling:SetDesiredCapacity",
          "autoscaling:UpdateAutoScalingGroup",
          "rds:StartDBInstance",
          "rds:CreateDBInstance",
          "rds:ModifyDBInstance",
          "ec2:RunInstances",
          "ec2:CreateNatGateway",
          "ec2:AllocateAddress",
          "elasticloadbalancing:CreateLoadBalancer",
          "ecs:CreateService",
        ], a)
      ])
    ])
    error_message = "Role deploy KHÔNG được có quyền bật hạ tầng tính tiền theo giờ. Bật/tắt là việc của scripts/up.sh do người chạy, không phải của một push vào main — mỗi giờ bật tốn $0.1954 và một pipeline tự bật là chi phí không có trần."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.deploy.json).Statement :
      alltrue([for a in flatten([s.Action]) : !startswith(a, "iam:Create") && !startswith(a, "iam:Put") && !startswith(a, "iam:Attach")])
    ])
    error_message = "Role deploy không được tự tạo hay nới IAM — thay đổi quyền phải đi qua terraform apply do người chạy."
  }

  # Hai assert trên MÙ VỚI WILDCARD, và đó là lỗ đủ to để vô hiệu hoá cả hai:
  # `"autoscaling:*"` không khớp chuỗi chính xác nào trong denylist và cũng
  # không startswith("iam:Create"), nhưng nó cấp đúng autoscaling:SetDesiredCapacity
  # — tức cấp đúng thứ mà denylist tồn tại để cấm. `"rds:*"` và `"*"` trần cũng
  # vậy. Nên chốt bằng một điều kiện không có kẽ: policy này không có action nào
  # chứa dấu *.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.deploy.json).Statement :
      alltrue([for a in flatten([s.Action]) : !strcontains(a, "*")])
    ])
    error_message = "Action của role deploy phải là tên ĐẦY ĐỦ, không được chứa dấu *. Một wildcard theo service làm hai assert denylist phía trên vô hiệu. Nếu sau này thật sự cần một nhóm action mà AWS chỉ cấp được dạng wildcard (ví dụ ssmmessages:* cho ECS Exec): trước hết thử liệt kê tường minh các action con; chỉ khi AWS không cho liệt kê thì mới nới assert này thành allowlist ĐÚNG tiền tố đó (và không bao giờ gồm autoscaling, rds hay ec2), kèm một đoạn trong policy.tf nói vì sao service đó là ngoại lệ. Đừng xoá assert."
  }
}

# ─────────────────────────────────────────────────────────────────
# Ba run block trên chỉ assert điều PHỦ ĐỊNH — "không được có quyền X". Chúng
# không bắt được lỗi ngược chiều: một lần trim quyền quá tay. Lỗi đó không hiện
# ra ở fmt, validate hay plan; nó hiện ra ở lần deploy đầu tiên sau apply, dưới
# dạng AccessDenied giữa lúc migration đã chạy xong. Run block này là chỗ duy
# nhất canh việc policy cấp ĐỦ.
#
# Danh sách dưới đây là hợp của mọi lệnh `aws` trong .github/workflows/deploy.yml.
# Hai action CỐ TÌNH không có trong danh sách: ecr:BatchGetImage và
# ecr:GetDownloadUrlForLayer — policy vẫn cấp chúng vì chúng nằm trong policy
# push chuẩn của AWS, nhưng chưa có bằng chứng runtime rằng docker push gọi tới,
# nên chúng là ứng viên để xoá chứ không phải điều kiện bắt buộc.
# ─────────────────────────────────────────────────────────────────
run "deploy_role_cap_du_quyen_cho_moi_lenh_pipeline_goi" {
  command = plan

  assert {
    condition = length(setsubtract(
      [
        "ecr:GetAuthorizationToken",       # amazon-ecr-login
        "ecr:BatchCheckLayerAvailability", # docker push
        "ecr:InitiateLayerUpload",         # docker push
        "ecr:UploadLayerPart",             # docker push
        "ecr:CompleteLayerUpload",         # docker push
        "ecr:PutImage",                    # docker push
        "ecr:DescribeImages",              # bước "Image đã tồn tại chưa" (tag IMMUTABLE)
        "ecs:DescribeServices",            # preflight, và ghi lại revision cũ để rollback
        "ecs:ListContainerInstances",      # preflight
        "ecs:DescribeTaskDefinition",      # lấy revision hiện tại làm khuôn
        "ecs:RegisterTaskDefinition",      # revision mới chỉ đổi field image
        "ecs:UpdateService",               # trỏ 2 service sang revision mới, và rollback
        "ecs:RunTask",                     # task migrator
        "ecs:DescribeTasks",               # vòng poll lấy lastStatus + exitCode
        "iam:PassRole",                    # RegisterTaskDefinition tham chiếu 3 role
        "logs:GetLogEvents",               # in nguyên nhân khi migration fail
        "logs:DescribeLogStreams",         # nhánh dự phòng khi tên stream đoán sai
        "rds:DescribeDBInstances",         # preflight
        "rds:CreateDBSnapshot",            # điểm quay về cho dữ liệu
        "rds:DescribeDBSnapshots",         # kiểm chứng snapshot, và liệt kê để dọn
        "rds:DeleteDBSnapshot",            # dọn, giữ 3 cái mới nhất
        "s3:PutObject",                    # migrate-<sha>.sql
      ],
      flatten([
        for s in jsondecode(data.aws_iam_policy_document.deploy.json).Statement :
        flatten([s.Action])
      ])
    )) == 0
    error_message = "Policy role deploy THIẾU action mà deploy.yml gọi tới. Policy hiện cấp: ${join(", ", sort(flatten([for s in jsondecode(data.aws_iam_policy_document.deploy.json).Statement : flatten([s.Action])])))}. So với danh sách trong assert này để tìm cái thiếu. Nếu bạn vừa xoá một action vì cho là không dùng: nó CÓ được gọi — sửa deploy.yml trước, rồi mới sửa danh sách này."
  }
}

# ─────────────────────────────────────────────────────────────────
# Role plan: điều quan trọng nhất là nó KHÔNG đọc được tfstate, vì tfstate chứa
# master password của RDS ở dạng plaintext (random_password luôn nằm trong
# state — đó là bản chất của Terraform, không sửa được ở phía ta).
# ─────────────────────────────────────────────────────────────────
run "plan_role_khong_doc_duoc_tfstate_va_khong_giai_ma_duoc_secret" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.plan_deny.json).Statement :
      s.Effect == "Deny"
    ])
    error_message = "Inline policy của role plan chỉ được chứa statement Deny. Mọi quyền Allow đến từ managed ReadOnlyAccess; thêm Allow ở đây là mở đường mà chính policy này tồn tại để bịt."
  }

  assert {
    condition = anytrue([
      for s in jsondecode(data.aws_iam_policy_document.plan_deny.json).Statement :
      contains(flatten([s.Action]), "s3:GetObject")
    ])
    error_message = "Phải Deny s3:GetObject — đó là đường đọc terraform.tfstate, tức đọc master password của RDS."
  }

  assert {
    condition = anytrue([
      for s in jsondecode(data.aws_iam_policy_document.plan_deny.json).Statement :
      contains(flatten([s.Action]), "kms:Decrypt")
    ])
    error_message = "Phải Deny kms:Decrypt — thiếu nó thì SecureString trong Parameter Store đọc được ra plaintext."
  }

  assert {
    condition = anytrue([
      for s in jsondecode(data.aws_iam_policy_document.plan_deny.json).Statement :
      anytrue([for a in flatten([s.Action]) : startswith(a, "ssm:GetParameter")])
    ])
    error_message = "Phải Deny ssm:GetParameter* trên /hushstore/* — cùng lập luận đã dùng cho container-instance-role ở Phase 1."
  }

  assert {
    condition = anytrue([
      for s in jsondecode(data.aws_iam_policy_document.plan_deny.json).Statement :
      contains(flatten([s.Action]), "sts:AssumeRole")
    ])
    error_message = "Phải Deny sts:AssumeRole — một job chạy từ PR không được đổi vai sang role khác."
  }
}
