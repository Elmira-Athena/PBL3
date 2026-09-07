# Buổi 12 — Linux, phần 2: bộ nhớ, container, quyền và cách vào máy không cần SSH

> Tương ứng: [`docs/thiet-ke-he-thong-aws.md`](../docs/thiet-ke-he-thong-aws.md)
> Phần III, mục "Linux", nửa sau (dòng 788–972): swap, container nhìn từ
> Linux, quyền user, và không-SSH.
> Buổi 11 xử lý *khởi động* máy. Buổi này xử lý máy *chạy* — hết bộ nhớ thì
> sao, container cách ly bằng gì, ai được chạm vào gì, và khi cần vào máy
> chẩn đoán thì đi đường nào vì port 22 đã đóng hoàn toàn.

## Vì sao phải tự tạo swap

`t3.micro` chỉ có 1 GB RAM, mà phải chạy đồng thời:

| Tiến trình | RAM xấp xỉ |
|---|---|
| `ecs-agent` | ~100 MB |
| `nginx` (container web) | ~15 MB |
| .NET API (container api) | ~250 MB |
| Hệ điều hành + `docker` daemon | ~200 MB |

Cộng lại đã chật, và lúc deploy còn có task `migrator` chạy chồng lên trong
một khoảng ngắn. Khi Linux hết RAM, **OOM killer** của kernel chọn một tiến
trình và `SIGKILL` nó — trong container biểu hiện là **exit code 137**, task
chết không rõ lý do (không phải bug trong code, chỉ là hết chỗ).

Nên `user_data` tự tạo 2 GB **swap** — một file trên đĩa được kernel dùng
như RAM mở rộng, chậm hơn nhiều nhưng còn hơn bị giết:

```bash
dd if=/dev/zero of=/swapfile bs=1M count=2048   # cấp phát file 2GB toàn số 0
chmod 600 /swapfile                             # CHỈ root đọc được
mkswap /swapfile                                # định dạng thành vùng swap
swapon /swapfile                                # bật ngay
echo '/swapfile none swap sw 0 0' >> /etc/fstab # bật lại sau reboot
sysctl -w vm.swappiness=60                       # mức độ chịu đẩy ra swap
```

Ba chi tiết đáng nhớ:

- **`chmod 600` là bắt buộc, không phải cho gọn.** Swap chứa nguyên xi nội
  dung RAM — gồm mật khẩu database và JWT secret ở dạng chưa mã hoá. Để mặc
  định `644` là bất kỳ user nào trên máy cũng đọc được bí mật đó từ đĩa.
- **`/etc/fstab` là bắt buộc để swap sống qua reboot.** `swapon` chỉ có tác
  dụng cho phiên hiện tại; không ghi vào `fstab` thì reboot xong là mất
  swap, quay lại nguy cơ OOM.
- **`vm.swappiness` giữ nguyên mặc định 60**, không hạ xuống — hạ xuống làm
  kernel *ngại* dùng swap hơn, tức OOM killer ra tay sớm hơn, đúng thứ ta
  đang cố tránh.

Và vì swap phải sẵn sàng **trước khi** ECS agent lên (agent cũng ăn RAM),
thứ tự trong `user_data` là: tạo swap trước, ghi `ecs.config` sau. Có một
test tự động khoá thứ tự này lại, cùng nhóm với test chống deadmlock ở buổi
11.

## Container, nhìn từ phía Linux

Container **không phải máy ảo**. Khác biệt gốc rễ ở **kernel**: máy ảo có
kernel riêng (ảo hoá cả phần cứng, phải boot một hệ điều hành đầy đủ);
container **dùng chung kernel của máy host** — không boot gì cả. Đó là lý
do container khởi động trong mili-giây còn máy ảo mất hàng chục giây.

Nó chỉ là một tiến trình Linux bình thường, bị kernel giới hạn tầm nhìn
bằng hai tính năng có sẵn của kernel (không phải công nghệ riêng của
Docker):

