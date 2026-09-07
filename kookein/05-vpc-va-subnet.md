# Buổi 5 — VPC và 6 subnet

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần III, mục "Mạng — VPC và 6 subnet" (dòng 423–483).
> Mục tiêu buổi này: hiểu cấu trúc mạng cụ thể của HushStore — không chỉ khái
> niệm subnet chung (buổi 1), mà đúng 6 subnet, đúng CIDR, đúng route table.

## Bức tranh tổng

VPC của HushStore: `10.20.0.0/16`. Chọn `10.20` chứ không phải `10.0` để
không trùng với hạ tầng cũ trong lúc chuyển đổi song song (một chi tiết vận
hành, không phải yêu cầu kỹ thuật).

| Subnet | CIDR | AZ | Chứa | Có IP public? | Có đường ra Internet? |
|---|---|---|---|---|---|
| `public-a` | `10.20.0.0/24` | 1a | ALB, **NAT Gateway A** | Có | Trực tiếp qua IGW |
| `public-b` | `10.20.1.0/24` | 1b | ALB, **NAT Gateway B** | Có | Trực tiếp qua IGW |
| `app-a` | `10.20.10.0/24` | 1a | EC2 chạy container | **Không** | Chỉ đi ra, qua **NAT A** |
| `app-b` | `10.20.11.0/24` | 1b | EC2 chạy container | **Không** | Chỉ đi ra, qua **NAT B** |
| `db-a` | `10.20.20.0/24` | 1a | RDS | **Không** | **Không có** |
| `db-b` | `10.20.21.0/24` | 1b | RDS (subnet group) | **Không** | **Không có** |

*(**DB subnet group** = danh sách subnet ta khai cho RDS, để AWS biết được
phép đặt database vào những chỗ nào.)*

## Vì sao 3 tier chứ không phải 2 (chỉ web + db)

Vì db tier tách riêng thì mới viết được rule "chỉ app tier mới nói chuyện
được với database" ở tầng subnet (nhắc lại bài học buổi 1, mục 2: rule chỉ
đặt được lên biên giới). Nếu app và db cùng subnet, rule đó không tồn tại
được — vì không có biên giới nào giữa chúng.

Subnet không tốn phí, nên tách 3 tier là một lựa chọn **miễn phí đổi lấy một
lớp phòng thủ thật**.

## Vì sao mỗi tier có 2 AZ

ALB bắt buộc 2 AZ (đã học ở buổi 3). Db subnet group của RDS cũng đòi tối
thiểu 2 subnet ở 2 AZ.

⚠️ **Chỗ dễ đọc nhầm sơ đồ:** subnet group của RDS phủ 2 AZ là yêu cầu của
AWS cho **mọi** RDS, kể cả loại single-AZ (không bật Multi-AZ). Nhìn thấy
"RDS ở cả hai AZ" trên sơ đồ **không** phải bằng chứng Multi-AZ đang bật.
Bằng chứng thật là cờ `multi_az` trong cấu hình RDS — buổi 13 sẽ nói kỹ.

## Route table — một cho mỗi app subnet, không dùng chung

- **Public subnet**: `0.0.0.0/0` → **Internet Gateway**. Có đường vào và ra.
  Cả hai public subnet dùng **chung** một route table (đích của chúng giống
  hệt nhau — cùng trỏ IGW).
- **App subnet**: **hai route table riêng**. `app-a` → NAT A, `app-b` →
  NAT B.
- **Db subnet**: **không có dòng `0.0.0.0/0` nào**. Không đường ra, không
  đường vào.

**Vì sao app tier phải tách route table, còn public tier thì không?** Vì một
route table chỉ chứa được **một** dòng `0.0.0.0/0`. Nếu dùng chung một route
table, hai AZ buộc phải đi chung một NAT — và lúc đó dựng NAT thứ hai cũng vô
nghĩa, vì không có cách nào trỏ `app-b` sang nó. Tách route table là **điều
kiện cần** của việc có NAT theo từng AZ, không phải một tuỳ chọn làm cho đẹp.
Route table không tính phí.

## Vì sao hai NAT Gateway chứ không phải một

NAT Gateway nằm *trong* một subnet cụ thể, nên nó gắn chặt với đúng một AZ và
**không tự chuyển sang AZ khác** khi AZ đó hỏng.

