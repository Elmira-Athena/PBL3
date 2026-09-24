# Báo cáo kiểm thử bảo mật — HushStore trên AWS (cửa sổ đo đợt 7)

**Đề tài 513** — "Đầu ra" số 2: chứng minh các rule được mở theo nguyên tắc tối
thiểu và **đã thực sự ngăn được tấn công**, đo lại trên hạ tầng **PostgreSQL 17**
sau đợt 7.

> 🕰️ **Đây là BẢN GHI của một phép đo, không phải mô tả hệ thống hôm nay.** Mọi
> số, IP, cổng và commit ở đây là những gì đã đo trong **cửa sổ 2026-09-23 →
> 2026-09-24** và **không được sửa** — sửa số trong một bản ghi là làm giả bằng
> chứng. Bản ghi cũ (`security-validation-report.md`, đo 2026-08-24 trên SQL Server
> cổng 1433) được **giữ nguyên** để đối chiếu, KHÔNG sửa.

| | |
|---|---|
| Ngày đo | **2026-09-23** (kịch bản IAM 10/11/12, $0) + **2026-09-24** (kịch bản 1–9 cần hạ tầng) |
| Account | **`551897327153`**, IAM user `hushstore-ops` (không SSO) |
| Region | `ap-southeast-1` |
| Máy tấn công | Laptop macOS, IP công khai `113.185.109.227` |
| Công cụ | `nmap 7.991`, `curl`, `nc`, `python3 socket`, `aws iam simulate-principal-policy`, `aws sts assume-role-with-web-identity`, `aws logs filter-log-events` |
| Mục tiêu | ALB `hushstore-alb-718170503.ap-southeast-1.elb.amazonaws.com` (`56.10.32.28` / `18.143.55.202`) · EC2 `10.20.11.176` · RDS `10.20.21.49` |
| Database | **PostgreSQL 17.9**, cổng **5432**, `db.t4g.micro`, **Multi-AZ = true** |
| Hạ tầng | Terraform, commit `d050c35`. Cả 4 image ECR ở tag `d050c353…` |
| Output thô | [`docs/evidence/acc-551897327153/2026-09-23/`](evidence/acc-551897327153/2026-09-23/) — mọi số lấy từ đó, không viết tay |
| Bản ghi cũ (đối chiếu) | [`docs/evidence/acc-551897327153/`](evidence/acc-551897327153/) (2026-08-24, SQL Server) |
| Chi phí cửa sổ đo | **$0.1765** (ALB + 2×NAT + EC2 + RDS, ~32 phút) |

Kiểm thử thực hiện trên hạ tầng **do chính nhóm sở hữu**, trong phạm vi đề bài.

---

## 1. Vì sao có bản ghi này

Đợt 7 (2026-09-05) đổi database **SQL Server 2025 → PostgreSQL 17**, kéo theo cổng
DB `1433` → `5432`, thêm **NAT Gateway thứ hai** và bật **RDS Multi-AZ**. Bản ghi
2026-08-24 vì vậy mô tả một hệ thống **không còn tồn tại**. Cửa sổ đo này chạy lại
để chứng minh **hình dạng phòng thủ không đổi trên engine mới**, và đo hai tính
chất mà cấu hình cũ chưa có (Multi-AZ, 2 NAT).

**Kết luận: 12/12 kịch bản + 2 kịch bản mới ĐẠT.** Kết quả trùng khớp bản ghi cũ ở
mọi điểm bản chất; khác biệt duy nhất là những thứ đổi *có chủ ý* ở đợt 7 (cổng
5432 thay 1433, thêm role Technician trong seed).

---

## 2. Bảng kết quả 12 kịch bản

| # | Kịch bản | Kết quả 2026-09-24 | So với 2026-08-24 | Bằng chứng |
|---|---|---|---|---|
| 1 | Quét port ALB (nmap) | Chỉ `80`/`443` open, 998 port `filtered` | Giống | `kb01-nmap-alb.txt` |
| 2 | EC2 IP riêng từ Internet | 3 port (`8080`,`22`,`80`) drop im lặng (timeout) | Giống | `kb02-03-rds-ec2-tu-internet.txt` |
| 3 | RDS từ Internet | Endpoint phân giải ra IP riêng `10.20.21.49`, cổng **5432** timeout; `PubliclyAccessible=false` | Giống (đổi 1433→5432) | `kb02-03-…` |
| 4 | Cổng ứng dụng `:8080` qua ALB | `curl` timeout (exit 28) | Giống | `kb04-05-…` |
| 5 | SSH `:22` mọi hướng | `nc` timeout; **0 key pair**, launch template `KeyName=None`, 0 SG rule cổng 22, NACL rule 90 DENY 22, IMDSv2 `required` | Giống | `kb04-05-…`, `kb05-khong-co-ssh.txt` |
| 6 | Rate limit login | 5 request đầu `400`, sau đó `429`; quan sát cửa sổ 1 phút cuộn (reset đúng bản chất fixed-window) | Giống | `kb06-rate-limit-login.txt` |
| 7 | Allowlist Host header | Host lạ → `403`; chỉ `hushstore.io.vn` + `api.hushstore.io.vn` → `200` | Giống | `kb07-host-allowlist.txt` |
| 8 | NACL rule 50 DENY theo IP | Bật rule: đường trực tiếp từ IP của tôi TIMEOUT 25s, trong khi mạng của tôi vẫn `200` và ALB vẫn healthy; tắt rule: `200` lại | Giống | `kb08-nacl-deny-theo-ip.txt` |
| 9 | VPC Flow Logs REJECT | 2028 bản ghi REJECT từ IP tấn công; **cổng 5432 xuất hiện** và bị REJECT như mọi cổng khác | Giống (thêm 5432) | `kb09-flowlog-reject.txt` |
| 10 | Blast radius IAM (17 phép thử) | Trùng khớp hoàn toàn: hai tập secret giao rỗng, host `explicitDeny` 4 action đọc SSM, không role nào chạm RDS | Giống hệt | `kb10-blast-radius-iam.txt` |
| 11 | Giả mạo OIDC assume-role | Token bịa → `InvalidIdentityToken`; JWT tự ký khớp mọi claim → `Couldn't retrieve verification key` (AWS kiểm chữ ký GitHub thật); trust policy `StringEquals` ghim `refs/heads/main` | Giống | `kb11-gia-mao-oidc.txt` |
| 12 | Blast radius role deploy | 4 việc pipeline `allowed`; 8 việc nguy hiểm `implicitDeny` (không bật hạ tầng tốn tiền, không đọc tfstate/secret, không chạy task api, không pass seeder role); điều kiện `ecs:cluster` có tác dụng thật | Giống | `kb12-blast-radius-deploy-role.txt` |

