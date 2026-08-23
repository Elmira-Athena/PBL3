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
  # Kiểm CẢ HAI trust policy, không chỉ role deploy. Từ khi role plan nhận hai
  # giá trị `sub` thì nó là policy PHỨC TẠP HƠN trong hai cái — mà chính chỗ
  # phức tạp hơn mới là chỗ một dấu `*` dễ lọt vào. Bỏ nó ra ngoài phạm vi kiểm
  # là bỏ đúng chỗ đáng kiểm.
  assert {
    condition = alltrue([
      for doc in [
        data.aws_iam_policy_document.assume_deploy.json,
        data.aws_iam_policy_document.assume_plan.json,
      ] :
      alltrue([for s in jsondecode(doc).Statement : !can(s.Condition.StringLike)])
    ])
    error_message = "KHÔNG trust policy nào được dùng StringLike — chỉ StringEquals trên giá trị sub đầy đủ. StringLike với wildcard sẽ khớp cả pull_request và mọi nhánh."
  }

  assert {
    condition = alltrue([
      for doc in [
        data.aws_iam_policy_document.assume_deploy.json,
        data.aws_iam_policy_document.assume_plan.json,
      ] :
      alltrue([
        for s in jsondecode(doc).Statement :
        alltrue([
          for v in flatten([s.Condition.StringEquals["token.actions.githubusercontent.com:sub"]]) :
          !strcontains(v, "*")
        ])
      ])
    ])
    error_message = "Giá trị của claim sub không được chứa dấu * ở BẤT KỲ trust policy nào — mỗi giá trị phải là một trigger cụ thể. Danh sách nhiều giá trị thì được (StringEquals vẫn so khớp chính xác); wildcard thì không."
  }

  # Role DEPLOY phải chỉ nhận ĐÚNG MỘT giá trị sub. Đây là tính chất bất đối
  # xứng cốt lõi: role plan nhận cả push-main và pull_request (nó chỉ đọc), còn
  # role deploy — role sửa được hạ tầng — không bao giờ được với tới từ một PR.
  # Thêm `pull_request` vào role deploy là biến "mở được PR" thành "deploy được".
  assert {
    condition = length(flatten([
      for s in jsondecode(data.aws_iam_policy_document.assume_deploy.json).Statement :
      flatten([s.Condition.StringEquals["token.actions.githubusercontent.com:sub"]])
    ])) == 1
    error_message = "Trust policy của role deploy phải nhận ĐÚNG MỘT giá trị sub. Nhiều giá trị nghĩa là có thêm một trigger assume được role ghi — và nếu giá trị thêm là pull_request thì bất kỳ ai mở được PR cũng deploy được."
  }

  # Thiếu điều kiện aud thì một token GitHub ký cho audience khác vẫn thoả.
  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.assume_deploy.json).Statement :
      try(s.Condition.StringEquals["token.actions.githubusercontent.com:aud"], null) != null
    ])
    error_message = "Trust policy phải có điều kiện trên claim aud = sts.amazonaws.com."
  }

  # Assert này TỪNG canh "hai role khớp hai giá trị sub khác nhau". Điều kiện đó
  # hết đúng khi role plan được cho nhận thêm push-main, để `terraform test` chạy
  # được trong CI dưới quy ước commit thẳng lên main. Hai tập sub vì thế GIAO
  # NHAU ở giá trị push-main — và điều cần canh không phải là "giao nhau hay
  # không", mà là CHIỀU của phần giao.
  #
  # Đặc quyền chảy một chiều: role plan là ReadOnlyAccess cộng 5 nhóm Deny (đọc
  # object S3, kms:Decrypt, ssm:GetParameter* trên /hushstore/*, đọc log,
  # sts:AssumeRole), còn role deploy mới là role push được image và sửa được
  # service. Nên "push-main tới được role plan" là chiều vô hại — người push
  # được lên main cũng push được một nhánh rồi mở PR, tức đã tới được role plan
  # từ trước.
  #
  # Chiều nguy hiểm là chiều ngược lại: một token của `pull_request` chạm được
  # role deploy. Nó biến "ai mở được PR" thành "ai deploy được", và đó là điều mà
  # một dấu `*` hoặc một lần copy-paste giữa hai data source sẽ mở ra mà không có
  # triệu chứng nào. Đó là chiều assert này canh.
  assert {
    condition = alltrue([
      for v in flatten([
        for s in jsondecode(data.aws_iam_policy_document.assume_deploy.json).Statement :
        [s.Condition.StringEquals["token.actions.githubusercontent.com:sub"]]
      ]) : !endswith(v, ":pull_request")
    ])
    error_message = "Trust policy của role DEPLOY không được nhận claim sub của một pull_request. Role plan nhận cả pull_request và push-main là có chủ ý (nó chỉ đọc); role deploy thì phải đúng một giá trị ref:refs/heads/<deploy_branch>, nếu không thì bất kỳ ai mở được PR cũng deploy được."
  }

  # Mặt còn lại của cùng một quyết định: nếu có người "sửa" chuyện gì bằng cách
  # bỏ pull_request khỏi role plan thì `terraform test` trên PR chết im lặng —
  # job đỏ ở bước assume-role, và không ai đọc nó như một lỗ hổng cấu hình.
  assert {
    condition = anytrue([
      for v in flatten([
        for s in jsondecode(data.aws_iam_policy_document.assume_plan.json).Statement :
        [s.Condition.StringEquals["token.actions.githubusercontent.com:sub"]]
      ]) : endswith(v, ":pull_request")
    ])
    error_message = "Trust policy của role PLAN phải còn nhận claim sub của pull_request — thiếu nó thì CI trên PR không assume được role nào và terraform test không chạy."
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
