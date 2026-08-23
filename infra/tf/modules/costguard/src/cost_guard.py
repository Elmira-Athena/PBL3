"""Cost guard của HushStore — hạ mọi thứ tính tiền theo giờ về mức thấp nhất.

Chạy mỗi đêm 00:00 giờ Việt Nam qua EventBridge Scheduler (xem schedule.tf).

VÌ SAO TỒN TẠI
Rủi ro mà Lambda này bịt không phải "quên tắt một đêm" — cái đó có trần và
`status.sh` cho thấy ngay hôm sau. Nó bịt rủi ro KHÔNG có trần: AWS **tự khởi
động lại** một RDS đã `stopped` sau 7 ngày, không thông báo gì. Ở $0.098/giờ thì
một tuần chạy ngầm là $16.5, một tháng là $71 — tiêu mà không ai từng bấm gì.
Chạy mỗi đêm đưa cửa sổ đó từ 7 ngày về tối đa 24 giờ.

NÓ KHÔNG ĐƯA CHI PHÍ VỀ $0, VÀ ĐÓ LÀ CÓ CHỦ Ý
NAT Gateway ($0.0590/giờ) và ALB ($0.0252/giờ) do Terraform quản lý. Xoá chúng
bằng API sẽ làm state lệch thực tế, và lần `terraform apply` sau xử lý sai —
nhẹ thì tạo lại, nặng thì lỗi giữa apply và để hạ tầng nửa vời. Nên với hai
khoản đó Lambda chỉ ĐỌC rồi BÁO kèm đúng câu lệnh cần chạy; con người đóng vòng
lặp. IAM role của Lambda không cấp một action xoá nào — xem lambda.tf.

NÓ KHÔNG BẬT ĐƯỢC GÌ
Không có `rds:StartDBInstance` trong policy, và mã này không bao giờ gọi
SetDesiredCapacity với giá trị khác 0. Một cost guard có quyền bật là một cost
guard có thể gây ra đúng thứ nó tồn tại để chặn.

IM LẶNG KHI KHÔNG CÓ GÌ ĐỂ NÓI
Chạy đêm thứ hai khi mọi thứ đã tắt là một lần chạy THÀNH CÔNG và KHÔNG gửi
SNS. Một cảnh báo bắn mỗi đêm là một cảnh báo bị bỏ qua, và khi nó bị bỏ qua
thì đêm nó bắn vì lý do thật cũng bị bỏ qua. SNS chỉ đi khi (a) thật sự tắt
được cái gì, (b) phát hiện NAT/ALB còn sống, hoặc (c) có lỗi.
"""

import json
import os

import boto3
from botocore.exceptions import BotoCoreError, ClientError

# Đọc cấu hình ở tầng module, không đọc trong handler: thiếu một biến môi trường
# thì Lambda chết ngay ở init với KeyError nêu đúng tên biến, thay vì chạy được
# một nửa rồi bỏ sót đúng bước quan trọng.
PROJECT = os.environ["PROJECT"]
CLUSTER_NAME = os.environ["CLUSTER_NAME"]
ASG_NAME = os.environ["ASG_NAME"]
RDS_IDENTIFIER = os.environ["RDS_IDENTIFIER"]
SERVICE_NAMES = [s.strip() for s in os.environ["SERVICE_NAMES"].split(",") if s.strip()]
SNS_TOPIC_ARN = os.environ["SNS_TOPIC_ARN"]

# Câu lệnh người phải chạy khi còn NAT/ALB. Đưa nguyên văn vào email vì lúc 8 giờ
# sáng đọc điện thoại thì "vào repo tìm script tắt hạ tầng" là một bước đủ để
# việc bị hoãn sang chiều — và mỗi giờ hoãn là $0.0842.
DOWN_COMMAND = "bash infra/tf/scripts/down.sh"

