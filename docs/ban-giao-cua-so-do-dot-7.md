# Bàn giao — cửa sổ đo AWS của đợt 7

> **Viết cho một phiên không có ngữ cảnh gì.** Đọc file này trước, rồi mới mở
> `docs/bat-dau-phien-moi.md` nếu cần nền rộng hơn.
>
> **Cập nhật:** 2026-09-23 · **Nhánh:** `main`, cây làm việc sạch, CI xanh cả 3 job.

---

## 🚨 BƯỚC 0 — LÀM TRƯỚC KHI ĐỌC TIẾP

**RDS nhiều khả năng đang chạy và đang tính tiền.**

Lần đo cuối ghi trong `CLAUDE.md`: **2026-09-06, RDS `stopped`**. Hôm nay là
**2026-09-23** — **17 ngày**. Mà AWS **tự khởi động lại** một RDS `stopped` sau
**7 ngày**. Tức nó có thể đã tự bật từ khoảng **2026-09-13** và chạy ~10 ngày ở
`HS_RATE_RDS_UP = 0.098/giờ` ≈ **$2,35/ngày**.

Chưa ai xác minh. Kiểm ngay:

```bash
bash infra/tf/scripts/status.sh
# hoặc nhanh hơn:
aws rds describe-db-instances --profile hushstore \
  --query 'DBInstances[].[DBInstanceIdentifier,DBInstanceStatus]' --output table
```

Nếu nó `available` mà bạn chưa định đo → `bash infra/tf/scripts/down.sh` ngay,
rồi mới quay lại kế hoạch bên dưới.

⚠️ Đây là **suy luận từ ngày tháng, chưa phải phép đo**. Có thể ai đó đã stop lại
rồi. Nhưng chi phí của việc kiểm là 1 lệnh, còn chi phí của việc bỏ qua là tiền thật.

---

## 1. Việc này là gì, và vì sao nó đắt nhất

Đợt 7 (2026-09-05) đổi database **SQL Server 2025 → PostgreSQL 17**. Tầng code và
tầng hạ tầng **đều đã xong và đã apply lên AWS**. Thứ còn thiếu **không phải
apply** — mà là **cửa sổ đo**: chạy lại các phép đo trên cấu hình mới.

Món nợ chính là **`docs/security-validation-report.md`** — đây là **"Đầu ra số 2"
của đề bài 513**: *chứng minh các rule được mở theo nguyên tắc tối thiểu và đã
thực sự ngăn được tấn công*. Báo cáo hiện tại đo ngày **2026-08-24**, trên
**SQL Server, cổng 1433**, hạ tầng **1 NAT, không Multi-AZ**.

Hôm nay hạ tầng là **PostgreSQL 17, cổng 5432, 2 NAT, RDS Multi-AZ**. Nên báo cáo
đang mô tả một hệ thống **không còn tồn tại**.

> 🕰️ **KHÔNG được sửa số trong báo cáo cũ.** Nó là **bản ghi của một phép đo**.
> Sửa số trong một bản ghi là làm giả bằng chứng — và chính báo cáo đó đã tự viết
> luật này ở đầu file. Cách đúng: **đo lại và ghi bản mới**, giữ bản cũ để đối chiếu.

---

## 2. Trạng thái đã biết (đo, không đoán)

| Thứ | Giá trị | Nguồn |
|---|---|---|
| Account | `551897327153`, IAM user `hushstore-ops` (**không** phải SSO) | `security-validation-report.md` §5 |
| Region | `ap-southeast-1` | |
| RDS | `hushstore-db-tf`, `db.t4g.micro`, PostgreSQL 17, Multi-AZ = **true** | đo 2026-09-06 |
| Trạng thái AWS lần cuối | RDS `stopped` · 0 ALB · 0 NAT · 0 EC2 | 2026-09-06 — **đã cũ 17 ngày** |
| `terraform test` | **109/109** trên 8 module | đo 2026-09-23 |
| LoadProbe | 9/9 ở cả 1 và 2 instance, **đo ở local**, PostgreSQL | `evidence/2026-09-05-postgresql-2-instance.md` |
| Bằng chứng bảo mật cũ | `docs/evidence/acc-551897327153/kb01…kb13.txt` | 2026-08-24 |

**Terraform:** bản CI dùng là **1.15.8**. Trên máy này `/usr/local/bin/terraform`
là **symlink hỏng** trỏ vào home của người khác — cài lại trước khi làm gì.

---

## 3. Năm việc phải đo

### 3.1 🔴 Chạy lại 12 kịch bản bảo mật trên cổng 5432 — *đắt nhất, quan trọng nhất*

Không phải cả 12 đều bị ảnh hưởng như nhau. Phân loại theo mức độ:

| Kịch bản | Ảnh hưởng của việc đổi engine | Ưu tiên |
|---|---|---|
| **3** — kết nối thẳng vào RDS | **Trực tiếp.** Cổng đổi 1433 → 5432; IP riêng của RDS đã khác (RDS dựng lại) | 🔴 bắt buộc |
| **9** — VPC Flow Logs `REJECT` | **Trực tiếp.** Bản ghi cũ đếm port `1433`; lần này phải thấy `5432` | 🔴 bắt buộc |
| **2** — kết nối IP riêng EC2 | IP nội bộ đổi | 🟠 nên |
| **5** — SSH mọi hướng | Không đổi về bản chất, nhưng rẻ và là chốt hồi quy | 🟠 nên (rẻ) |
| **1, 4, 6, 7, 8** | Không phụ thuộc engine DB | 🟡 chạy cho đủ bộ |
| **10, 11, 12** — IAM / OIDC | **Không cần hạ tầng chạy.** Thuần `aws iam simulate-principal-policy` + `sts` | 🟢 chạy được ngay, $0 |

> 💡 **Làm 10/11/12 trước.** Chúng không cần bật gì cả, không tốn đồng nào, và
> chiếm 3/12 kịch bản. Xong chúng thì cửa sổ tính tiền chỉ còn phải phủ 9 cái.

**Thêm hai kịch bản mới mà cấu hình mới mở ra** (báo cáo cũ không thể có):
- **Multi-AZ**: RDS standby có nằm ở AZ khác thật không, và nó có `publicly_accessible=false` không
- **NAT thứ hai**: mỗi AZ đi ra bằng NAT của chính nó — `terraform test` đã chốt ở mã, nhưng chưa ai đo trên AWS thật

### 3.2 🔴 Seeder vào RDS qua `verify-full` với CA thật

Chuỗi kết nối dùng `SSL Mode=VerifyFull` + `Root Certificate=/usr/local/share/ca-certificates/rds-ap-southeast-1.crt`.
Đã chạy thật **ở local** với 2 ca đối chứng âm, **chưa** chạy vào RDS thật.

Cần: task `seeder` run-task → exit 0, rồi đếm lại
`AppRoles=3, Categories=18, Manufacturers=21, Products=49, ProductVariants=52`
(đây là số của lần seed 2026-08-24 — nếu khác thì phải giải thích vì sao).

**Ca đối chứng âm bắt buộc:** cố tình bỏ `Root Certificate` → phải **thất bại**.
Không có ca này thì không chứng minh được `verify-full` đang thật sự verify.

### 3.3 🟠 Failover Multi-AZ

`aws rds reboot-db-instance --force-failover`. Đo: thời gian đứt kết nối, và ứng
dụng có tự nối lại không. Đây là thứ **duy nhất** biện minh cho chi phí Multi-AZ
(storage tính tiền 2 AZ **kể cả khi RDS `stopped`**).

### 3.4 🟠 `ReplicaLag` + ca đối chứng cost guard khi có replica

🚨 **Đọc kỹ trước khi bật:** có read replica thì AWS **từ chối** stop primary ⇒
`down.sh` và cost guard **mất tác dụng**, mà RDS `stopped` còn tự bật lại sau 7 ngày.
`down.sh` đã tự huỷ replica trước khi stop và `status.sh` in một dòng đỏ khi thấy
replica — **nhưng cả hai chỉ chạy khi có người gõ**.

**Tuyệt đối không bật `enable_read_replica` rồi để qua đêm.** Bật → đo → tắt, trong
cùng một phiên, có người ngồi canh.

### 3.5 🟡 Đối chiếu `HS_RATE_RDS_UP` với hoá đơn thật

`infra/tf/scripts/lib.sh:89` — `HS_RATE_RDS_UP=0.098` là số **đo trên SQL Server**,
nay cố ý giữ làm **cận trên**. Sau cửa sổ đo sẽ có hoá đơn thật của
`db.t4g.micro` PostgreSQL → cập nhật, kèm comment nói rõ số đó lấy từ đâu.

Cùng lúc: `HS_RATE_VPCE=0.01` (dòng 73) là **giá niêm yết, chưa đối chiếu hoá đơn**.

---

## 4. Trình tự chạy — thứ tự là ràng buộc, không phải gợi ý

```bash
# ── 0. Kiểm tra trước khi bật bất cứ thứ gì tính tiền ──────────────
aws sts get-caller-identity --profile hushstore   # phải ra hushstore-ops
bash infra/tf/scripts/status.sh                   # RDS đang chạy hay stopped?

# ECR phải có image, nếu không ECS chỉ trả CannotPullContainerError:
aws ecr describe-images --profile hushstore --repository-name hushstore-api

# my_ip PHẢI khớp IP công khai HIỆN TẠI, nếu không kịch bản 8 "đạt" GIẢ:
curl -s https://checkip.amazonaws.com    # rồi cập nhật my_ip trong terraform.tfvars

# ── 1. Ba kịch bản IAM — không cần hạ tầng, $0 ─────────────────────
# kịch bản 10, 11, 12 — xem lệnh nguyên văn trong
# docs/evidence/acc-551897327153/kb10-*.txt, kb11-*.txt, kb12-*.txt

# ── 2. Bật SỚM flow logs, để nó bắt được luôn đợt nmap của kịch bản 1
# enable_flow_logs = true   (bản ghi hiện sau ~10 phút, gom mỗi 600s)

# ── 3. Pha 1 — KHÔNG có ALB, để chạy migration + seed ──────────────
bash infra/tf/scripts/up.sh --no-alb
# rồi run-task migrator (phải exit 0), run-task seeder (phải exit 0)

# ── 4. Pha 2 — bật ALB (cần cert ACM đã ISSUED) ────────────────────
bash infra/tf/scripts/up.sh

# ── 5. Chạy kịch bản 1→9. Kịch bản 8 chạy CUỐI CÙNG ────────────────
# vì enable_deny_demo = true chặn chính máy đang test.

# ── 6. TẮT, rồi XÁC MINH bằng mắt ──────────────────────────────────
bash infra/tf/scripts/down.sh
bash infra/tf/scripts/status.sh    # phải thấy: ALB=0 NAT=0 EC2=0 ASG=0 RDS=stopped
```

