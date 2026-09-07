# Buổi 10 — EC2 và ECS: chạy container trên máy chủ thật

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần III, mục "EC2 và ECS" (dòng 637–672).
> Mục tiêu buổi này: hiểu vì sao dùng container, vì sao chọn ECS **EC2 launch
> type** (không phải Fargate), và đánh đổi của việc chọn port cố định.

## Vì sao EC2 launch type, không phải Fargate

Đề bài yêu cầu **triển khai website thông qua EC2 Instance**. HushStore dùng
**ECS EC2 launch type**: container thật, nhưng chạy trên EC2 thật do nhóm tự
quản lý — khác **Fargate**, nơi AWS quản máy hộ và bạn không nhìn thấy EC2
nào cả.

Đây là lựa chọn bắt buộc theo đề bài, không phải sở thích — và nó có hệ quả
xuyên suốt: một số tính năng (namespace, cgroup tuỳ biến — buổi 12) chỉ có ở
EC2 launch type vì kernel thuộc về nhóm quản lý, không thuộc AWS quản như ở
Fargate.

## Vì sao container thay vì cài trực tiếp lên máy

Vì image container là **bất biến**: cùng một image chạy ở đâu cũng như nhau.
Không còn cảnh "máy tôi chạy được, máy production thì không". Deploy là đổi
sang image mới; rollback là trỏ về image cũ. Và máy chủ trở thành thứ
dùng-rồi-bỏ (disposable): xoá máy dựng lại vẫn đúng nguyên trạng, vì không
có state nào nằm trên máy (state thật nằm ở RDS, S3 — ngoài máy EC2).

## Các thành phần và vai trò

- **Launch Template** — khuôn mẫu: dùng AMI Amazon Linux đã có sẵn ECS agent,
  loại `t3.micro`, gắn `sg-web` (buổi 6), không gán IP public (buổi 5), và
  một đoạn `user_data` chạy lúc boot để khai báo máy này thuộc cluster nào +
  tạo 2GB swap (buổi 11 nói kỹ).
- **Auto Scaling Group** — `min 0 / max 1`. Đặt về 0 là máy bị xoá **cùng ổ
  đĩa** → chi phí về 0 thật (không chỉ dừng, mà xoá hẳn). Đặt về 1 là máy
  mới hoàn toàn được dựng lại.
- **ECS Cluster + Capacity Provider** — cluster là nhóm máy; capacity
  provider là cầu nối giữa cluster và ASG (quyết định ASG dựng/xoá máy khi
  cần chỗ chạy container).
- **Task Definition** — bản mô tả container: image nào, cần bao nhiêu RAM,
  mở port nào, biến môi trường và bí mật lấy từ đâu, log gửi đi đâu. Có 4
  cái: `web`, `api`, `migrator`, `seeder`.
- **ECS Service** — giữ container luôn chạy. Container chết thì service tự
  dựng lại. Có 2 cái: `web` và `api`. `migrator` và `seeder` **không có
  service** vì chúng là việc chạy-một-lần-rồi-thoát (chạy migration, seed
  dữ liệu, rồi exit — không cần giữ luôn chạy).

## Vì sao `bridge` network mode với port cố định

HushStore chọn **`bridge` mode với port cố định** 80 và 8080, thay vì để ECS
tự chọn port ngẫu nhiên (`awsvpc` mode hoặc dynamic port mapping).

Lý do là bảo mật: nếu để port động, SG phải mở dải `32768–65535` từ `sg-alb`
— hàng chục nghìn port. Với port cố định, SG chỉ mở đúng hai port (nhớ lại
bảng SG ở buổi 6).

**Đánh đổi:** mỗi máy chỉ chạy được **một** bản của mỗi container (vì hai
bản cùng loại không thể cùng bind một port cố định trên một máy), nên lúc
deploy có downtime ~20–40 giây (container cũ phải dừng trước khi container
mới lên, không có khoảng gối đầu).

Ở quy mô đồ án, đổi 30 giây downtime lấy một bề mặt tấn công nhỏ hơn hàng
nghìn lần (2 port thay vì ~33.000 port) là hợp lý — và đây là một **lựa chọn
có ý thức**, đáng nói khi báo cáo, không phải hạn chế bị bỏ quên. (Cũng chính
đánh đổi này là lý do hệ thống **không scale ngang được** dễ dàng — nhắc lại
ở buổi 15, Phần V "Giới hạn đã biết".)

## Bài tập đọc code

Mở [`infra/tf/modules/ecs/`](../infra/tf/modules/ecs/):

1. Tìm resource Auto Scaling Group — xác nhận `min_size = 0` và
   `max_size = 1` (hoặc biến tương đương).
2. Tìm 4 Task Definition (`web`, `api`, `migrator`, `seeder`) — với mỗi cái,
   xem `networkMode` có phải `bridge` không, và cổng host (`hostPort`) có cố
   định (80 hoặc 8080) không, hay để trống (`0` nghĩa là ECS tự chọn ngẫu
   nhiên — kỳ vọng KHÔNG phải trường hợp này ở HushStore).
3. Xác nhận `migrator` và `seeder` không có `aws_ecs_service` tương ứng
   (chỉ `web` và `api` có).

## Câu hỏi tự kiểm tra

1. Vì sao ECS EC2 launch type được chọn thay vì Fargate — đây có phải lựa
   chọn kỹ thuật tự do hay bị ràng buộc bởi đề bài?
2. `min 0 / max 1` của ASG khác gì so với "dừng máy" (stop) thông thường?
3. Port cố định giúp SG đơn giản hơn ở điểm nào cụ thể, và cái giá phải trả
   là gì?
4. Vì sao `migrator` và `seeder` không cần ECS Service?

<details>
<summary>Gợi ý đáp án</summary>

1. Bị ràng buộc bởi đề bài — đề yêu cầu "triển khai website thông qua EC2
   Instance", nên phải nhìn thấy và quản lý EC2 thật, không thể dùng Fargate
   (AWS quản máy hộ, ẩn hoàn toàn EC2).
2. Đặt ASG về 0 xoá luôn máy **và ổ đĩa** — chi phí về 0 hoàn toàn (không
   còn tính phí compute lẫn storage của EC2 đó). "Stop" một máy đơn thường
   vẫn giữ ổ đĩa (EBS) và vẫn tính phí storage.
3. SG chỉ cần mở đúng 2 port (80, 8080) từ `sg-alb`, thay vì phải mở cả dải
   32768–65535 nếu dùng port động. Cái giá: mỗi máy chỉ chạy được một bản
   mỗi loại container, nên deploy có downtime ngắn (~20-40s) vì phải dừng
   bản cũ trước khi lên bản mới.
4. Vì chúng là việc chạy-một-lần-rồi-thoát (migration DB, seed dữ liệu) —
   ECS Service có nhiệm vụ giữ container *luôn chạy*, không phù hợp với
   loại việc chạy xong rồi kết thúc.

</details>

Buổi tiếp theo: [11-linux-phan-1-boot-va-tien-trinh.md](11-linux-phan-1-boot-va-tien-trinh.md).