# Client tạo ở tầng module để lần chạy warm không phải dựng lại session.
_ecs = boto3.client("ecs")
_autoscaling = boto3.client("autoscaling")
_rds = boto3.client("rds")
_ec2 = boto3.client("ec2")
_elbv2 = boto3.client("elbv2")
_sns = boto3.client("sns")


def _err_code(exc):
    """Mã lỗi AWS, hoặc None nếu đây không phải ClientError."""
    if isinstance(exc, ClientError):
        return exc.response.get("Error", {}).get("Code")
    return None


def _err_text(exc):
    if isinstance(exc, ClientError):
        error = exc.response.get("Error", {})
        return f"{error.get('Code', '?')}: {error.get('Message', exc)}"
    return repr(exc)


def _stop_ecs_services(actions, notes, findings, errors):
    """BƯỚC 1 — ECS service về 0. PHẢI chạy TRƯỚC khi hạ ASG.

    Thứ tự ở đây là RÀNG BUỘC, không phải khuyến nghị. Nếu hạ ASG trước thì ECS
    scheduler thấy task chết cùng instance và liên tục thử xếp lại task lên phần
    capacity đang biến mất; nó không dừng cho tới khi desiredCount về 0. Đó đúng
    là vòng lặp mà `down.sh` đã gặp thật, và triệu chứng của nó là apply/drain
    treo hàng chục phút trong khi NAT + ALB + EC2 vẫn tính tiền từng giây.
    """
    if not SERVICE_NAMES:
        return

    try:
        response = _ecs.describe_services(cluster=CLUSTER_NAME, services=SERVICE_NAMES)
    except (ClientError, BotoCoreError) as exc:
        errors.append(f"Không đọc được ECS service trong cluster {CLUSTER_NAME} — {_err_text(exc)}")
        return

    # describe_services KHÔNG ném lỗi khi service không tồn tại: nó trả về mục đó
    # trong `failures` với reason MISSING. Đó là trạng thái BÌNH THƯỜNG của dự án
    # này — service bị `enable_alb` gate nên nó không tồn tại khi stack đang tắt.
    # Vì thế nó đi vào `notes` (chỉ ghi log) chứ không vào `errors`: tính là lỗi
    # thì mỗi đêm stack đã tắt sẽ gửi một email vô nghĩa, và đó là cách nhanh
    # nhất để người nhận học cách bỏ qua email của hệ thống này.
    for failure in response.get("failures", []):
        notes.append(
            f"ECS service {failure.get('arn', '?')} không tồn tại "
            f"({failure.get('reason', '?')}) — bình thường khi enable_alb = false."
        )

    for service in response.get("services", []):
        name = service.get("serviceName", "?")
        desired = service.get("desiredCount", 0)

        if service.get("status") != "ACTIVE":
            notes.append(f"ECS service {name} đang ở trạng thái {service.get('status')} — bỏ qua.")
            continue

        if desired == 0:
            notes.append(f"ECS service {name} đã ở desiredCount = 0.")
            continue

        try:
            _ecs.update_service(cluster=CLUSTER_NAME, service=name, desiredCount=0)
            actions.append(f"ECS service {name}: desiredCount {desired} → 0.")
        except (ClientError, BotoCoreError) as exc:
            errors.append(f"Không hạ được ECS service {name} về 0 — {_err_text(exc)}")


