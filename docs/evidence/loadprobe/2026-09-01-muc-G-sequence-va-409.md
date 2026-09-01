# Mục 🅶 đợt 3 phần 1 — SEQUENCE + ánh xạ 409: **S01 🔴 → ✅**, và một phát hiện quan trọng hơn

> **Hai việc của gói 2 phần code đều xong và đã đo.** Nhưng phép đo đủ 9 kịch bản sau khi sửa
> làm lộ ra một thứ quan trọng hơn cả bản vá: **S03 trước đây ĐẠT chỉ vì lỗi S01 đang làm chết
> 9/10 request.** Sửa S01 đã tháo mất một tấm lưới an toàn tình cờ. Đọc §3 trước khi đọc bảng
> tổng kết, nếu không sẽ đọc "4 đạt / 5 hỏng" thành hồi quy.
>
> **Và việc chạy `pre_migration_checks.sql` trên RDS thì KHÔNG hoàn thành được** — vì hai lý do,
> lý do thứ hai nghiêm trọng hơn lý do thứ nhất. Xem §5.

- **Ngày đo:** 2026-09-01 · API `http://localhost:5222` · DB local (container SQL Server)
- **Migration:** `20260901073353_AddDocumentCodeSequences` — 5 `CreateSequence`, không đổi bảng nào
- **Nguyên trạng DB đã trả về đủ sau khi đo** (Products=2, ProductSerials=0, Orders=0, ServiceTickets=0)

---

## 1. SEQUENCE — bản vá và vì sao nó là bản vá đúng

Thuật toán cũ là **check-then-act**: đọc mọi mã trong ngày → lấy max → +1. N request đồng thời
**đều tính ra cùng một giá trị** → unique index `IX_Orders_OrderCode` chặn → người thua nhận lỗi.

**Thứ gây ra race chính là yêu cầu "reset mỗi ngày"** — nó bắt buộc phải có câu `SELECT MAX` để
biết hôm nay đã tới đâu. Nên bản vá là **bỏ việc reset**, tức bỏ nguyên nhân, không vá triệu chứng:
sequence **toàn cục**, định dạng vẫn `PREFIX-yyyyMMdd-NNNNNN` để mã còn đọc được và còn sắp đúng
thời gian.

| Trước | Sau |
|---|---|
| `GetCodesByDatePrefixAsync` nạp toàn bộ mã trong ngày về RAM, lấy max, +1 | `SELECT NEXT VALUE FOR [Seq…]` — một round-trip, không khoá, không đọc dữ liệu cũ |
| 5 phương thức repository + 5 khai báo interface | **đã xoá hết** (`grep GetCodesByDatePrefixAsync` → `0`) |

**Vì sao KHÔNG chọn "bắt unique-violation rồi retry" làm phương án chính:** với bộ cấp phát
`SELECT MAX`, mỗi vòng retry chỉ cho **đúng một** người qua → cần O(N) vòng, mỗi vòng một
round-trip. Ở 50 request đồng thời đó là hàng nghìn round-trip. Retry là công cụ cho va chạm
**hiếm**, không phải va chạm **chắc chắn**.

**Đánh đổi phải nói với người quyết định nghiệp vụ:** mã không còn là "chứng từ thứ N trong ngày",
và dãy mã **sẽ có lỗ** (`NEXT VALUE FOR` không mang tính giao dịch — giá trị bị tiêu thụ dù
transaction rollback). Nhưng đọc số trong mã để suy ra số lượng **vốn đã sai từ trước**: rollback
đã tạo lỗ hổng rồi. Muốn đếm thì `COUNT(*)`.

### Trần độ rộng cột — đã kiểm, không phải giả định

Cột mã là `nvarchar(20)` ở cả 5 bảng (đã kiểm bằng `INFORMATION_SCHEMA.COLUMNS`). Vì sequence
không reset, con số lớn dần mãi:

