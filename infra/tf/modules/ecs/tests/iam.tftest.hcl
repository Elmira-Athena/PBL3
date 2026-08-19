provider "aws" {
  region  = "ap-southeast-1"
  profile = "hushstore"
}

variables {
  project                   = "hushstore-tftest"
  assets_bucket_arn         = "arn:aws:s3:::hushstore-public-assets"
  artifacts_bucket_arn      = "arn:aws:s3:::hushstore-artifacts"
  ssm_connection_string_arn = "arn:aws:ssm:ap-southeast-1:000000000000:parameter/hushstore/prod/connection-string"
  ssm_jwt_secret_arn        = "arn:aws:ssm:ap-southeast-1:000000000000:parameter/hushstore/prod/jwt-secret"
}

run "container_instance_role_khong_co_quyen_s3_hay_secret" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.instance_extra.json).Statement :
      !anytrue([for r in flatten([try(s.Resource, [])]) : startswith(r, var.assets_bucket_arn)])
    ])
    error_message = "Role của EC2 host KHÔNG được có quyền trên bucket ảnh sản phẩm — S3 ảnh là việc của task role, không phải của host (host chỉ được GetObject bucket artifacts để đọc seed SQL)."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.instance_extra.json).Statement :
      s.Effect == "Deny" || !anytrue([for a in flatten([s.Action]) : startswith(a, "ssm:GetParameter")])
    ])
    error_message = "Role của EC2 host KHÔNG được có statement Allow nào cho ssm:GetParameter — secret chỉ do ECS agent inject qua task execution role. (Statement Deny bịt lỗ hổng của managed policy thì được phép.)"
  }
}

run "task_app_role_chi_duoc_s3_tren_dung_bucket_anh" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.task_app.json).Statement :
      alltrue([
        for a in flatten([s.Action]) :
        startswith(a, "s3:") || startswith(a, "ssmmessages:")
      ])
    ])
    error_message = "Task role của app chỉ được có action s3:* và ssmmessages:* — không rds, không ecr, không ssm:GetParameter."
  }

  assert {
    condition = anytrue([
      for s in jsondecode(data.aws_iam_policy_document.task_app.json).Statement :
      contains(flatten([try(s.Resource, [])]), "arn:aws:s3:::hushstore-public-assets/*")
    ])
    error_message = "Quyền S3 phải giới hạn đúng vào object của bucket ảnh sản phẩm, không dùng Resource = *."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.task_app.json).Statement :
      !contains(flatten([try(s.Resource, [])]), "*") || alltrue([for a in flatten([s.Action]) : startswith(a, "ssmmessages:")])
    ])
    error_message = "Chỉ ssmmessages (ECS Exec) được dùng Resource = *; quyền S3 phải giới hạn theo ARN."
  }
}

run "task_execution_role_chi_doc_dung_2_parameter_khong_dung_wildcard_toan_bo" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.task_execution_extra.json).Statement :
      !contains(flatten([try(s.Resource, [])]), "arn:aws:ssm:*:*:parameter/*")
    ])
    error_message = "Task execution role không được đọc toàn bộ Parameter Store — phải liệt kê đúng ARN của 2 parameter cần dùng."
  }

  assert {
    condition = anytrue([
      for s in jsondecode(data.aws_iam_policy_document.task_execution_extra.json).Statement :
      contains(flatten([try(s.Resource, [])]), var.ssm_connection_string_arn)
    ])
    error_message = "Task execution role phải đọc được parameter connection-string để inject vào container."
  }
}

run "moi_role_chi_cho_dung_service_principal_duoc_assume" {
  command = plan

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.ec2_assume.json).Statement :
      contains(flatten([s.Principal.Service]), "ec2.amazonaws.com")
    ])
    error_message = "Trust policy của instance role chỉ cho ec2.amazonaws.com assume."
  }

  assert {
    condition = alltrue([
      for s in jsondecode(data.aws_iam_policy_document.ecs_tasks_assume.json).Statement :
      contains(flatten([s.Principal.Service]), "ecs-tasks.amazonaws.com")
    ])
    error_message = "Trust policy của task role chỉ cho ecs-tasks.amazonaws.com assume."
  }
}

run "instance_role_bi_deny_doc_secret_cua_ta_du_managed_policy_cho_phep" {
  command = plan

  assert {
    condition = anytrue([
      for s in jsondecode(data.aws_iam_policy_document.instance_extra.json).Statement :
      s.Effect == "Deny" && anytrue([for a in flatten([s.Action]) : a == "ssm:GetParameter"])
    ])
    error_message = "instance_extra phải có statement Deny bao gồm ssm:GetParameter — nếu không, AmazonSSMManagedInstanceCore (Resource = *) sẽ cho host đọc mọi SecureString, kể cả db-password."
  }
}