def _scale_asg_to_zero(actions, notes, findings, errors):
    """BƯỚC 2 — ASG desired về 0, và chỉ khi nó đang > 0.

    Gọi SetDesiredCapacity(0) trên một group đã ở 0 không lỗi, nhưng nó tạo một
    activity mới trong lịch sử ASG mỗi đêm và làm nhiễu đúng chỗ người ta vào
    xem khi cần biết "đêm nào instance bị hạ". Đọc trước, ghi sau.

    HonorCooldown = False: cooldown mặc định tồn tại để chống scale-in dồn dập;
    ở đây việc hạ về 0 là quyết định đã chốt, không phải một phản ứng theo tải,
    nên chờ cooldown chỉ có nghĩa là trả thêm tiền EC2 trong lúc chờ.
    """
    try:
        response = _autoscaling.describe_auto_scaling_groups(AutoScalingGroupNames=[ASG_NAME])
    except (ClientError, BotoCoreError) as exc:
        errors.append(f"Không đọc được Auto Scaling Group {ASG_NAME} — {_err_text(exc)}")
        return

    groups = response.get("AutoScalingGroups", [])
    if not groups:
        # Đây là lỗi thật, không phải trạng thái bình thường: ASG do Terraform
        # quản lý và tồn tại liên tục (min_size = 0), nên "không tìm thấy" nghĩa
        # là tên đã lệch giữa module ecs và module costguard. Một cost guard
        # đang canh một cái tên không tồn tại là một cost guard không canh gì,
        # và nó phải ồn lên chứ không được im lặng.
        errors.append(
            f"Không tìm thấy Auto Scaling Group {ASG_NAME}. Kiểm tra biến asg_name "
            f"của module costguard có khớp output asg_name của module ecs không."
        )
        return

    desired = groups[0].get("DesiredCapacity", 0)
    if desired == 0:
        notes.append(f"ASG {ASG_NAME} đã ở desired = 0.")
        return

    try:
        _autoscaling.set_desired_capacity(
            AutoScalingGroupName=ASG_NAME,
            DesiredCapacity=0,
            HonorCooldown=False,
        )
        actions.append(f"ASG {ASG_NAME}: desired {desired} → 0 (EC2 $0,0132/giờ về $0).")
    except (ClientError, BotoCoreError) as exc:
        errors.append(f"Không hạ được ASG {ASG_NAME} về 0 — {_err_text(exc)}")


def _stop_rds(actions, notes, findings, errors):
    """BƯỚC 3 — stop RDS, và CHỈ khi status là `available`.

    StopDBInstance trên một instance không `available` trả `InvalidDBInstanceState`
    — kể cả khi trạng thái đó là `stopping` hay `stopped`, tức là chính cái đích
    ta muốn. Gọi mù sẽ biến "đêm nay không có gì để làm" thành một lỗi giả, và
    một lỗi giả mỗi đêm thì đêm có lỗi thật không ai nhận ra.

    Đây là bước quan trọng nhất trong cả Lambda: RDS là khoản duy nhất trong
    thiết kế có thể tự bật lại mà không ai bấm gì.
    """
    try:
        response = _rds.describe_db_instances(DBInstanceIdentifier=RDS_IDENTIFIER)
    except (ClientError, BotoCoreError) as exc:
        errors.append(f"Không đọc được RDS {RDS_IDENTIFIER} — {_err_text(exc)}")
        return

    instances = response.get("DBInstances", [])
    if not instances:
        errors.append(f"Không tìm thấy RDS instance {RDS_IDENTIFIER}.")
        return

    status = instances[0].get("DBInstanceStatus", "unknown")
    if status != "available":
        notes.append(
            f"RDS {RDS_IDENTIFIER} đang ở trạng thái '{status}' — không gọi StopDBInstance "
            f"(chỉ trạng thái 'available' mới stop được)."
        )
        return

    try:
        _rds.stop_db_instance(DBInstanceIdentifier=RDS_IDENTIFIER)
        actions.append(
            f"RDS {RDS_IDENTIFIER}: đã phát lệnh stop ($0,098/giờ về $0,004/giờ tiền storage). "
            f"Mất khoảng 5 phút để về 'stopped'."
        )
    except (ClientError, BotoCoreError) as exc:
        # Cửa sổ tranh chấp: giữa describe và stop có thể có một lần chạy khác
        # (hoặc `down.sh` do người chạy) đã stop trước. Trạng thái đích đã đạt
        # được, nên đây không phải lỗi. Đây cũng là lớp bù cho việc KHÔNG đặt
        # được reserved_concurrent_executions = 1 — xem lambda.tf.
        if _err_code(exc) in ("InvalidDBInstanceState", "DBInstanceNotFound"):
            notes.append(
                f"RDS {RDS_IDENTIFIER}: đã có tiến trình khác stop trước "
                f"({_err_code(exc)}) — trạng thái đích vẫn đạt được."
            )
        else:
            errors.append(f"Không stop được RDS {RDS_IDENTIFIER} — {_err_text(exc)}")