```
"ORD-yyyyMMdd-" (13 ký tự) + 6 chữ số = 19  ✓
tiền tố 3 ký tự (ORD/POS/SRV) + 7 chữ số = 20  ✓  (vừa khít)
tiền tố 3 ký tự              + 8 chữ số = 21  ✗  TRÀN
```

→ trần thực tế **9.999.999** chứng từ cho mỗi sequence tiền tố 3 ký tự (99.999.999 cho PN/KK/ST).
Việc so sánh **chuỗi** đã bị bỏ hẳn nên số rộng thêm **không** gây sai như quả bom `{n:D3}` cũ —
độ rộng cột là giới hạn thật duy nhất còn lại.

### Migration đảo được — đã kiểm cả hai chiều

```
dotnet ef database update UpdateFullNameMaxLength   → DROP SEQUENCE ×5 · sys.sequences = 0
dotnet ef database update                           → CREATE SEQUENCE ×5 · sys.sequences = 5
```

Không đổi bảng nào, nên rollback là thao tác sạch — khác hẳn phần 2 của đợt 3 (`RowVersion` chạm
mọi đường `SaveChanges`). Đây đúng là lý do runbook tách đợt 3 làm hai gói.

---

## 2. S01 — bằng chứng chính

```
── S01: 50 khách checkout đồng thời
   ✅ ĐẠT — 50 đơn, 50 mã phân biệt.
     · Đơn tạo được: 50/50
     · Mã đơn phân biệt: 50
     · Mã HTTP: 200×50
```

| | Trước | Sau |
|---|---|---|
| Đơn tạo được | **12/50** (lần chạy 2026-09-01), 18/50 (2026-08-31) | **50/50** |
| Mã HTTP | `200×12, 400×38` | **`200×50`** |
| Kết luận | 🔴 HỎNG | **✅ ĐẠT** |

Đây là tiêu chí XONG mà runbook đặt ra cho gói 2: *"S01 chuyển 🔴 → ✅"*.

---

## 3. 🚨 Đọc bảng 9 kịch bản cho đúng — S03 KHÔNG phải hồi quy của bản vá này

Chạy đủ 9 kịch bản, 1 instance, sau khi sửa: **4 đạt / 5 hỏng / 0 không kết luận.**
Lần chạy tương đương trước đó: **5 đạt / 4 hỏng.** Nhìn dòng tổng kết thì trông như đi lùi.
Không phải.

| # | Trước | Sau | Đọc thế nào |
|---|---|---|---|
| S01 | 🔴 | **✅** | **Đã sửa** bằng SEQUENCE |
| S02 | ✅ | ✅ | không đổi |
| S03 | ✅ | **🔴** | **Lộ ra, không phải mới hỏng** — xem dưới |
| S04 | ✅ | 🔴 | kịch bản **phụ thuộc thời điểm**, đã nằm trong hợp "đã từng sai" |
| S05 | ✅ | ✅ | không đổi |
| S06 | 🔴 | 🔴 | đã biết, việc của gói 3 |
| S07 | ✅ | ✅ | không đổi |
| S08 | 🔴 | 🔴 | đã biết, việc của gói 3 |
| S09 | 🔴 | 🔴 | đã biết, việc của gói 3 |

### Vì sao S03 là "lộ ra" chứ không phải "mới hỏng" — bằng chứng, không phải suy luận

So mã HTTP của **chính kịch bản S03** ở hai lần chạy:

| | Mã HTTP | `COUNT(VoucherUsages)` | Kết luận |
|---|---|---|---|
| Trước SEQUENCE | **`200×1, 400×9`** | 1 | ✅ ĐẠT |
| Sau SEQUENCE | **`200×10`** | **10** | 🔴 HỎNG |

S03 bắn 10 request checkout của **cùng một khách** với voucher `MaxUsesPerUser = 1`. Trước đây
**9/10 request chết** — và chúng chết ở **bước sinh mã đơn**, tức đúng lỗi S01. Chỉ 1 request
sống sót tới được logic voucher, nên bất biến "đúng 1 lượt" **đúng một cách tình cờ**: đoạn code
cần đo **chưa bao giờ chạy quá một lần**.