- **namespace** — quyết định tiến trình *thấy* được gì: `/` riêng, danh
  sách tiến trình riêng (`ps` trong container chỉ thấy chính nó), network
  riêng. Nhờ network namespace, `web` nghe port 80 và `api` nghe port 8080
  **trong không gian mạng riêng của chính nó** — về lý, hai container khác
  nhau nghe cùng port bên trong namespace của chúng không đụng nhau.

  Nhưng HushStore dùng `bridge` mode với **host port cố định** (nhắc lại từ
  buổi 10) — nghĩa là port còn bị *map ra máy thật*, và ở đó chỉ một tiến
  trình giữ được port 80 tại một thời điểm. Đây chính là chốt chặn scale
  ngang được nhắc ở buổi 10 và sẽ nhắc lại ở buổi 15 (Phần V): namespace lo
  được cách ly *bên trong*, nhưng không gỡ được giới hạn *bên ngoài* do
  chọn port tĩnh.

- **cgroup** (control group) — quyết định tiến trình *dùng* được bao
  nhiêu: RAM, CPU, I/O. Khi task definition ghi `memory = 512`, ECS đang
  đặt một giới hạn cgroup; container vượt quá là kernel `SIGKILL` nó (một
  nguồn exit-137 khác, độc lập với OOM ở tầng máy).

Vì cgroup là của **kernel trên máy do ta quản** (EC2, không phải Fargate),
ta mới đặt được các tham số dưới đây trong task definition:

```hcl
linuxParameters = {
  initProcessEnabled = true
  maxSwap            = 1024
  swappiness         = 60
}
```

`maxSwap` và `swappiness` ở cấp container **chỉ tồn tại với EC2 launch
type** — Fargate không có, vì ở Fargate kernel không thuộc về ta để đặt.
Đây là lợi ích cụ thể, đo được của việc đề bài bắt dùng EC2 thật, không chỉ
là một ràng buộc phải chịu.

`initProcessEnabled = true` chèn một tiến trình `init` nhỏ làm PID 1 trong
container. Trên Linux, PID 1 có nghĩa vụ đặc biệt: **thu dọn tiến trình con
đã chết** (zombie process). Ứng dụng `dotnet` không tự làm việc đó, nên
thiếu init thì mỗi phiên ECS Exec (mở shell vào container để debug) để lại
một zombie không ai dọn.

## Người dùng và quyền — container không chạy bằng root

Mặc định một container chạy bằng `root`. Nếu ai đó thoát được ra khỏi
container (container escape — một lỗ hổng đã biết trong lịch sử Docker),
họ sẽ là `root` trên chính máy host. Nên `Dockerfile` của API hạ quyền
xuống trước khi chạy:

```dockerfile
RUN useradd -m appuser && chown -R appuser /app
USER appuser
```

**Thứ tự trong file quan trọng, và có lý do kỹ thuật cụ thể:** phần cài **CA
certificate của RDS** phải nằm **trước** dòng `USER appuser`, vì lệnh
`update-ca-certificates` ghi vào `/etc/ssl/certs` — chỉ `root` mới có quyền
ghi ở đó. Đảo thứ tự (hạ quyền trước, cài cert sau) thì build sẽ báo lỗi
permission denied ngay tại bước build, không phải lúc chạy.

### Trust store và VerifyFull

Connection string dùng `SSL Mode=VerifyFull` — nghĩa là API *thật sự kiểm
tra* chứng chỉ của RDS đúng là do Amazon phát hành, thay vì tin bừa bất kỳ
ai xưng "tôi là RDS". Muốn kiểm được thì container phải có sẵn CA của Amazon
RDS trong trust store của nó, nên `Dockerfile` tải cert đó về rồi nạp vào hệ
thống:

```dockerfile
ADD --checksum=sha256:3c69...e979 https://truststore.pki.rds.amazonaws.com/...
RUN update-ca-certificates
```

`--checksum` không phải chi tiết vụn: **không ghim checksum, ai kiểm soát
được đường tải (DNS bị đầu độc, MITM giữa đường) là chèn thêm được một CA
giả vào trust store** — và từ đó giả mạo được RDS mà API vẫn tin là hợp lệ.
Ghim checksum biến bước tải thành một khẳng định "đúng đúng file này, không
file nào khác", độc lập với việc đường tải có bị chặn giữa đường hay không.