---

## 3. Migration + seed lên RDS thật qua `verify-full` (có ca đối chứng âm)

Bằng chứng: `kb00-migration-seed-verify-full.txt`.

- **Migrator** (`hushstore-migrator:d050c353…`) → **exit 0**. Đây là **lần migrate
  đầu tiên** lên RDS PostgreSQL: log cho thấy EF đọc `__EFMigrationsHistory` (bảng
  chưa tồn tại) rồi tự tạo, áp `InitialCreatePostgres` + `AddRateLimitCounters`.
- **Seeder** (`verify-full` + CA thật) → **exit 0**. Đếm lại:
  `AppRoles=4, AppUsers=1, Categories=18, Manufacturers=21, Products=49, ProductVariants=52`.
  So với 2026-08-24: khớp 5/6. **`AppRoles=4` thay vì 3** vì role `Technician` (KTV)
  được thêm vào seed ở commit `b4bc7de` (đợt 7); commit cũ `2941d31` không có role này.
- **Ca đối chứng âm bắt buộc:** chạy lại seeder với `PGSSLROOTCERT` trỏ vào file
  **không tồn tại** (qua `containerOverrides`) → **exit 2**,
  `root certificate file "/khong-ton-tai/root.crt" does not exist`. Tức bỏ CA thì
  kết nối **gãy**, không tụt về chế độ không xác thực → `verify-full` đang **thật
  sự** verify cert.

**Thời gian start RDS PostgreSQL lần đầu: ~4–5 phút** (đo được; số cũ ~14 phút là
của SQL Server Express, không dùng cho PostgreSQL nữa).

---

## 4. Hai kịch bản mới mà cấu hình đợt 7 mở ra

Bằng chứng: `kb13b-multiaz-va-nat2.txt`.

- **Multi-AZ**: primary ở `ap-southeast-1b`, standby ở `ap-southeast-1a` (AZ khác
  thật), `PubliclyAccessible=false`, PostgreSQL 17.9 cổng 5432.
- **NAT thứ hai**: hai NAT Gateway, mỗi cái trong public subnet của AZ riêng
  (`1a`, `1b`). Route table xác nhận **mỗi app subnet đi ra bằng NAT của CHÍNH AZ
  mình** (không chéo AZ) — `terraform test` đã chốt ở mã, nay đo trên AWS thật.

---

## 5. Còn thiếu — cần đo sau (CHƯA làm trong cửa sổ này)

Hai việc dưới **cố ý bỏ qua** phiên này theo quyết định vận hành, cần đo ở một
phiên có người ngồi canh:

- [ ] **§3.3 Failover Multi-AZ** — `aws rds reboot-db-instance --force-failover`,
  đo cửa sổ đứt kết nối + ứng dụng có tự nối lại không. Đây là thứ **duy nhất**
  biện minh cho chi phí Multi-AZ. (Đã chuẩn bị sẵn bộ đo health-probe; chưa chạy.)
- [ ] **§3.4 `ReplicaLag` + ca đối chứng cost guard khi có replica** — cần
  `enable_read_replica = true`. 🚨 **RỦI RO CAO:** có replica thì AWS từ chối stop
  primary ⇒ `down.sh`/cost guard mất tác dụng, RDS tự bật lại sau 7 ngày. **Bật →
  đo → tắt trong CÙNG một phiên, có người canh.** Tuyệt đối không để qua đêm.
- [ ] **§3.5 Đối chiếu `HS_RATE_RDS_UP` với hóa đơn thật** — hiện `0.098` vẫn là
  số đo trên SQL Server, giữ làm cận trên. Chi phí RDS Multi-AZ thật đo gián tiếp
  ~$0.057/giờ (xem lib.sh). Cập nhật khi có hóa đơn thật của `db.t4g.micro` Postgres.

---

## 6. Trạng thái hạ tầng sau cửa sổ đo

Đã chạy `down.sh` + xác minh bằng AWS API: **ALB=0, NAT=0, EIP=0, EC2=0, Flow
Log=0, RDS=stopped**. Không còn resource nào tính theo giờ. Còn lại S3/ECR/snapshot/
ACM cert — tất cả $0 hoặc xấp xỉ.

⚠️ RDS `stopped` sẽ **tự bật lại sau 7 ngày** (AWS) — nếu không đo tiếp trước
~2026-10-01 thì chạy `down.sh` lại để stop.