Sửa S01 xong, cả 10 request đi tới được logic voucher, và check-then-act ở đường
"số lượt mỗi người" hiện nguyên hình: **10 lượt cho một khách có hạn mức 1**.

🔴 **Lỗi này đã tồn tại từ trước, chỉ bị một lỗi khác che.** Runbook nói *"chỗ nào còn
check-then-act thì HỎNG, không có ngoại lệ"* — S03 luôn thuộc nhóm đó; nó chỉ chưa bao giờ được
đo thật.

### Điều đáng lo nhất: chốt `KHÔNG KẾT LUẬN` KHÔNG bắt được ca này

Bẫy #8 dạy: kịch bản bị rate limiter chặn sẽ thoả mọi bất biến vì code cần đo chưa chạy, nên
`KHÔNG KẾT LUẬN` phải được phân biệt khỏi `ĐẠT`. Bộ đo đã có chốt đó, và nó báo
**`0 KHÔNG KẾT LUẬN`** ở **cả hai** lần chạy.

Nhưng ở đây 9 request **thật sự đã chạy** và **thật sự trả về `400`** — một phản hồi HTTP hợp lệ,
không phải bị chặn. Bộ đo không có cách nào biết rằng `400` đó đến từ **một lỗi khác** chứ không
từ **bất biến đang đo**. Nên:

> **`0 KHÔNG KẾT LUẬN` không đủ để kết luận một `✅` là thật.** Một kịch bản còn có thể ĐẠT vì
> một lỗi *khác* đang loại bớt request trước khi chúng tới được đoạn code cần đo.

Đã ghi thành **bẫy #17** ở §5 của runbook.

---

## 4. Ánh xạ 409 — đã đo, kèm ca đối chứng âm

`ConflictExceptionHandler` (`src/API/Middleware/`) đăng ký bằng `AddExceptionHandler<T>()` và
chạy **trước** khối `UseExceptionHandler(500)`.

**Vì sao phải đo chứ không tin code:** thứ tự đăng ký là điều kiện sống còn — đăng ký sau thì
handler tổng đã ghi `500` và kết thúc response, file thành **code chết mà không có gì báo lỗi**.
Build sạch chứng minh **không** điều này.

Cắm 3 probe tạm vào một endpoint, đo, rồi `git checkout` để revert (đã kiểm `grep __PROBE` → `0`):

| Exception ném ra | HTTP | Thân phản hồi | |
|---|---|---|---|
| `DbUpdateConcurrencyException` | **409** | `"Dữ liệu vừa được người khác thay đổi. Vui lòng tải lại trang và thử lại."` | ✅ |
| `ConcurrentModificationException("Phiếu vừa đổi trạng thái…")` | **409** | `"Phiếu vừa đổi trạng thái, vui lòng tải lại trang."` — **giữ nguyên câu của service** | ✅ |
| `InvalidOperationException` (không phải xung đột) | **500** | rơi xuống handler tổng | ✅ |

Dòng thứ ba là **ca đối chứng âm** và nó quan trọng nhất: nó chứng minh handler **không** bắt bừa
mọi thứ. Không có nó thì hai dòng đầu chỉ chứng minh "cái gì cũng ra 409".