def _detect_terraform_owned_leftovers(actions, notes, findings, errors):
    """BƯỚC 4 — CHỈ ĐỌC. Hai khoản Lambda không được tự tắt.

    Phạm vi tìm bị giới hạn theo dự án (tag Project cho NAT, tiền tố tên cho
    load balancer) chứ không quét cả region. Lý do rất cụ thể: account này còn
    một người khác dùng để làm lab AWS (xem comment ở aws_budgets_budget). Báo
    NAT của người đó là gửi một email mỗi đêm mà người nhận không làm gì được —
    tức là phá đúng tính chất im-lặng-khi-không-có-gì-để-nói.

    Đánh đổi phải biết: filter tag Project dựa vào `default_tags` của provider
    (xem envs/prod/providers.tf). Nếu ai đó bỏ default_tags thì bước này lặng lẽ
    không thấy gì. Bù lại, việc bỏ default_tags hiện ra trong `terraform plan`
    dưới dạng thay đổi tag của hàng chục resource, nên nó không phải thay đổi
    lọt qua được mà không ai thấy.
    """
    try:
        nat_response = _ec2.describe_nat_gateways(
            Filter=[
                {"Name": "state", "Values": ["pending", "available"]},
                {"Name": "tag:Project", "Values": [PROJECT]},
            ]
        )
        for gateway in nat_response.get("NatGateways", []):
            findings.append(
                f"NAT Gateway {gateway.get('NatGatewayId')} còn sống — $0,0590/giờ, "
                f"tức $1,42/ngày. Terraform quản lý resource này nên Lambda không xoá."
            )
    except (ClientError, BotoCoreError) as exc:
        errors.append(f"Không kiểm được NAT Gateway — {_err_text(exc)}")

    try:
        paginator = _elbv2.get_paginator("describe_load_balancers")
        for page in paginator.paginate():
            for load_balancer in page.get("LoadBalancers", []):
                name = load_balancer.get("LoadBalancerName", "")
                if not name.startswith(f"{PROJECT}-"):
                    continue
                findings.append(
                    f"Load balancer {name} còn sống (state "
                    f"{load_balancer.get('State', {}).get('Code', '?')}) — $0,0252/giờ, "
                    f"tức $0,60/ngày. Terraform quản lý resource này nên Lambda không xoá."
                )
    except (ClientError, BotoCoreError) as exc:
        errors.append(f"Không kiểm được load balancer — {_err_text(exc)}")


def _build_message(actions, findings, errors):
    """Nội dung email. Tiếng Việt có dấu, và có đúng câu lệnh cần chạy."""
    lines = [f"Cost guard của {PROJECT} vừa chạy.", ""]

    if actions:
        lines.append("ĐÃ TẮT:")
        lines += [f"  - {item}" for item in actions]
        lines.append("")

    if findings:
        lines.append("CÒN TÍNH TIỀN, LAMBDA KHÔNG TỰ TẮT ĐƯỢC:")
        lines += [f"  - {item}" for item in findings]
        lines += [
            "",
            "Hai khoản này do Terraform quản lý. Xoá bằng API sẽ làm state lệch thực tế",
            "và lần apply sau xử lý sai, nên phải tắt bằng đúng câu lệnh dưới đây:",
            "",
            f"    {DOWN_COMMAND}",
            "",
        ]

    if errors:
        lines.append("LỖI TRONG LÚC CHẠY (các bước còn lại vẫn đã được thử):")
        lines += [f"  - {item}" for item in errors]
        lines += [
            "",
            "Kiểm tra trạng thái thật bằng: bash infra/tf/scripts/status.sh",
            "",
        ]

    lines.append("Log đầy đủ: CloudWatch Logs, log group /aws/lambda/" + f"{PROJECT}-cost-guard")
    return "\n".join(lines)


