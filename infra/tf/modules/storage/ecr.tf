locals {
  ecr_repos = ["api", "web", "migrator", "seeder"]
}

resource "aws_ecr_repository" "this" {
  for_each = toset(local.ecr_repos)

  name = "${var.project}-${each.key}"

  # IMMUTABLE: tag là git SHA nên không bao giờ được ghi đè. Đây là điều
  # kiện để rollback bằng cách trỏ lại task definition revision cũ thực sự
  # đáng tin — image của revision cũ chắc chắn còn nguyên nội dung.
  image_tag_mutability = "IMMUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  # Cho phép terraform destroy xoá repo kể cả khi còn image bên trong.
  force_delete = true

  tags = { Name = "${var.project}-${each.key}" }
}

resource "aws_ecr_lifecycle_policy" "this" {
  for_each = aws_ecr_repository.this

  repository = each.value.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Chi giu ${var.ecr_keep_images} image gan nhat"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = var.ecr_keep_images
        }
        action = { type = "expire" }
      }
    ]
  })
}