Dòng thứ hai chứng minh nguyên tắc thiết kế: **service call-site cung cấp thông báo theo ngữ
cảnh, handler chỉ lo status code.** Trên SQL Server không có cách nào khác —
`SqlException` **không có** thuộc tính tên constraint, và parse `ex.Message` để đoán constraint
là dùng biểu diễn chuỗi thay cho ngữ nghĩa (bẫy #7).

### ⚠️ Handler này HIỆN CHƯA với tới được từ đường nghiệp vụ nào — cố ý, nhưng phải biết

Đo race thật (6 request đồng thời nhập cùng một serial) → unique index chặn đúng, nhưng người
thua nhận **`400`**, không phải `409`:

```
201 (1 thắng) · 400 ×5 → "Đã xảy ra lỗi khi tạo phiếu nhập kho. Vui lòng thử lại."
```

Vì `ImportReceiptService` có `catch (Exception)` **nuốt** `DbUpdateException` và tự trả
`ApiResult.Fail` **trước khi** exception thoát ra tới middleware. Kiểm cả repo: **mọi** chỗ ném
`ConcurrentModificationException` đều bị bắt trong cùng service.

Đây **đúng như kế hoạch sắp xếp**, không phải sai sót: kế hoạch đặt việc *"mọi chỗ
`catch (Exception ex)` nuốt loại này phải `throw;` lại"* vào **đợt 3 phần 2** (gói 3), cùng với
`RowVersion` — vì `RowVersion` mới là thứ bắt đầu sinh ra `DbUpdateConcurrencyException` hàng loạt.
Đây là **cùng một khuôn "dựng bên nhận trước"** đã dùng ở đợt 2, khi ánh xạ `409` được thêm vào
`ApiCall.ToUserMessage` *trước* khi có endpoint nào trả về 409.

🎯 **Giá trị của việc đo hôm nay:** gói 3 sẽ gắn `RowVersion` vào và **biết chắc** dây nối 409 đã
sống, thay vì phát hiện handler là code chết sau khi đã đụng vào mọi đường `SaveChanges`.

---

## 5. 🔴 `pre_migration_checks.sql` trên RDS — KHÔNG chạy được, và lý do thứ hai mới là lý do thật

RDS đã bật thành công (`available` sau ~16 phút, hơi lâu hơn con số ~14 phút đã ghi) và **đã tắt
lại** ngay khi rõ là cửa sổ này không dùng được — theo đúng dấu hiệu dừng #2 của runbook
(*"phải bật/tắt AWS lần thứ hai nghĩa là kế hoạch cửa sổ đã sai — dừng, nghĩ lại"*).

### Lý do 1 — RDS không với tới được từ máy local, **có chủ đích**

| Kiểm | Kết quả |
|---|---|
| `PubliclyAccessible` | **`false`** |
| SG của RDS (`sg-0376a77e01eda88ff`) inbound 1433 | **chỉ từ `sg-0677ed47526fd60e9`** (`hushstore-web-sg`), **0 dải CIDR** |
| EC2 instance đang chạy | **0** |
| SSM managed node | **0** |
| NAT gateway | **0** |
| ECS container instance đã đăng ký | **0** |

Đây là **thành quả bảo mật có chủ đích**, có bằng chứng riêng
(`docs/evidence/acc-551897327153/kb03-rds-tu-internet.txt`). **Không mở CIDR cho IP máy local** —
làm vậy là tự tay tháo một deliverable đã nghiệm thu.

Đường đi đúng là dựng compute **trong VPC**. `hushstore-seeder` có sẵn `sqlcmd` và đã nối sẵn
`DB_PASSWORD` từ SSM, nhưng task definition đó là **`EC2` + `bridge`**, cần container instance mà
hiện có 0. Đăng ký một task definition Fargate tạm (dùng lại đúng image + execution role + secret)
**bị chặn bởi cổng phân quyền** — đây là việc tạo hạ tầng AWS, nên cần bạn cho phép.

### Lý do 2 — và đây mới là lý do quan trọng: **câu trả lời sẽ gần như vô nghĩa**

Tiền đề của mục 🅶 là: *"Chạy trên DB local là vô nghĩa: local gần như rỗng"*, hàm ý **RDS thì
có dữ liệu thật**. **Tiền đề đó sai.**

`docs/evidence/acc-551897327153/kb00-migration-va-seed.txt` ghi RDS này được dựng **mới hoàn toàn
ngày 2026-08-24** bằng `efbundle` + seeder, và nội dung sau seed là:

```
AppRoles = 3 · AppUsers = 1 · Categories = 18 · Manufacturers = 21
Products = 49 · ProductVariants = 52
```

**Không có** `Orders`, `ProductSerials`, `InventoryChecks`, `InventoryAdjustmentLogs`,
`VoucherUsages`, `ServiceTickets` — đây là bản dựng lại hạ tầng từ Terraform, **không phải một
database có lịch sử chạy thật**. Ba bảng mà script cần soi chỉ được sinh ra khi **chạy luồng
nghiệp vụ qua API**, và trên account này chỉ có 13 kịch bản kiểm **bảo mật/hạ tầng** (KB01–KB13)
từng chạy, không có luồng nghiệp vụ nào.

Đã kiểm nốt hai account còn lại: `408194747451` **không có RDS nào**; `667836586836` là account
cũ đã bỏ, token SSO hết hạn (và cũng là bản dựng từ Terraform). **Không account nào trong dự án
có database mang lịch sử production.**

### Hệ quả: blocker của đợt 3 không phải cái mà runbook đang ghi

| Runbook đang ghi | Thực tế |
|---|---|
| "Chưa chạy `pre_migration_checks.sql` trên RDS" | "**Không tồn tại** database nào có lịch sử production để chạy nó" |

Hai câu hỏi nghiệp vụ tách ra thành hai câu hỏi khác nhau, và **chỉ một câu trả lời được**:

| Câu hỏi | Trả lời được? |
|---|---|
| *"Migration thêm unique index có FAIL trên DB đích không?"* | ✅ **Được** — và câu trả lời gần chắc chắn là **không fail**, vì DB đích không có dữ liệu xung đột |
| *"Dữ liệu đã bị lỗi nhân bản `InventoryAdjustmentLogs` làm bẩn chưa?"* | ❌ **Không** — cần lịch sử production, thứ không tồn tại |

🚨 **Vì vậy: ghi "✅ rỗng ⇒ an toàn tạo unique index" vào tài liệu là chế tạo niềm tin giả.**
Kết quả rỗng ở đây nghĩa là *"chưa có luồng nghiệp vụ nào từng chạy trên DB này"*, **không** nghĩa
là *"lỗi chưa gây thiệt hại"*. Đây đúng họ với bẫy #8 và với bẫy #17 vừa phát hiện ở §3 — và là
lý do phép đo này bị dừng thay vì được chạy cho có số.

### Ba lựa chọn, cần bạn quyết

1. **Tuyên bố blocker vô hiệu** *(khuyến nghị)* — DB đích không có dữ liệu xung đột nên migration
   không thể fail; ghi thẳng vào runbook rằng câu hỏi thứ hai **không trả lời được trong phạm vi
   dự án này** và đi tiếp gói 3. **$0**, không cần AWS.
2. **Vẫn chạy script để có số cho báo cáo** — cần bạn cho phép đăng ký một ECS task definition
   tạm (Fargate, dùng lại image/role/secret của seeder), cộng một cửa sổ RDS nữa. Kết quả biết
   trước là rỗng; giá trị duy nhất là "đã chạy trên RDS thật".
3. **Đo thứ thực sự đáng đo, ở local, $0** — dùng LoadProbe tạo dữ liệu bẩn *thật*
   (S06 tái hiện được lỗi nhân bản `InventoryAdjustmentLogs` ngay lần chạy đầu), rồi thử áp
   migration thêm unique index lên **chính** dữ liệu bẩn đó. Cái này kiểm được điều mà cả hai
   lựa chọn trên không kiểm: **migration xử lý dữ liệu xung đột ra sao khi thật sự có xung đột.**

---

## 6. Chốt hồi quy đã chạy

```
dotnet build PBL3.sln --no-incremental       → 0 Error(s) / 184 cảnh báo (không thêm cảnh báo)
grep -rn 'GetCodesByDatePrefixAsync' src/    → 0   (đã xoá hết, không để lại)
bash devops/scripts/check-error-message-leaks.sh all  → Sạch [all]: 139 file, 0 chỗ
sys.sequences sau migration                  → 5
rollback rồi áp lại                          → 0 → 5, sạch cả hai chiều
```