Với chỉ một NAT:
- Mọi byte từ `app-b` đi ra Internet phải sang AZ-a trước ⇒ chịu thêm phí
  **cross-AZ $0.01/GB mỗi chiều**, cộng trên $0.045/GB xử lý của NAT. Khoản
  này không hiện ở dòng hoá đơn nào tên "NAT" — dễ bị bỏ sót khi ước tính chi
  phí.
- Mất AZ-a thì `app-b` mất luôn đường ra: không pull được image từ ECR ⇒ ECS
  **không dựng lại được task**, và SSM cũng đứt.

⚠️ **Nhưng phải nói đúng thứ NAT thứ hai mua được:** nó chỉ cứu **egress**
(đường đi ra) — deploy, ECR, SSM. Đường phục vụ người dùng thật (ALB → EC2 →
RDS) đi hoàn toàn trong VPC và **không chạm NAT**. Muốn hệ thống sống sót khi
mất một AZ, thứ phải bật là **Multi-AZ của RDS** (buổi 13), không phải NAT.
Giá của mỗi NAT: **$0.059/giờ**.

## S3 Gateway Endpoint

Một "cửa riêng" đi tới S3 ngay trong VPC, **miễn phí**. Nhờ nó, traffic
đọc/ghi ảnh sản phẩm không cần đi qua NAT Gateway — tiết kiệm phí data
($0.045/GB) và vẫn hoạt động ngay cả khi NAT đã bị tắt (ví dụ lúc đang tiết
kiệm chi phí).

## Bài tập đọc code

Mở [`infra/tf/modules/network/vpc.tf`](../infra/tf/modules/network/vpc.tf):

1. Tìm 6 resource `aws_subnet` — so khớp CIDR với bảng ở mục 1. Có đúng
   `10.20.0.0/24`, `10.20.1.0/24`, `10.20.10.0/24`, `10.20.11.0/24`,
   `10.20.20.0/24`, `10.20.21.0/24` không?
2. Tìm resource liên quan tới NAT Gateway — có đúng 2 cái, mỗi cái gắn với
   subnet khác nhau không?
3. Tìm resource `aws_route_table` cho app subnet — xác nhận có 2 cái riêng
   biệt, mỗi cái trỏ tới một NAT Gateway khác nhau.
4. Tìm resource liên quan tới S3 endpoint (tìm từ khoá `s3` hoặc
   `vpc_endpoint`) — xác nhận loại là Gateway Endpoint (không phải Interface
   Endpoint, loại đó tính phí theo giờ).

## Câu hỏi tự kiểm tra

1. Vì sao "RDS xuất hiện ở cả hai AZ trên sơ đồ" không đủ để kết luận
   Multi-AZ đang bật?
2. NAT thứ hai giải quyết được vấn đề gì, và KHÔNG giải quyết được vấn đề gì?
3. Nếu gộp `public-a` và `public-b` dùng chung một route table nhưng vẫn giữ
   `app-a`/`app-b` tách route table riêng, có hợp lý không? Vì sao?
4. S3 Gateway Endpoint giúp tiết kiệm loại chi phí nào cụ thể?

<details>
<summary>Gợi ý đáp án</summary>

1. Vì AWS *bắt buộc* mọi RDS (kể cả single-AZ) phải có subnet group phủ 2 AZ
   — đó là yêu cầu hạ tầng, không phải bằng chứng của tính năng Multi-AZ.
   Bằng chứng thật là cờ `multi_az = true` trong cấu hình.
2. Giải quyết được: chi phí cross-AZ egress, và app tier ở AZ còn lại vẫn
   deploy/pull image được khi một AZ hỏng. KHÔNG giải quyết: đường phục vụ
   người dùng thật (không đi qua NAT), và không giúp RDS sống sót khi mất
   AZ — đó là việc của Multi-AZ.
3. Hợp lý, vì hai public subnet có đích giống nhau (đều trỏ ra IGW) — không
   có lý do kỹ thuật để tách. Ngược lại, app subnet cần tách vì mỗi cái phải
   trỏ tới NAT khác nhau (mỗi route table chỉ chứa một dòng `0.0.0.0/0`).
4. Tiết kiệm phí xử lý NAT Gateway ($0.045/GB) cho traffic đọc/ghi ảnh sản
   phẩm lên S3, vì traffic đó đi qua "cửa riêng" miễn phí thay vì qua NAT.

</details>

Buổi tiếp theo: [06-security-group.md](06-security-group.md).
