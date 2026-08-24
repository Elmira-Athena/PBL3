# Service bị enable_alb gate: ECS CreateService trả lỗi nếu target group chưa
# gắn vào load balancer nào. ALB + TG + listener + service là một khối bật/tắt
# cùng nhau. Cluster, capacity provider, ASG và task definition thì KHÔNG bị
# gate — chúng miễn phí và cần tồn tại để run-task migrator.
#
# ─── health_check_grace_period_seconds suy ra từ đâu ─────────────────────────
# Target group (modules/alb/alb.tf) đặt interval = 15, unhealthy_threshold = 3.
# Nghĩa là ALB kết luận task unhealthy sau 15 × 3 = 45 giây fail liên tục, và
# ECS sẽ kill task đó. Grace period là khoảng thời gian ALB health check bị BỎ
# QUA sau khi task vào trạng thái RUNNING, nên nó phải lớn hơn thời gian khởi
# động thực tế — nếu không task đang boot bình thường vẫn bị giết ở giây thứ 45
# và service rơi vào vòng lặp replace mãi không stable.
# Grace period tính từ lúc task RUNNING, nên thời gian pull image KHÔNG nằm trong đó.

resource "aws_ecs_service" "web" {
  count = var.enable_alb ? 1 : 0

  name            = "${var.project}-web"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.web.arn
  desired_count   = var.service_desired_count

  capacity_provider_strategy {
    capacity_provider = aws_ecs_capacity_provider.this.name
    weight            = 1
    base              = 0
  }

  # Static host port 80 + đúng 1 instance nên không thể giữ bản cũ chạy trong
  # lúc bản mới lên — port sẽ xung đột. Hệ quả: deploy có downtime ~20-40s.
  # Đây là đánh đổi có ý thức để sg-web giữ đúng 2 ingress rule; đường
  # zero-downtime là dynamic port mapping + 2 instance, nhưng phải mở dải
  # 32768-65535 trên sg-web và tốn thêm một instance.
  deployment_minimum_healthy_percent = 0
  deployment_maximum_percent         = 100

  # Circuit breaker: ECS tự phát hiện deploy hỏng và tự lăn về revision trước.
  #
  # Không có nó, một task không bao giờ healthy sẽ để service ở "IN_PROGRESS"
  # hơn 30 phút rồi mới bỏ cuộc — trong lúc đó không ai biết deploy đã chết.
  # deploy.yml CÓ bước rollback thủ công, nhưng bước đó chỉ chạy khi job còn
  # sống; nếu runner bị huỷ hoặc `wait services-stable` timeout thì không ai lăn
  # về, và circuit breaker là lớp duy nhất còn lại.
  #
  # Miễn phí, và là cơ chế native của ECS chứ không phải logic ta tự viết.
  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  load_balancer {
    target_group_arn = var.tg_web_arn
    container_name   = "web"
    container_port   = 80
  }

  # nginx serve static file, sẵn sàng trong khoảng 1 giây. 60s là 45s (cửa sổ
  # unhealthy) cộng biên cho việc đăng ký target vào target group.
  health_check_grace_period_seconds = 60

  tags = { Name = "${var.project}-web" }

  # Service không tạo được trước khi cluster biết capacity provider của nó.
  depends_on = [aws_ecs_cluster_capacity_providers.this]

  lifecycle {
    # BẮT BUỘC từ Phase 2. Terraform vẫn ĐỊNH HÌNH task definition (image nào,
    # bao nhiêu RAM, secret nào, log đi đâu), nhưng revision ĐANG CHẠY do
    # GitHub Actions đăng ký: pipeline lấy taskdef hiện tại, đổi đúng field
    # image sang :<git-sha>, register revision mới, rồi update-service.
    #
    # Thiếu dòng này thì lần `terraform apply` kế tiếp thấy service đang trỏ một
    # revision không phải revision của mình và kéo nó về — tức ROLLBACK NGẦM về
    # image trong var.image_tag, không cảnh báo, không ai chủ ý. Đó là loại lỗi
    # chỉ lộ ra khi có người hỏi "sao bug đã sửa lại quay lại".
    ignore_changes = [task_definition]
  }
}

resource "aws_ecs_service" "api" {
  count = var.enable_alb ? 1 : 0

  name            = "${var.project}-api"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.service_desired_count

  capacity_provider_strategy {
    capacity_provider = aws_ecs_capacity_provider.this.name
    weight            = 1
    base              = 0
  }

  deployment_minimum_healthy_percent = 0
  deployment_maximum_percent         = 100

  # Circuit breaker: ECS tự phát hiện deploy hỏng và tự lăn về revision trước.
  #
  # Không có nó, một task không bao giờ healthy sẽ để service ở "IN_PROGRESS"
  # hơn 30 phút rồi mới bỏ cuộc — trong lúc đó không ai biết deploy đã chết.
  # deploy.yml CÓ bước rollback thủ công, nhưng bước đó chỉ chạy khi job còn
  # sống; nếu runner bị huỷ hoặc `wait services-stable` timeout thì không ai lăn
  # về, và circuit breaker là lớp duy nhất còn lại.
  #
  # Miễn phí, và là cơ chế native của ECS chứ không phải logic ta tự viết.
  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  load_balancer {
    target_group_arn = var.tg_api_arn
    container_name   = "api"
    container_port   = 8080
  }

  # 120s, gấp hơn hai lần cửa sổ 45s. Lý do API cần nhiều hơn web: health check
  # của nó là /health/ready, mà endpoint đó có AddDbContextCheck nên nó MỞ KẾT
  # NỐI TỚI RDS. Cộng dồn: .NET cold start trên t3.micro, cộng lần kết nối đầu
  # tới một RDS vừa được start (SQL Server Express khởi động chậm). 45s là quá
  # sát — task đang boot đúng cách vẫn có thể bị giết.
  health_check_grace_period_seconds = 120

  # ECS Exec — BẮT BUỘC ở mức service, không chỉ ở task definition. Thiếu nó thì
  # `aws ecs execute-command` trả lỗi dù task definition đã có initProcessEnabled
  # và task role đã có quyền ssmmessages. Đây là điều kiện để chạy kịch bản kiểm
  # thử số 10 của đề bài (chứng minh blast radius của task role là nhỏ).
  enable_execute_command = true

  tags = { Name = "${var.project}-api" }

  depends_on = [aws_ecs_cluster_capacity_providers.this]

  lifecycle {
    # Cùng lý do như service web ở trên: pipeline nắm revision, Terraform nắm
    # hình dạng. Xem comment đầy đủ ở aws_ecs_service.web.
    ignore_changes = [task_definition]
  }
}