def _build_subject(findings, errors):
    """Subject của SNS, và nó BUỘC phải là ASCII in được.

    Đây không phải lựa chọn phong cách mà là ràng buộc của AWS: sns:Publish trả
    InvalidParameterException nếu Subject chứa ký tự ngoài ASCII in được, và độ
    dài tối đa là 100 ký tự. Vì thế ba chuỗi trạng thái dưới đây viết không dấu.
    Phần THÂN mail (Message) không bị ràng buộc này nên nó là tiếng Việt có dấu
    đầy đủ — xem _build_message, và đó là chỗ chứa toàn bộ nội dung thật.
    """
    if errors:
        state = "CO LOI"
    elif findings:
        state = "CON NAT/ALB SONG"
    else:
        state = "da tat xong"

    return f"[{PROJECT}] cost guard: {state}"[:100]


def lambda_handler(event, context):
    actions = []
    notes = []
    findings = []
    errors = []

    # Bốn bước chạy độc lập, và một bước lỗi KHÔNG được ngăn các bước sau: tắt
    # được 3 trong 4 thứ vẫn tốt hơn tắt được 0. Từng bước đã tự bọc ClientError
    # ở trong; vòng try ở đây là lớp cuối cho lỗi ngoài dự kiến (KeyError vì AWS
    # đổi shape response, TypeError vì một sửa đổi sai) — thiếu nó thì một bug ở
    # bước 1 làm RDS không bao giờ được stop, tức mất đúng lý do Lambda tồn tại.
    for step in (
        _stop_ecs_services,
        _scale_asg_to_zero,
        _stop_rds,
        _detect_terraform_owned_leftovers,
    ):
        try:
            step(actions, notes, findings, errors)
        except Exception as exc:  # noqa: BLE001
            errors.append(f"Bước {step.__name__} lỗi ngoài dự kiến — {exc!r}")

    summary = {
        "actions": actions,
        "notes": notes,
        "findings": findings,
        "errors": errors,
    }
    # In cả `notes` xuống CloudWatch dù chúng không đi vào email: khi cần biết
    # "đêm 20 tháng 8 RDS đang ở trạng thái gì" thì đây là chỗ duy nhất còn dữ
    # liệu. Retention 3 ngày, xem lambda.tf.
    print(json.dumps(summary, ensure_ascii=False))

    if actions or findings or errors:
        try:
            _sns.publish(
                TopicArn=SNS_TOPIC_ARN,
                Subject=_build_subject(findings, errors),
                Message=_build_message(actions, findings, errors),
            )
        except (ClientError, BotoCoreError) as exc:
            # Không thêm vào `errors` rồi tự gửi lại — vòng đó không có đáy.
            # Ghi log và để `raise` phía dưới làm lần chạy đỏ trong CloudWatch.
            print(f"Không gửi được SNS: {_err_text(exc)}")
            errors.append(f"Không gửi được SNS — {_err_text(exc)}")
    # Không có gì tắt được, không phát hiện gì, không lỗi → KHÔNG publish. Đây
    # là đường đi bình thường của hầu hết các đêm và nó phải hoàn toàn im lặng.

    if errors:
        # raise để lần chạy hiện ra là THẤT BẠI trong CloudWatch/Lambda metrics.
        # Không raise thì một Lambda lỗi mỗi đêm vẫn báo "Success" và biểu đồ
        # Errors phẳng — tức là không có cách nào phát hiện nó đã ngừng bảo vệ.
        # Scheduler sẽ retry tối đa 2 lần (xem schedule.tf); các bước đã thành
        # công là idempotent nên retry không gây tác dụng phụ.
        raise RuntimeError(f"Cost guard gặp {len(errors)} lỗi: " + " | ".join(errors))

    return summary
