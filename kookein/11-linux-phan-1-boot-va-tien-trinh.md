# Buổi 11 — Linux, phần 1: ba bản phân phối và sự cố boot đã gặp thật

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần III, mục "Linux — hệ điều hành chạy bên dưới tất cả", từ đầu tới hết
> mục boot (dòng 673–786).
> Đề bài mở đầu bằng yêu cầu tìm hiểu Linux — buổi này trả lời trực tiếp câu
> đó bằng một sự cố thật đã xảy ra hai lần trong dự án, không phải lý thuyết
> suông.

## Không phải một Linux, mà ba

Điểm hay bị bỏ qua: hệ thống này chạy **ba bản phân phối Linux khác nhau
cùng lúc**, mỗi bản chọn có lý do:

| Ở đâu | Bản phân phối | Vì sao chọn nó |
|---|---|---|
| **Máy EC2** (host) | Amazon Linux 2023, bản ECS-optimized | AWS đã cài sẵn `docker` và `ecs-agent`. Không phải cài gì → `user_data` gần như không phải làm gì |
| **Container `web`** | Alpine (`nginx:alpine`) | Nhỏ nhất — image ~50MB. Chỉ cần serve file tĩnh nên không cần gì hơn |
| **Container `api`** | Debian (`aspnet:10.0`) | Microsoft build image .NET trên Debian. Đây là lý do trong `Dockerfile` ta gõ `useradd` và `update-ca-certificates` — lệnh của Debian; Alpine dùng `adduser` và cơ chế khác |
| **Task `migrator` / `seeder`** | Debian (`runtime-deps:10.0`, `debian:12-slim`) | Chạy-một-lần-rồi-thoát. `seeder` cần `postgresql-client-17` từ apt repo của PostgreSQL |

## musl vs glibc — khác biệt sâu nhất

Alpine dùng thư viện C tên **musl**, còn Debian và Amazon Linux dùng
**glibc**. (**Thư viện C** là lớp trung gian mà gần như mọi chương trình gọi
để nhờ kernel làm việc — mở file, mở socket, cấp bộ nhớ. Một file thực thi
build sẵn không tự chứa lớp đó, nó chỉ *gọi tên hàm* và trông chờ tìm thấy
lúc chạy.) Hai thư viện khác nhau nghĩa là tên hàm và cách gọi lệch nhau, nên
file build cho bên này thiếu thứ bên kia cần — một file thực thi build trên
Debian không chắc chạy được trên Alpine.

HushStore không gặp vấn đề này vì **mỗi container tự mang runtime của nó**
(image đã đóng gói đủ mọi thư viện cần).

## Kiến trúc CPU — chỗ tinh vi hơn musl/glibc

Máy phát triển hiện tại là Intel (`uname -m` → `x86_64`) và `t3.micro` cũng
Intel, nên mọi thứ vô tình khớp. Nhưng một máy Mac chip Apple Silicon hay một
CI runner Graviton đều là `arm64` — và lúc đó **hai trong bốn** image sẽ gãy,
mỗi cái vì một lý do khác:

| Image | Phụ thuộc kiến trúc? | Vì sao |
|---|---|---|
| `web` | không | nginx + file tĩnh; base image tự chọn đúng kiến trúc |
| `api` | không | .NET *framework-dependent* — `.dll` là bytecode IL, runtime kiến trúc nào cũng chạy |
| `migrator` | **có** | build *self-contained* `-r linux-x64` → ra file thực thi máy, chỉ chạy trên x64 |
| `seeder` | không | `postgresql-client-17` có cả `amd64` lẫn `arm64` trong apt repo |

Chỗ gãy của `migrator` là kiểu lỗi **khó tìm nhất**: build stage 1 tạo binary
x64, build stage 2 lấy base image theo kiến trúc **máy build** (không phải
kiến trúc đích). Trên máy arm64 sẽ ra một image arm64 chứa binary chỉ chạy
được x64. Build **xanh**, push ECR **thành công**, và lỗi chỉ hiện ra lúc ECS
*khởi động* container — nghĩa là mọi bước kiểm tra tự động thông thường đều
"qua", chỉ runtime mới lộ.

Cách phòng ngừa: ghim kiến trúc ngay trong `Dockerfile`, ở **mọi** stage:

```dockerfile
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0        AS build
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
```

Bài học: hiện tại nó đúng nhờ **may mắn** (máy build tình cờ là x86_64), ghim
lại để nó đúng vì **thiết kế**.

## Boot sequence — và deadlock đã gặp hai lần thật

Trình tự boot của Amazon Linux 2023:

```
kernel  →  systemd  →  cloud-init  →  cloud-final.service  →  user_data
                                              ↓ (xong)
                                        ecs.service khởi động
```

- **`systemd`** là tiến trình số 1, quản mọi dịch vụ. Mỗi dịch vụ là một
  *unit* (`docker.service`, `ecs.service`), có thứ tự phụ thuộc khai báo
  bằng `After=`.
- **`cloud-init`** là cơ chế chuẩn để một máy ảo tự cấu hình lúc boot đầu
  tiên. Nó là chỗ AWS chạy đoạn `user_data` bạn viết trong Launch Template.

**Bẫy nằm ở chỗ:** `ecs.service` khai báo `After=docker.service
cloud-final.service`, mà `user_data` **chạy bên trong** `cloud-final`. Nên
nếu trong `user_data` ta gọi:

```bash
systemctl enable --now ecs      # ❌ TUYỆT ĐỐI KHÔNG
```

thì thành **deadlock ba tầng**: `systemctl` chờ unit `ecs` active → unit
`ecs` chờ `cloud-final` xong → `cloud-final` chờ `user_data` trả về →
`user_data` đang chờ `systemctl`. Không bên nào nhường.

Triệu chứng đo được lúc đó:

```bash
cloud-init status              # running — đứng mãi ở modules-final
systemctl is-active ecs        # inactive (dead)
ps -ef | grep systemctl        # `systemctl enable --now ecs` treo,
                               # là tiến trình con của cloud-init modules --mode=final
```

Máy **chạy bình thường**, SSM vào được, nhưng không bao giờ đăng ký vào ECS
cluster — nên `terraform apply` xanh mà không có container nào lên. Đây là
loại lỗi nguy hiểm nhất: mọi công cụ kiểm tra thông thường (Terraform, health
check của AWS) đều báo "ổn", chỉ khi vào máy chẩn đoán mới thấy.

Gỡ bằng cách `kill` tiến trình `systemctl` đang treo: agent lên ngay lập tức.

**Bản sửa** là **xoá dòng đó đi**, không thay bằng gì cả — trên AMI
ECS-optimized, unit `ecs` đã được `enable` sẵn từ trước, nó tự lên sau khi
cloud-init xong. Toàn bộ việc `user_data` cần làm chỉ là ghi một dòng cấu
hình:

```bash
echo "ECS_CLUSTER=hushstore" >> /etc/ecs/ecs.config
```

Bài học Linux: **đừng gọi `systemctl start` từ trong cloud-init.** Bài học
kiểm thử: có một test tự động
(`user_data_dung_thu_tu_va_khong_tu_khoi_dong_ecs_agent`) đọc `user_data`,
bỏ dòng comment ra, rồi bắt lỗi nếu thấy `systemctl`, `service ecs`, hoặc
`start ecs`. Sự cố đã trở thành một rào chắn vĩnh viễn — đây là ví dụ tốt
của việc "học từ sự cố" bằng cách biến nó thành test, không chỉ ghi vào tài
liệu.

## Bài tập đọc code

1. Tìm file `Dockerfile` (thường ở root repo hoặc `src/`) — xác nhận có
   dòng `FROM --platform=linux/amd64 ...` ở mọi stage.
2. Tìm `user_data` trong Launch Template
   ([`infra/tf/modules/ecs/`](../infra/tf/modules/ecs/)) — xác nhận **không
   có** dòng nào chứa `systemctl`, `service ecs`, hoặc `start ecs`.
3. Tìm test tự động nhắc ở trên (tìm từ khoá liên quan tới `user_data` trong
   [`infra/tf/modules/ecs/tests/`](../infra/tf/modules/ecs/tests/)) — đọc
   assertion để hiểu nó kiểm tra chính xác điều gì.

## Câu hỏi tự kiểm tra

1. Vì sao 3 bản phân phối Linux khác nhau không gây vấn đề tương thích, dù
   chúng dùng thư viện C khác nhau (musl vs glibc)?
2. Vấn đề kiến trúc CPU của `migrator` khác gì vấn đề musl/glibc — vì sao nó
   "khó tìm nhất"?
3. Giải thích deadlock ba tầng bằng lời của bạn: ai đang chờ ai?
4. Vì sao bản sửa là "xoá dòng `systemctl enable --now ecs`" mà không phải
   "sửa thứ tự gọi nó"?

<details>
<summary>Gợi ý đáp án</summary>

1. Vì mỗi container tự đóng gói (bundle) đủ runtime/thư viện nó cần bên
   trong image — không có chuyện một file thực thi build ở container này
   được chạy trực tiếp trong container khác.
2. Vấn đề musl/glibc chỉ xảy ra khi cố chạy một binary "trần" giữa hai loại
   container khác nhau — nhưng vì mỗi container tự mang runtime, sự cố này
   không có cơ hội xảy ra thật. Vấn đề kiến trúc CPU khó tìm hơn vì: (1) nó
   chỉ lộ khi build trên máy có kiến trúc khác (không phải luôn xảy ra), (2)
   build và push đều "xanh" — không có bước kiểm tra tự động nào phát hiện
   trước khi container thực sự khởi động trên máy đích.
3. `systemctl` (được gọi từ user_data) chờ cho unit `ecs` chuyển sang active
   → unit `ecs` không active được vì nó khai `After=cloud-final.service`,
   tức nó chờ `cloud-final` báo xong → `cloud-final` không báo xong được vì
   nó đang chờ `user_data` (script đang chạy trong nó) trả về → `user_data`
   không trả về được vì nó đang bị treo ở chính lệnh `systemctl` đó. Vòng
   tròn khép kín, không ai nhường ai.
4. Vì trên AMI ECS-optimized, unit `ecs` **đã được enable sẵn** — nó tự khởi
   động đúng lúc sau khi cloud-init hoàn tất, không cần ai gọi tay. Dòng lệnh
   đó là dư thừa và có hại, không phải thiếu thứ tự — nên xoá hẳn, không cần
   thay bằng lệnh gọi đúng thứ tự nào khác.

</details>

Buổi tiếp theo: [12-linux-phan-2-tai-nguyen-va-quyen.md](12-linux-phan-2-tai-nguyen-va-quyen.md).