---

## 5. Bẫy đã gặp thật — đừng gặp lại

1. **ECR rỗng** → bật cả hạ tầng chỉ để nhận `CannotPullContainerError`. Kiểm trước.
2. **`my_ip` cũ** → kịch bản 8 cho **dương tính giả**: rule DENY chặn một IP không
   còn là của mình, và nó vẫn "đạt". IP nhà là IP động.
3. **Thời gian start RDS**: số cũ ~14 phút là của **SQL Server Express**. PostgreSQL
   được kỳ vọng nhanh hơn, nhưng **chưa ai đo** — đừng đặt timeout theo số cũ mà
   cũng đừng giả định nó nhanh.
4. **`KHÔNG KẾT LUẬN` ≠ `ĐẠT`.** Một kịch bản bị rate limiter chặn sẽ thoả mọi bất
   biến vì code cần đo chưa hề chạy. Đó là bằng chứng an toàn giả.
5. **Một ✅ không phải bằng chứng an toàn; một 🔴 **là** bằng chứng hỏng.** Luật này
   của repo áp cho mọi bảng trong `docs/evidence/`.
6. **`enable_ecr_endpoints`** (mới thêm) là công tắc **tính tiền** duy nhất **không**
   được `enable_nat`/`enable_alb`/`instance_count` che chắn. Bật thì $0,04/giờ chảy
   ngay cả sau `down.sh`. `status.sh` đã có dòng riêng cho nó.

---

## 6. Chi phí của cửa sổ đo

| Khoản | $/giờ |
|---|---|
| ALB | 0,0252 |
| NAT × 2 | 0,1180 |
| EC2 t3.micro | 0,0132 |
| RDS đang chạy | 0,0980 *(số của SQL Server — cận trên)* |
| **Tổng khi bật đủ** | **≈ 0,25** |
| Sàn khi đã `down.sh` | 0,008 *(storage 20GB × 2 AZ, tính cả khi stopped)* |

Một cửa sổ đo 4 tiếng ≈ **$1**. Rẻ. Thứ đắt là **quên tắt**.

---

## 7. Xong là như thế nào

- [ ] `docs/security-validation-report-2026-09-xx.md` — bản ghi **mới**, không sửa bản cũ
- [ ] `docs/evidence/acc-551897327153/` có bộ `kb*.txt` mới, **output thô, không viết tay số nào**
- [ ] Mọi chỗ ghi `1433` trong bản mới là `5432`
- [ ] Có ca đối chứng âm cho `verify-full` (bỏ CA → phải hỏng)
- [ ] `HS_RATE_RDS_UP` cập nhật theo hoá đơn thật, hoặc ghi rõ vì sao chưa
- [ ] `status.sh` xác nhận ALB=0 NAT=0 EC2=0 ASG=0 RDS=stopped
- [ ] `CLAUDE.md` + `docs/bat-dau-phien-moi.md` cập nhật trạng thái

---

## 8. Đọc theo thứ tự này

| # | File | Để biết |
|---|---|---|
| 1 | file này | việc cần làm |
| 2 | `docs/security-validation-report.md` | 12 kịch bản là gì, §5 "Cách tái lập" |
| 3 | `docs/evidence/acc-551897327153/kb*.txt` | **lệnh tấn công nguyên văn** — copy từ đây |
| 4 | `CLAUDE.md` | quy ước bắt buộc của repo |
| 5 | `docs/terraform-runbook.md` | chi tiết hạ tầng |
| 6 | `infra/tf/scripts/{up,down,status}.sh` | cách bật/tắt/xác minh |

---

## 9. Ba việc KHÁC, không thuộc gói này

Đừng gộp vào — chúng chưa được chọn:

- **Góp ý của thầy, mục 2 / 3 / 6**: đổi naming subnet `-a`→`-1a` · viết bản phản
  biện · WAF
- **Mục 🅹**: 40 lời gọi `GetFromJsonAsync` trong `src/Client/` → `ApiCall.SendAsync`
- **Đợt 5 bước 2–5**, **đợt 6**, **nửa hạ tầng đợt 4**