## Không có SSH — và vẫn vào được máy để chẩn đoán

Vì đề bài chấm nguyên tắc tối thiểu, **port 22 đóng hoàn toàn**: không SG
rule nào mở nó, không key pair `.pem` nào tồn tại, và `nacl-app` (buổi 8)
còn có rule DENY 22 tường minh làm lớp thứ hai.

Nhưng vẫn cần vào máy để chẩn đoán khi có sự cố (như deadlock ở buổi 11).
Đường vào là **SSM Session Manager**: trên Amazon Linux 2023 có sẵn daemon
`amazon-ssm-agent`, nó **tự gọi ra ngoài** AWS (outbound, qua NAT) và giữ
một kênh mở. Người vận hành bấm "connect" từ *phía AWS* — không có ai gọi
*vào* máy từ ngoài cả. Đây là khác biệt bản chất, không phải hình thức:

| | SSH | SSM Session Manager |
|---|---|---|
| Chiều kết nối | từ ngoài **vào** | từ máy **ra** |
| Cần port mở | 22 | **không** |
| Cần credential trên máy | khoá riêng `.pem` | **không** — dùng IAM |
| Thu hồi quyền | phải xoá `authorized_keys` trên từng máy | sửa IAM policy, có hiệu lực ngay |
| Nhật ký | phải tự cấu hình `sshd`, log nằm **trên chính máy** bị chiếm | hiện trong CloudTrail Event History (mặc định, miễn phí, giữ 90 ngày), **ở ngoài máy**, không sửa được từ trong |

> **Giới hạn đã biết:** dự án **chưa tạo CloudTrail trail** riêng, nên chỉ
> có Event History mặc định — 90 ngày, chỉ management event, không đẩy ra
> S3, không query bằng Athena. Đủ để trả lời "ai đã vào máy lúc nào", không
> đủ làm bằng chứng lưu trữ lâu dài. Nhắc lại chi tiết này ở buổi 15 (Phần
> V — giới hạn đã biết).

## Vài chi tiết Linux khác đã gặp thật

- **`set -euxo pipefail`** ở đầu `user_data` — bốn cờ bash: `e` dừng ngay
  khi một lệnh lỗi, `u` báo lỗi nếu dùng biến chưa gán, `x` in mọi lệnh ra
  log (đọc ở `/var/log/cloud-init-output.log` — nơi đầu tiên phải xem khi
  máy boot sai), `o pipefail` để lỗi giữa một pipeline không bị lệnh cuối
  che mất. Thiếu các cờ này, một lệnh gãy giữa `user_data` sẽ trôi qua im
  lặng và máy trông như boot thành công.
- **Đồng hồ hệ thống vẫn đúng dù NTP (port 123) bị egress của `sg-web`
  chặn** — nhờ Amazon Time Sync ở địa chỉ *link-local* `169.254.169.123`
  (dải `169.254.0.0/16`, loại IP thứ ba bên cạnh private/public: chỉ có
  nghĩa trên đúng một đường mạng, không định tuyến đi đâu, nên kernel gửi
  thẳng ra card mạng — không qua route table, không qua NAT, không cần SG
  rule nào, và **không xuất hiện trong Flow Logs**). Các bản ghi REJECT
  port 123 vì thế không phải sự cố. Chứng minh gián tiếp đồng hồ đúng: (1)
  `migrator` xác thực cert RDS bằng `VerifyFull` — lệch giờ thì cert bị coi
  là chưa hiệu lực và task exit khác 0; (2) ECS agent pull image từ ECR
  qua request ký SigV4 kèm dấu thời gian, bị từ chối nếu lệch quá ~15 phút.
- **`gzip_static on`** trong nginx thay vì nén lúc chạy: `dotnet publish`
  đã sinh sẵn file `.gz` cạnh mỗi asset, nginx chỉ gửi file có sẵn — đổi CPU
  lấy đĩa, đúng hướng trên máy 2 vCPU burstable. File `.br` (Brotli) cũng có
  sẵn nhưng `nginx:alpine` không biên dịch kèm module đọc nó, nên chỉ nằm
  chiếm chỗ vô ích.

## Bộ lệnh chẩn đoán tối thiểu

Vào máy bằng `aws ssm start-session --target <instance-id>`, rồi:

| Cần biết | Lệnh |
|---|---|
| cloud-init xong chưa, lỗi gì | `cloud-init status`, `cat /var/log/cloud-init-output.log` |
| ECS agent sống không | `systemctl status ecs`, `journalctl -u ecs -n 50` |
| Container nào đang chạy | `docker ps` |
| RAM và swap còn bao nhiêu | `free -h` |
| Có ai bị OOM killer giết không | `dmesg -T \| grep -i "killed process"` |
| Đĩa còn chỗ không | `df -h` |
| Máy đang nghe port nào | `ss -tlnp` |
| Cấu hình ECS đã ghi đúng chưa | `cat /etc/ecs/ecs.config` |

`ss -tlnp` là lệnh đáng chạy nhất khi bảo vệ đồ án: liệt kê **mọi port máy
đang nghe**, và danh sách đó phải không có `22`.

## Bài tập đọc code

1. Tìm `user_data` trong Launch Template
   ([`infra/tf/modules/ecs/`](../infra/tf/modules/ecs/)) — xác nhận thứ tự
   thật là swap trước, `ecs.config` sau.
2. Tìm `Dockerfile` của API — xác nhận `USER appuser` nằm **sau** đoạn cài
   CA certificate, và dòng `ADD` có `--checksum=`.
3. Tìm task definition của API trong cùng module — xác nhận có khối
   `linuxParameters` với `initProcessEnabled`, `maxSwap`, `swappiness`.

## Câu hỏi tự kiểm tra

1. Vì sao `chmod 600` trên file swap là vấn đề bảo mật, không chỉ vệ sinh
   hệ thống?
2. `maxSwap`/`swappiness` ở cấp container vì sao chỉ tồn tại trên EC2 launch
   type mà không có ở Fargate?
3. Đảo thứ tự `USER appuser` lên trước đoạn cài CA certificate trong
   Dockerfile thì hỏng ở bước nào — build hay lúc chạy?
4. SSM Session Manager an toàn hơn SSH về bản chất ở điểm nào, không phải
   chỉ "không cần mở port"?

<details>
<summary>Gợi ý đáp án</summary>

1. Vì swap là bản sao của nội dung RAM ghi ra đĩa, và RAM đang chứa mật
   khẩu database, JWT secret ở dạng chưa mã hoá. Quyền `644` (mặc định) cho
   phép mọi user trên máy đọc được file đó, tức đọc được bí mật dù không
   có quyền truy cập trực tiếp vào tiến trình đang giữ chúng trong RAM.
2. Vì hai tham số đó là cấu hình cgroup ở cấp kernel của máy host — chỉ có
   ý nghĩa khi ta thực sự sở hữu và quản kernel đó. Ở Fargate, AWS quản
   kernel bên dưới, người dùng không có quyền (và không cần) chỉnh cgroup
   trực tiếp.
3. Hỏng ngay lúc **build** (`docker build` báo lỗi permission denied ở bước
   `update-ca-certificates`), không phải lúc chạy — vì `USER appuser` đổi
   quyền của mọi lệnh `RUN` phía sau nó trong Dockerfile, và ghi vào
   `/etc/ssl/certs` cần quyền root.
4. Vì chiều kết nối đảo ngược: SSH cần ai đó *gọi vào* máy (đòi mở port,
   đòi máy có địa chỉ/route tới được từ ngoài), còn SSM Session Manager để
   *máy tự gọi ra* AWS và giữ kênh chờ lệnh — máy không cần và không có gì
   để "gõ cửa từ ngoài" cả, nên bề mặt tấn công từ bên ngoài với port đó là
   con số không tuyệt đối, không phải "khó hơn" mà là "không tồn tại đường
   đó".

</details>

Buổi tiếp theo: [13-rds-database.md](13-rds-database.md).
