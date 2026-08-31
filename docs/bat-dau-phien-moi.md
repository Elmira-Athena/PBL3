# Bắt đầu phiên mới — đọc file này trước

**Cập nhật:** 2026-08-31 · **Trạng thái repo:** trên `main` (đã merge `feat/retry-safe-call-sites`,
fast-forward, CI xanh), build `0 Error(s)` / 184 cảnh báo
· **Đã xong:** đợt 1, mục 4.1, đợt 2, **mục A**, **mục B**, **mục C**, **mục D**,
**nợ kiểm thử 🧪 (cả ưu tiên 1 và nửa giao diện của ưu tiên 2)**, **mục 🅴**
· **Kế tiếp:** ① **mục 🅷** (cùng lỗi `ex.Message` của mục 🅴 còn ở 6 chỗ khác,
`PosService.cs:462` là bản sinh đôi y hệt) → ② đợt 3, **vẫn bị chặn** bởi RDS.

> 🧪 **Đừng tin dòng "XONG" nào ở dưới trước khi đọc mục 🧪.** Ranh giới đã dịch hai lần trong
> phiên 2026-08-31 (chiều): luồng **Checkout đã chạy thật tới DB**, và **cả 6 nút double-submit
> hỏng thật của mục B đã được đo — 6/6 khoá đúng, kèm ca đối chứng âm cho 3 dialog**
> ([bằng chứng](evidence/ui/2026-08-31-6-nut-double-submit.md)). Còn lại: **POS · xuất/nhập kho ·
> phiếu dịch vụ** chưa ai bấm tay hết luồng nghiệp vụ (nút thì đã đo, *luồng* thì chưa).

> ⚠️ **Đọc con số "5/9 bất biến SAI" cho đúng: đó là HỢP của mọi lần chạy, không phải ảnh
> chụp một lần.** Phiên này chạy đủ 9 kịch bản ở **cả hai** cấu hình và ra **4 HỎNG mỗi
> cấu hình** (S01/S06/S08/S09) — vì **S04 không tái hiện**, dù không một dòng
> `HasOpenTicketForSerialAsync` nào đổi. S04 và S07 **phụ thuộc thời điểm**: một lần ✅ không
> phải bằng chứng an toàn, còn một lần 🔴 **là** bằng chứng hỏng. Bất đối xứng này áp cho mọi
> bảng trong `docs/evidence/loadprobe/`.

Tài liệu này viết cho một phiên **không có ngữ cảnh gì cả**. Nó trả lời đúng ba câu:
*đang ở đâu*, *làm gì tiếp*, và *chạy/kiểm bằng lệnh nào*.

---

## 0. Đọc theo thứ tự này

| # | File | Đọc để biết |
|---|---|---|
| 1 | **file này** | việc kế tiếp + cách chạy + cách kiểm |
| 2 | [`CLAUDE.md`](../CLAUDE.md) | quy ước bắt buộc của repo (có 4 quy tắc sinh ra từ lỗi thật) |
| 3 | [`docs/nang-cap-dot-1-ket-qua.md`](nang-cap-dot-1-ket-qua.md) | *vì sao* mọi thứ thành ra như hiện tại + bằng chứng đã chạy |
| 4 | [`tools/LoadProbe/README.md`](../tools/LoadProbe/README.md) | cách đo tính đúng đắn dưới tải, và **bốn cách đo sai** mà bộ đo cố ý chặn |
| 5 | [`docs/evidence/loadprobe/`](evidence/loadprobe/) | số đo đã có: 9 kịch bản × 2 cấu hình + bằng chứng đa-instance |
| 6 | `/Users/ml/.claude/plans/hi-n-t-i-t-i-ang-memoized-quill.md` | kế hoạch gốc đầy đủ (đợt 3→8) |

**Không cần đọc lại toàn bộ diff của 10 commit.** Mọi quyết định khó đều đã được ghi thành
comment **ngay tại chỗ code**, và commit message ghi lý do.

---

## 1. Đang ở đâu

Đợt 1 (sửa lỗi đồng thời, transaction, sinh mã), mục 4.1 (`EnableRetryOnFailure`), đợt 2
(frontend + vá bảo mật), mục A (rà retry 18/18 call-site), mục B (23 nút double-submit) và
mục C (bộ đo `tools/LoadProbe/` + hạ tầng 2 replica) đã xong và đã kiểm chạy thật.

**Mục C vừa đổi bản chất của những việc còn lại.** Trước nó, danh sách lỗi đợt 3 là *suy
luận từ đọc code*. Nay có **số đo**: LoadProbe chạy 9 kịch bản, `0 KHÔNG KẾT LUẬN`, và
**5/9 bất biến SAI**. Chi tiết ở mục 🅵 bên dưới — đọc trước khi động vào đợt 3, vì nó xếp
lại thứ tự ưu tiên.

**Không còn thứ nào cố ý làm dở.** Việc duy nhất còn lại là **đợt 3**, và nó bị chặn bởi
một thứ ở ngoài repo (phải chạy script kiểm tra trên RDS) chứ không phải bởi lựa chọn.

Đã đóng: **18/18 call-site transaction retry-safe** (mục A) · **23/23 nút mutation dùng
`ActionButton`/`BusyScope`** (mục B) · **`tools/LoadProbe/` 9 kịch bản + `docker-compose`
2 replica + nginx round-robin** (mục C) · **10/10 lỗ hổng NuGet High + cổng chặn ở CI**
(mục D), tất cả xong 2026-08-31.

---

## 2. Việc kế tiếp, xếp theo thứ tự nên làm

### ✅ 🅰 Rà nốt call-site chưa retry-safe — **XONG (2026-08-31)**

18/18 call-site `ExecuteInTransactionAsync` nay đều `retrySafe: true`. Kiểm lại bất cứ lúc nào:

```bash
grep -rn "CHƯA RÀ RETRY" --include='*.cs' src/Service/          # kỳ vọng: rỗng
grep -rc "}, retrySafe: true)" --include='*.cs' src/Service/ | grep -v ':0'   # tổng 18
```

**Khuôn đã áp cho cả 14 chỗ** — hữu ích khi viết call-site transaction MỚI:

1. **Kiểm tra nghiệp vụ ở ngoài** bằng đọc **không tracking** (projection hoặc bản
   `AsNoTracking`), chỉ để trả lỗi đẹp mà không phải mở transaction.
2. **Nạp lại mọi entity sẽ ghi ở bên trong** delegate.
3. **Kiểm lại ở trong** làm chốt chống race — và **ném** chứ không `return`, vì `return` thì
   transaction vẫn commit.
4. Mọi giá trị **sinh một lần** (mã chứng từ, `DateTime.UtcNow` dùng để ghi) tính **bên trong**.

**Bốn bẫy đã gặp khi làm — đều là "chỉ đổi cờ thì hỏng":**

| Bẫy | Ở đâu | Hỏng thế nào |
|---|---|---|
| `+=` / `++` trên entity tracked | `InventoryCheckService.SubmitAsync`, `RejectAsync` | Cộng chồng thành `cũ + 2×missing`. EF **thấy** có thay đổi nên vẫn sinh `UPDATE` — với con số **sai**. Ghi sai số liệu âm thầm, tệ hơn mất dữ liệu vì kết quả trông vẫn hợp lệ. |
| Collection đã `Include` rồi `.Add` | `InventoryExportService.ExportOrderAsync` | `orderDetail.OrderSerials` giữ luôn bản ghi Add của lần thử trước → sinh `OrderSerial` **trùng**. |
| Entity `new` sẵn ở ngoài rồi `Add` ở trong | `OrderService` (`VoucherUsage`), `PosService` (`Order`, `OrderDetail`, `Warranty`) | Sau lần thử 1 chúng đã có Id; lần thử 2 `Add` lại là **no-op** hoặc ghi trùng. Đã sửa bằng cách trả **dữ liệu thuần** (`VoucherUsagePlan`, `itemPlan`) rồi mới `new` entity bên trong. |
| Danh sách tích luỹ khai ở ngoài | `PosService` (`newWarranties`) | Lần thử 2 `Add` chồng lên danh sách cũ → bảo hành nhân đôi. |

**Hai thay đổi kèm theo, cần biết khi đọc code:**

- **`ConcurrentModificationException`** (`src/Core/Exceptions/`) — kiểu riêng cho chốt chống
  race bên trong transaction. Cần nó vì các call-site trả `ApiResult` đều có
  `catch (Exception)` bọc ngoài sẽ nuốt mất thông báo cụ thể. Bắt
  `InvalidOperationException` thay thế thì **không an toàn**: EF Core và `IUnitOfWork` cũng ném
  đúng kiểu đó cho chuyện khác. Các call-site ở `ServiceTicketService` **không** dùng kiểu này
  vì chúng `catch { throw; }` — `InvalidOperationException` đã đi ra nguyên vẹn.
- **`ApplyVouchersAsync` nay trả `VoucherUsagePlan`** (record dữ liệu thuần) chứ không trả
  entity `VoucherUsage` dựng sẵn. Cả hai call-site tự `new` entity bên trong delegate.

**Đã kiểm chạy thật** (API local, 2026-08-31): tạo → gửi duyệt → phê duyệt → từ chối phiếu
kiểm kê đều 200, và **truy vấn thẳng DB xác nhận giá trị đã ghi** (`Status = 2` +
`ApprovedAt`, `Status = 0` + `RejectReason`) — đúng thứ mà chế độ hỏng "không sinh `UPDATE`"
sẽ làm sai. Dữ liệu test đã dọn, `InventoryChecks = 0` như cũ.

> ⚠️ Còn **chưa** kiểm chạy thật: luồng đặt hàng, POS, xuất kho và phiếu dịch vụ — DB local
> gần như rỗng (Products = 2, ProductSerials = 0, Orders = 0) nên không dựng nổi kịch bản.
> Bốn luồng này mới chỉ được rà bằng đọc code + build sạch.

---

### ✅ 🅱 Quét nốt chống double-submit — **XONG (2026-08-31)**

23/23 nút trong danh sách cũ + 1 nút khảo sát cũ bỏ sót nay dùng `ActionButton`; hai chỗ cần
khoá cả cụm thì bọc `BusyScope`. Tổng cộng 21 file, 25 tag `<MudButton>` đổi thành
`<ActionButton>` (24 nút mutation + nút "Hủy" của `WriteReviewDialog`, xem bên dưới).
`<MudButton>` trong `src/Client/Pages/`: 161 → 136; `<ActionButton>`: 21 → 46.

**⚠️ Đọc kỹ chỗ này — nhan đề cũ của mục B ("23 nút *chưa chống* double-submit") NÓI QUÁ.**
Đo thực tế cho thấy chỉ **6/23** nút thật sự không có bảo vệ nào; 17 nút còn lại đã có cờ
thủ công và **cờ đó chạy đúng**. Phân loại này quan trọng vì nó cho biết mục B đã *vá* được
bao nhiêu, chứ không phải chỉ *dọn* được bao nhiêu:

| Nhóm | Số nút | Trạng thái trước | Giá trị của việc chuyển |
|---|---|---|---|
| Không có cờ nào | **6** | bấm hai lần = hai request | **vá lỗi thật** |
| Có cờ + `StateHasChanged()` | 16 | đã chạy đúng | thống nhất cơ chế, bớt chỗ để sai |
| Có cờ, không cần `StateHasChanged()` | 1 | đã chạy đúng (cờ đặt trước mọi `await` nên `ComponentBase` tự vẽ lại) | như trên |

Sáu nút **thật sự** không có bảo vệ: `Orders/OrderDetail` (`ConfirmCompleteOrder`,
`ConfirmCancelOrder`) · `Storefront/MyOrderDetail` (`ConfirmCancelOrder`) ·
`ServiceTicketQuotation.SubmitQuotation` · `ServiceTicketIntake.SubmitIntake` ·
`Pos/Index.SaveDraft`.

**Vì sao 17 nút kia vẫn chạy đúng** — bẫy #5 ở §5 chỉ cắn khi cờ được đặt **sau** một `await`
**và** không có `StateHasChanged()` theo sau. Các dialog CRUD đều viết
`_isSaving = true; _errorMessage = null; StateHasChanged();` nên thoát bẫy. Đừng đọc bẫy #5
rồi suy ra "mọi cờ thủ công đều hỏng".

**Đã kiểm chạy thật** (API + Blazor client local, 2026-08-31) trên `SupplierDialog`, ba cấu
hình, cùng một kịch bản "bấm 2 lần trong một tick rồi bấm thêm lần thứ 3 lúc request đang bay",
đếm request bằng hook `window.fetch` và đối chiếu số bản ghi trong DB:

| Cấu hình | Nút khoá ngay sau click 1? | Số POST | Bản ghi tạo ra |
|---|---|---|---|
| `ActionButton` (bản mới) | **có** | 1 | **1** |
| Cờ thủ công (bản cũ) | có | 1 | 1 |
| Gỡ `Disabled` — ca đối chứng | **không** | **2** | **2 — trùng** |

Ca đối chứng có mặt ở đây là để chứng minh phép kiểm **không rỗng**: nếu thiếu nó thì kết quả
"1 POST" có thể chỉ nghĩa là kịch bản click không bao giờ chạm tới handler. Dữ liệu test đã
dọn, `Suppliers` về lại 6 như trước.

> ⚠️ Chưa kiểm chạy thật: Checkout, POS, xuất/nhập kho, phiếu dịch vụ — DB local gần như rỗng
> (Products = 2, ProductSerials = 0, Orders = 0) nên không dựng nổi kịch bản. Đúng giới hạn đã
> ghi ở mục A. Các nút này mới chỉ được rà bằng đọc code + build sạch. Trớ trêu là **5 trong 6
> nút hỏng thật lại nằm đúng nhóm không kiểm được** — muốn kiểm phải seed dữ liệu trước.

**Ba điều cần biết khi đọc diff:**

- **Cờ thủ công đã bị gỡ hẳn** (`_isSaving`, `_isSubmitting`, `_isApproving`, `_isConfirming`)
  cùng spinner viết tay — `ActionButton` tự lo cả hai. Chỗ nào `StateHasChanged()` còn phục vụ
  việc khác (xoá banner lỗi trước khi gọi API) thì **giữ nguyên**.
- **Bỏ cờ có thể làm vỡ `try`.** Ở `ImportReceiptPage.SaveReceipt`, khối `try` tồn tại *chỉ để*
  nhả cờ trong `finally`; gỡ cờ xong thì còn `try` không `catch`/`finally` → **không biên dịch
  được**. Đã bỏ luôn khối `try` và lùi thụt lề. Chỗ nào `try` có `catch` thật thì giữ.
- **`BusyScope` dùng ở 2 nơi mới:** `Orders/OrderDetail` (Duyệt/Hủy cùng hiện khi `Status == 0`
  — hai nút khác nhau, mỗi nút tự thấy mình rảnh) và `WriteReviewDialog` (nút "Hủy" trước đây
  bind `Disabled="_isSaving"`; muốn giữ đúng hành vi đó thì nó phải vào chung scope, nên nó
  cũng thành `ActionButton` — nó là tag thứ 25, không phải nút mutation thứ 25).

#### 🚨 `ActionButton` trên nút `ButtonType.Submit` là VÔ HIỆU — bản cũ của file này khuyên SAI

Bản trước của mục B viết: *"Nút trong `<MudForm>` có `ButtonType="ButtonType.Submit"` thì phải
giữ nguyên thuộc tính đó — `ActionButton` có truyền `ButtonType` qua."* Truyền qua thì đúng,
nhưng **cái khoá không hoạt động**, và đây là kiểu hỏng im lặng: build sạch, nút trông vẫn bình thường.

Cơ chế: khi handler nằm ở `<EditForm OnValidSubmit="HandleSubmit">`, cú click **submit form**,
nó **không** đi qua `OnClick` của nút. `ActionButton.HandleClickAsync` vẫn chạy — nhưng
`OnClick` của nó rỗng, nên `TryBegin()` rồi `End()` xong **tức thì**, trong khi `HandleSubmit`
mới bắt đầu chạy bất đồng bộ. Cờ bận đã nhả trước khi việc thật kịp bắt đầu.

**Đã đo trên `Admin/Customers/CustomerDialog`** (2026-08-31), cùng kịch bản ba cú click:

| Cấu hình | Nút khoá ngay? | Số POST |
|---|---|---|
| Bản gốc — `ButtonType.Submit` + cờ thủ công | **có** | **1** |
| Chuyển sang `ActionButton` theo lời khuyên cũ | **không** | **3** |

Chuyển hai file này sang `ActionButton` là **hồi quy**, không phải cải tiến. Vì vậy
`Admin/Customers/CustomerDialog` và `Admin/Employees/EmployeeDialog` **cố ý giữ cờ thủ công** —
đừng "dọn nốt" chúng. Cờ ở đó đặt **trước mọi `await`** nên chạy đúng.

Muốn dùng `ActionButton` cho nút submit thì phải **bỏ `ButtonType.Submit`** và chuyển handler
từ `EditForm.OnValidSubmit` sang `OnClick` của nút (tự gọi validate) — đó là việc sửa cấu trúc
form, không phải đổi tag.

`Admin/Products/ProductForm` **chuyển được** và đã chuyển: nút của nó nằm **ngoài** `EditForm`
và có `OnClick="HandleSubmit"` thật. Đây là nút mutation thứ 24 — khảo sát gốc bỏ sót cả ba file này vì
nó lọc theo `OnClick=`, mà hai file kia không có thuộc tính đó.

**Nút `MudIconButton`** (trong ô bảng) **không** dùng `ActionButton` được. Không có nút nào
thuộc diện này trong danh sách 23, nhưng nếu gặp về sau thì sửa cờ tại chỗ theo mẫu ở
`InventoryCheckDetailPage.HandleMarkDefective`: đặt cờ **trước mọi `await`**, gọi
`StateHasChanged()`, nhả trong `finally`.

**⚠️ Nhắc lại cho rõ:** cơ chế này **không bảo vệ server**. Hai tab, F5 giữa chừng, hay `curl` —
vẫn double-submit. Phòng tuyến thật là conditional update + unique index (đợt 1 đã làm cho
voucher và báo giá; đợt 3 làm nốt). Đừng đọc mục này rồi tưởng nhóm lỗi đồng thời đã xong.

### ✅ 🅲 Đợt 0 — bộ đo + hạ tầng đa instance — **XONG (2026-08-31)**

Ba thứ đã dựng, tất cả $0:

| Đã dựng | Ở đâu |
|---|---|
| `tools/LoadProbe/` — console app .NET, 9 kịch bản `IProbeScenario` | [`tools/LoadProbe/README.md`](../tools/LoadProbe/README.md) |
| `docker-compose` 2 replica API + nginx round-robin | [`devops/docker/docker-compose.multi.yml`](../devops/docker/docker-compose.multi.yml) |
| Bằng chứng đã chạy (một instance / hai instance / hạ tầng) | [`docs/evidence/loadprobe/`](evidence/loadprobe/) |

Chạy lại bất cứ lúc nào — công thức đầy đủ ở
[`docs/evidence/loadprobe/2026-08-31-ha-tang-2-replica.md`](evidence/loadprobe/2026-08-31-ha-tang-2-replica.md).

**Ba tính chất đa-instance đã chứng minh được:**

1. **Round-robin có thật** — 10 request qua nginx ra 2 giá trị `X-Upstream` phân biệt.
2. **Cả hai container sống sót lúc boot** — khối seed role `Technician` không còn giết task.
3. ⭐ **Khoá tài khoản không kẹt trong RAM một task** — khoá qua replica **A**, gọi ngay qua
   replica **B** → `403` + `X-Account-Status: locked`, không độ trễ. Đây là bằng chứng
   before/after thuyết phục nhất của cả báo cáo và nó tốn $0. *(Nửa "TRƯỚC" chưa đo — cần
   tạm khôi phục `MemoryCache`, việc của đợt 6.)*

**Cảnh báo RAM migrator: con số cũ tính bằng SAI đại lượng.** "api 512 + web 192 +
migrator 512 = 1216 MiB" là **`memory` (giới hạn cứng)**, nhưng ECS xếp task theo
**`memoryReservation`**, và `taskdef.tf` khai đủ cho cả bốn container: api **384**, web
**96**, migrator **256**, seeder **128**. Nhu cầu thật: **480 MiB** thường trực, **736 MiB**
lúc deploy, **864 MiB** nếu seeder chạy cùng. Đo thật ở local: API đỉnh **~198 MiB** dưới
tải. → **Việc "hạ migrator 512 → 256" không phải điều kiện cần để chạy 2 task.**
Còn nửa chưa xác minh: con số ~950–985 MiB khả dụng, cần
`aws ecs describe-container-instances --query 'containerInstances[0].remainingResources'`.

---

### 🅵 Kết quả đo — **5/9 bất biến ĐÃ TỪNG SAI**, đọc trước khi làm đợt 3

`0 KHÔNG KẾT LUẬN` ở **cả bốn** lần chạy, tức không có phép đo nào bị rate limiter làm rỗng.

Bốn cột = bốn lần chạy đủ 9 kịch bản. Hai cột đầu là mục C; hai cột sau là phiên
2026-08-31 (chiều), chạy lại để trả nợ 🧪 ưu tiên 1.

| # | Kịch bản | C: 1 inst | C: 2 inst | Nay: 1 inst | Nay: 2 inst |
|---|---|---|---|---|---|
| S01 | 50 khách checkout đồng thời | 🔴 | 🔴 | 🔴 | 🔴 |
| S02 | Voucher `Quantity=1`, 20 khách | ✅ | ✅ | ✅ | ✅ |
| S03 | Cùng khách, `MaxUsesPerUser=1` | ✅ | ✅ | ✅ | ✅ |
| S04 | 10 lần `intake` cùng serial | ✅ | **🔴** | ✅ | **✅** ⚠️ |
| S05 | 10 lần `accept-quotation` | ✅ | ✅ | ✅ | ✅ |
| S06 | 5 lần `approve` phiếu kiểm kê | 🔴 | 🔴 | 🔴 | 🔴 |
| S07 | POS bán S xen kẽ kiểm kê đánh S Lost | ✅ | ✅ | ✅ | ✅ |
| S08 | 2 lần `create-quotation` song song | 🔴 | 🔴 | 🔴 | 🔴 |
| S09 | 2 lần `refresh-token` cùng cặp | 🔴 | 🔴 | 🔴 | 🔴 |

> 🔴 **"5/9" là HỢP của bốn lần chạy, không phải kết quả của một lần.** Mỗi lần chạy riêng lẻ
> cho **4 HỎNG**; cái thứ năm là S04, chỉ hiện ở một trong bốn lần. Ai chạy một lần rồi thấy 4
> mà kết luận "đã sửa được một cái" là đọc sai — không dòng code liên quan nào đổi.
>
> Vì vậy **cách đọc đúng của cả bảng này là "đã từng sai", không phải "đang sai"**: với kịch bản
> phụ thuộc thời điểm (S04, S07), một lần 🔴 là bằng chứng hỏng, một lần ✅ **không** là bằng
> chứng an toàn. Không có chuyện một ô ✅ xoá được một ô 🔴 ở cùng hàng.

#### 🔴 S01 — sinh mã chứng từ đua nhau, 41/50 đơn KHÔNG đặt được

```
Cannot insert duplicate key row in object 'dbo.Orders'
with unique index 'IX_Orders_OrderCode'. The duplicate key value is (ORD-20260831-000011).
```

Đọc cho đúng: **dữ liệu KHÔNG hỏng** — unique index đã chặn. Hỏng là **tính khả dụng**:
41/50 khách nhận `400`. `IDocumentCodeGenerator` vẫn là "đọc max rồi +1", tức check-then-act,
không có retry khi đụng unique index. Kế hoạch gốc đã hẹn *"refactor trước, thay ruột ở đợt
3"* — **nay có bằng chứng cho cái hẹn đó**.

⚠️ **Kèm một lỗi riêng, nhỏ và độc lập:** thông báo trả cho người dùng là
`"Lỗi hệ thống khi đặt hàng: An error occurred while saving the entity changes..."` —
tiếng Anh, lộ nội tạng EF, **vi phạm quy tắc tiếng Việt của CLAUDE.md**. Sửa được ngay,
không cần chờ đợt 3, ở khối `catch` của `OrderService.CheckoutAsync`.

#### 🔴 S06 — sổ tổn thất nhân **5**

5 lần `approve` song song → **5 bản ghi `InventoryAdjustmentLogs`** cho cùng một
`(AuditCheckId, SerialId)`, cả 5 request đều `200`. Chốt `check.Status != AwaitingApproval`
là check-then-act: dưới READ COMMITTED cả 5 đều đọc thấy `1` trước khi ai kịp commit.

**Hệ quả cho đợt 3:** đây đúng là bảng mà kế hoạch định thêm unique index
`(AuditCheckId, SerialId)` — và kết quả này nói **rất có thể DB production đã có bản ghi
trùng**, tức phát sinh việc dọn dữ liệu nghiệp vụ. Câu `GROUP BY … HAVING COUNT(*) > 1`
trong `pre_migration_checks.sql` giờ là câu hỏi **đáng tiền nhất** trong cả script.

#### 🔴 S04 — chỉ vỡ khi có HAI instance, và **không vỡ mỗi lần**

Mục C: ✅ với 1 instance, 🔴 với 2 instance (2 phiếu dịch vụ chưa đóng cho cùng một serial).
Đúng loại lỗi mà toàn bộ hạ tầng 2 replica sinh ra để bắt: cửa sổ check-then-act của
`HasOpenTicketForSerialAsync` đủ hẹp để một tiến trình che được, nhưng hai tiến trình thì
không. **Đừng kết luận từ lần chạy một instance.**

⚠️ **Phiên 2026-08-31 (chiều): 2 instance ra ✅ — KHÔNG tái hiện.** `HasOpenTicketForSerialAsync`
không đổi một dòng nào giữa hai lần chạy (mục 🅴 chỉ sửa thông báo lỗi trong `OrderService`), nên
cái ✅ này **không** phải bằng chứng đã sửa — nó là bằng chứng S04 **phụ thuộc thời điểm**, cùng
loại với S07. Việc hai tiến trình có chen được vào đúng cửa sổ hẹp đó hay không là chuyện xác
suất, và round-robin của nginx đã được kiểm 5/5 sạch trước khi đo nên không thể quy cho hạ tầng.

**Vẫn phải sửa.** Xem [`2026-08-31-hai-instance-sau-muc-E.md`](evidence/loadprobe/2026-08-31-hai-instance-sau-muc-E.md).

#### 🔴 S08 — 2 báo giá cùng `Pending` trên một phiếu

Khách duyệt cái nào cũng được, cái còn lại treo `Pending` vĩnh viễn.

#### 🔴 S09 — rotation refresh token làm một client bị đăng xuất oan

2 lời gọi song song cùng một cặp → **cả hai đều `200`**, nhưng DB chỉ giữ được một hash.
Client kia cầm một refresh token **đã chết ngay lúc nhận**. Đây chính là cảnh báo trong
đợt 2 về việc `JwtAuthenticationStateProvider` và `AuthHeaderHandler` phải dùng **chung**
`TokenRefreshCoordinator` — single-flight phía client giấu được lỗi này ở đường thường,
nhưng không đóng được nó ở tầng server.

#### ✅ Bốn cái ĐẠT nói lên điều gì

S02/S03 (voucher) ĐẠT là **đợt 1 đã có tác dụng thật** — `ExecuteUpdateAsync` với vị từ
trong cùng câu lệnh đóng đúng khe check-then-act. S05 ĐẠT nhờ `TryDecideAsync`. Cùng một
lớp lỗi: chỗ nào đã chuyển sang conditional update thì ĐẠT, chỗ nào còn check-then-act thì
HỎNG. Đó là bản đồ cho đợt 3.

⚠️ **S07 ĐẠT nhưng tín hiệu yếu** — lần chạy này POS thua cuộc đua (nhận `400`), nên nhánh
nguy hiểm "serial đã bán bị ghi đè thành Lost" chưa hề được chạm tới. Kịch bản này phụ
thuộc thời điểm; muốn kết luận phải chạy lặp nhiều lần. **Đừng đọc nó thành "đã an toàn".**

---

### ✅ 🅳 Vá 3 gói NuGet mức High + cổng chặn ở CI — **XONG (2026-08-31)**

`dotnet list package --vulnerable --include-transitive` nay **sạch cho cả 7 project**.

| Gói | Trước | Sau | Cách |
|---|---|---|---|
| `System.Security.Cryptography.Xml` | 9.0.0 và 10.0.0 (8 advisory) | **10.0.10** | ghim transitive ở `Infrastructure` + `Service` |
| `Microsoft.OpenApi` | 2.4.1 (1 advisory) | **2.7.5** | ghim transitive ở `API` |
| `AutoMapper` | 16.0.0 (1 advisory) | **gỡ hẳn** | không một dòng code nào dùng |

#### 🚨 Bản cũ của file này ghi SAI lý do hoãn

Bản trước viết: *"nâng phiên bản có rủi ro hồi quy riêng (AutoMapper 16 → bản mới có breaking
change ở cấu hình profile)"*. Sai hai lần:

1. **Không cần nhảy major nào cả.** Mọi bản vá đều nằm **trong major hiện tại**: AutoMapper vá
   ở `16.1.1`, `Microsoft.OpenApi` vá ở `2.7.5` (không cần đụng nhánh 3.x). Advisory nói rõ
   ngưỡng vá; không ai phải nuốt breaking change nào.
2. **AutoMapper không hề được dùng.** Kiểm trên toàn repo: **0** `CreateMap`, **0** `IMapper`,
   **0** `AddAutoMapper`, **0** lớp `: Profile`. Ánh xạ DTO thật sự làm bằng **22 chỗ projection
   LINQ** thủ công. Nên việc đúng là **gỡ gói**, không phải nâng nó — đóng advisory vĩnh viễn với
   rủi ro bằng không. (Build sau khi gỡ: `0 Error(s)`, **184** warning — đúng bằng số trước khi
   gỡ, tức chẳng có gì từng phụ thuộc vào nó.)

Kèm theo: **`CLAUDE.md` đã sai ở ba chỗ** (dòng 50, 69, 106) khi bắt buộc dùng AutoMapper —
mô tả một cơ chế không tồn tại, và mâu thuẫn với luật *DTO Projection* của chính nó. Đã sửa,
kèm cảnh báo đừng thêm lại.

**Nguyên tắc chọn phiên bản đã dùng:** bản **nhỏ nhất đóng được hết** advisory của gói đó, không
phải bản mới nhất — repo không có test tự động nào để đỡ hồi quy. Cẩn thận: một gói có thể dính
nhiều advisory với **ngưỡng vá khác nhau**. `Cryptography.Xml` dính 8 cái, bốn vá ở `10.0.6` và
bốn vá tới `10.0.10`; ghim `10.0.6` sẽ **dọn sạch cảnh báo của bốn cái đầu và để lại bốn cái
kia** — trông như đã xong.

**Hai ghim transitive KHÔNG tương đương nhau về mức phơi nhiễm**, đọc comment tại chỗ trước khi
đụng: chuỗi ở `Service` đi qua **EPPlus** (chạy trong ảnh production, xuất Excel) — phơi nhiễm
thật; chuỗi ở `Infrastructure` đi qua `EntityFrameworkCore.Tools` khai `PrivateAssets=all` —
**công cụ lúc thiết kế, không deploy**. Ghim cái sau chỉ để cổng CI không đỏ vĩnh viễn vì một
thứ không chạy ở đâu cả.

#### 🔴 Bẫy thứ 11 — `dotnet list package --vulnerable` TRẢ VỀ 0 KHI CÓ LỖ HỔNG

Đây là lý do 10 lỗ hổng sống được lâu đến vậy, và là lý do cổng CI không viết thẳng lệnh đó.
Đo trên chính repo này lúc 10 advisory còn mở:

```bash
dotnet list package --vulnerable --include-transitive ; echo $?
# → in ra đủ 10 advisory High, rồi in ra:  0
```

Một bước CI dạng `run: dotnet list package --vulnerable` sẽ **luôn xanh, vĩnh viễn**. Cổng phải
**đọc nội dung báo cáo**, không được tin mã thoát.

`devops/scripts/check-vulnerable-packages.sh` làm đúng thế: đọc JSON, quét **cả**
`topLevelPackages` lẫn `transitivePackages` (2/3 gói dính lỗi nằm ở nhóm sau), chặn
High/Critical, chỉ in Low/Moderate. Nó **fail-closed** khi phép quét **rỗng** — restore hỏng,
JSON đổi schema, không project nào → thoát `2`, không thoát `0`. Cùng lý lẽ với hạng
`KHÔNG KẾT LUẬN` của LoadProbe.

**Đã kiểm cả bốn nhánh, có ca đối chứng** để chứng minh phép kiểm không rỗng:

| Ca | Kỳ vọng | Kết quả |
|---|---|---|
| repo sau khi vá | `0` | ✅ sạch, 7 project |
| **tạm hoàn tác 3 csproj — ca đối chứng** | `1` | ✅ bắt đủ **19 dòng High**, cả trực tiếp lẫn transitive |
| `.sln` không tồn tại | `2` | ✅ |
| JSON hỏng | `2` | ✅ |

```bash
bash devops/scripts/check-vulnerable-packages.sh      # chạy y hệt ở máy cá nhân
```

---

### 🧪 NỢ KIỂM THỬ — cái gì đã đo thật, cái gì chưa, và vì sao

**Đọc mục này trước khi tin bất cứ dòng "XONG" nào ở trên.** Mọi mục A–D đều `build 0 Error(s)`
và có chốt `grep` xanh, nhưng **build sạch không phải bằng chứng chạy đúng**. Dưới đây là ranh
giới thật, chia theo *ai tạo ra rủi ro*.

#### ✅ Ưu tiên 1 — rủi ro do mục D tự tạo ra — **ĐÃ ĐO (2026-08-31)**

| Việc | Kết quả | Bằng chứng |
|---|---|---|
| Ghim `Microsoft.OpenApi` 2.7.5 có chạm tới lúc chạy được không | ✅ **`/swagger/v1/swagger.json` và `/openapi/v1.json` đều `200`**, JSON phân giải được, **115 path** + 183/184 schema, `0` exception trong log | xem bên dưới |
| Chạy đủ 9 kịch bản, không chỉ S02/S05 | ✅ **1 instance: 5 ĐẠT / 4 HỎNG / 0 KHÔNG KẾT LUẬN** · **2 instance: 5 / 4 / 0** — không có hồi quy nào từ việc vá gói | [`2026-08-31-du-9-kich-ban-sau-muc-D.md`](evidence/loadprobe/2026-08-31-du-9-kich-ban-sau-muc-D.md) · [`2026-08-31-hai-instance-sau-muc-E.md`](evidence/loadprobe/2026-08-31-hai-instance-sau-muc-E.md) |

**Vì sao "200" ở đây là bằng chứng đủ mạnh.** Nỗi lo là `Swashbuckle.AspNetCore` 10.1.2 **biên
dịch với `Microsoft.OpenApi` 2.4.1** trong khi ghim nâng lên 2.7.5, và hỏng kiểu đó thì **build
vẫn sạch** — chỉ bung lúc chạy. Nên phép đo phải chứng minh **hai** điều, không phải một:

1. **Bản 2.7.5 THẬT SỰ được nạp**, chứ không phải NuGet âm thầm trả lại 2.4.1 làm "200" trở nên
   vô nghĩa. Kiểm ở `API.deps.json` — thứ runtime host thật sự phân giải:
   ```bash
   python3 -c "import json;d=json.load(open('src/API/bin/Debug/net10.0/API.deps.json'));\
   print([k for t in d['targets'].values() for k in t if 'OpenApi' in k or 'Swashbuckle.AspNetCore/' in k])"
   #   → Microsoft.OpenApi/2.7.5  cùng  Swashbuckle.AspNetCore/10.1.2
   ```
   Đây là **ca đối chứng** của phép đo: nếu nó in `2.4.1` thì cái `200` chẳng chứng minh gì.
2. **Bộ sinh tài liệu đi hết bề mặt API**, chứ không trả về một cái vỏ rỗng. `115 path` +
   `183 schema` là con số nói điều đó — một tài liệu 0 path cũng là JSON `200` hợp lệ.

> 💡 `/swagger/v1/swagger.json` trả `openapi 3.0.4` (Swashbuckle) còn `/openapi/v1.json` trả
> `openapi 3.1.1` (bộ sinh sẵn của .NET). **Hai đường độc lập nhau** và cả hai đều còn chạy —
> nên nợ này đóng cho cả hai, không chỉ đường Swashbuckle.

#### 🟠 Ưu tiên 2 — nợ thừa hưởng từ mục A và B — **nửa GIAO DIỆN đã trả xong**

**Đọc bảng này thay vì câu "bốn luồng chưa chạy thật" của bản cũ** — ranh giới đã dịch trong
phiên 2026-08-31 (chiều), và nó dịch **không đều giữa hai nửa**:

| Luồng | Tầng Service (mục A — retry-safe) | Giao diện (mục B — nút double-submit) |
|---|---|---|
| Kiểm kê | ✅ đo tới DB từ trước | ✅ `SupplierDialog` + `CustomerDialog` |
| **Checkout** | ✅ **đã đo** — đặt đơn end-to-end (`200` + `ORD-…`, serial thật) và 50 khách đồng thời, `LogError` khớp 1:1 với số 400 | ✅ **đã đo** — `Orders/OrderDetail` ×2 (kèm `BusyScope`), `Storefront/MyOrderDetail` |
| POS | ❌ chưa chạy hết luồng nghiệp vụ | ✅ **đã đo** — `Pos/Index.SaveDraft` |
| Xuất/nhập kho | ❌ chưa | — không có nút nào trong nhóm 6 |
| Phiếu dịch vụ | ⚠️ *một phần* — S04/S05/S08 chạm `ServiceTicketService` qua API, chưa bấm tay hết luồng | ✅ **đã đo** — `ServiceTicketIntake`, `ServiceTicketQuotation` |

🎯 **6/6 nút double-submit hỏng thật đã đo, tất cả khoá đúng** — kèm **ca đối chứng âm**: tạm đổi
`ActionButton` → `MudButton` trần ở một nút thì 3 cú click cùng tick mở **3 dialog**. Không có ca
đó thì "1 dialog" không chứng minh gì (có thể chỉ nghĩa là cách bắn click bao giờ cũng chỉ ăn cú
đầu). Chi tiết + ba cái bẫy khi dựng phép đo:
[`evidence/ui/2026-08-31-6-nut-double-submit.md`](evidence/ui/2026-08-31-6-nut-double-submit.md).

**Còn nợ ở mục này: nửa *tầng Service* của POS · xuất/nhập kho · phiếu dịch vụ** — tức mục A ở
những luồng đó vẫn chỉ được rà bằng đọc code + build sạch. Nút đã đo ≠ luồng đã chạy.

> 💡 **Cách đo lại nếu cần** (không cần Chrome DevTools MCP, và đừng tranh chấp profile của phiên
> Claude khác): tự bật Chrome headless với `--user-data-dir` riêng + `--remote-debugging-port=9333`,
> rồi lái bằng CDP qua `WebSocket` **built-in của Node 23** — không phải cài gói nào. Driver ~73
> dòng. Ba cái bẫy bắt buộc phải biết trước khi viết lại (điều hướng bằng `Page.navigate` tới trang
> cần quyền **không tới được**; `innerText` của MudBlazor bị uppercase; đặt `input.value` bằng JS
> **không** kích hoạt `@bind-Value`) — cả ba ghi trong file bằng chứng.

**Nguyên nhân gốc không phải "chưa có thời gian" mà là THIẾU SERIAL.** DB local:
`Products = 2`, **`ProductSerials = 0`**, `Orders = 0`. Và
`ProductVariant.StockQuantity = COUNT(ProductSerials WHERE Status = Available)`, nên **không có
serial thì không bán được gì** — cả bốn luồng đều chết ở bước đầu.

⚠️ **Hai script seed hiện có KHÔNG sinh serial.** Đã kiểm: `seed_data.sql` chỉ có
`AppRoles`/`AppUsers`/`AppUserRoles`/`UserProfiles`; `seed_product_data.sql` chỉ có
`Categories`/`Manufacturers`/`Products`/`ProductVariants`. **Không file `.sql` nào chạm tới
`ProductSerials` hay `ImportReceipts`.** Đừng mất thời gian đi tìm — nó không tồn tại.

**Nhưng khả năng seed serial thì ĐÃ CÓ SẴN**, ở chỗ không ai nghĩ tới:
`tools/LoadProbe/Seeding/ProbeFixture.cs` tự `db.ProductSerials.Add(NewSerial(...))` bằng code
(gắn tiền tố `LP-`), chỉ có điều mặc định nó **dọn sạch sau khi chạy**. Có cờ để tắt việc dọn:

```bash
# Seed dữ liệu chạy được (gồm cả ProductSerials) rồi GIỮ LẠI để soi bằng tay
dotnet run --project tools/LoadProbe -- --scenarios S01 --keep
#   → in: "--keep: GIỮ LẠI dữ liệu probe trong DB. Dọn bằng cách chạy lại probe."
#   Sau đó mở Blazor client và bấm tay 4 luồng trên, kèm đếm request bằng hook
#   window.fetch như cách mục B đã đo trên SupplierDialog.
#   Dọn: chạy lại probe KHÔNG có --keep (nó dọn đầu vào lẫn đầu ra).
```

✅ **Đường này đã chạy thật ở phiên 2026-08-31 (chiều), nó hoạt động** — không còn là giả
thuyết: `--scenarios S01 --keep` để lại **60 serial `Available`** trên biến thể `LP-SKU-1`
(`VariantId` 1008), đủ để đặt đơn thật. Kiểm nhanh sau khi seed:

```bash
PW=$(grep -o '^SA_PASSWORD=.*' Infrastructure/db/.env | cut -d= -f2-)
docker exec hushstore_sqlserver_dev /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$PW" -C -I -d HushStoreDb \
  -Q "SELECT v.Id, v.SKU, COUNT(s.Id) FROM ProductVariants v JOIN ProductSerials s ON s.VariantId=v.Id AND s.Status=0 WHERE v.SKU LIKE 'LP-%' GROUP BY v.Id, v.SKU;"
```

**Hai chi tiết đã mất thời gian, đừng vấp lại:**

1. **Khách hàng do probe seed KHÔNG đăng nhập được bằng mật khẩu** — token của chúng được
   *mint* trong RAM (`ProbeEnvironment.MintToken`), không có mật khẩu nào trong DB. Muốn lái tay
   thì **tự đăng ký một khách mới** qua `POST /api/auth/register` rồi `login` — nhanh hơn hẳn
   việc đi dựng lại JWT bằng tay, và tránh phải đoán `ClaimTypes` nào bị map thành `nameid`.
2. **Checkout cần một `UserAddressId` có thật của chính khách đó** — tạo bằng
   `POST /api/storefront/user-addresses`. Thiếu nó thì trả `"Địa chỉ giao hàng không hợp lệ."`,
   trông giống lỗi nghiệp vụ nên rất dễ đi tìm sai chỗ.

Nhớ **dọn** dữ liệu lái tay của mình (khách + đơn + địa chỉ) — probe chỉ dọn thứ mang tiền tố
`LP-` và miền `@loadprobe.local`, nó **không** biết tới tài khoản bạn tự đăng ký.

Đây là đường rẻ nhất để tháo chốt chặn này — **không cần viết script seed mới**.

#### ✅ Cái ĐÃ đo thật rồi — đừng làm lại

| Đã đo | Bằng chứng |
|---|---|
| Transaction chạy dưới retrying strategy | tạo/gửi/duyệt/từ chối phiếu kiểm kê `200`, **đọc thẳng DB** xác nhận `Status`/`ApprovedAt`/`RejectReason` đã ghi |
| Chống double-submit thực sự khoá | `SupplierDialog`, 3 cấu hình, đếm bằng hook `window.fetch`; **ca đối chứng** gỡ `Disabled` cho 2 POST → 2 bản ghi trùng |
| `ActionButton` vô hiệu trên `ButtonType.Submit` | `CustomerDialog`: bản gốc 1 POST, bản đổi sang `ActionButton` **3 POST** |
| EPPlus vẫn chạy sau khi ghim `Cryptography.Xml` | `POST /api/build-pc/export` → `200`, file `Microsoft Excel 2007+`, **đọc ngược lại bằng `zipfile`** thấy đúng nội dung tiếng Việt |
| Cổng chặn lỗ hổng không rỗng | 4 nhánh: sạch `0` · **hoàn tác csproj `1` (19 dòng High)** · sln sai `2` · JSON hỏng `2` |
| Đợt 1 không hồi quy sau mục D | LoadProbe `S02,S05` → **2 ĐẠT, 0 KHÔNG KẾT LUẬN** |
| Khoá tài khoản không kẹt RAM một task | khoá qua replica A → gọi replica B `403` + `X-Account-Status: locked` |
| **Sinh tài liệu OpenAPI sau khi ghim `Microsoft.OpenApi` 2.7.5** | `/swagger/v1/swagger.json` + `/openapi/v1.json` → `200`, **115 path**, 0 exception; **`API.deps.json` xác nhận `2.7.5` thật sự được nạp** cạnh Swashbuckle 10.1.2 |
| **Đủ 9 kịch bản ở CẢ HAI cấu hình sau mục D** | 1 instance **5/4/0** · 2 instance **5/4/0** — round-robin kiểm trước khi đo, 5/5 chia đều |
| **Luồng Checkout chạy thật tới DB** | đặt đơn end-to-end `200` + `ORD-20260831-000014` trên serial thật; **ca đối chứng** cùng payload có mã voucher rác → `400` đúng thông báo nghiệp vụ |
| **Mục 🅴 không nuốt thông báo nghiệp vụ** | một lần chạy S02 sinh **cả ba** lớp thông báo (2 nghiệp vụ nguyên văn + 1 câu chung); 37 request 400 ↔ **37** `LogError` |
| **6/6 nút double-submit hỏng thật của mục B** | 3 click trong **một tick** → đúng 1 dialog (3 nút mở dialog) / đúng 1 POST (3 nút gọi API thẳng); **ca đối chứng âm** `MudButton` trần → **3 dialog** |

⚠️ **`S07` VÀ `S04` ĐẠT nhưng tín hiệu YẾU — hai kịch bản này phụ thuộc thời điểm.**
S07: lần chạy đó POS thua cuộc đua (`400`), nên nhánh nguy hiểm "serial đã bán bị ghi đè thành
Lost" **chưa hề được chạm tới**. S04: 🔴 với 2 instance ở mục C, nhưng **✅ với 2 instance ở phiên
này** — mà `HasOpenTicketForSerialAsync` **không đổi một dòng nào**, nên cái ✅ đó là bằng chứng
*flaky*, không phải bằng chứng *đã sửa*.

**Bất đối xứng cần nhớ:** với kịch bản phụ thuộc thời điểm, một lần 🔴 **là** bằng chứng hỏng,
còn một lần ✅ **không phải** bằng chứng an toàn. Muốn kết luận phía ✅ thì phải chạy lặp và đếm
tỉ lệ.

#### Quy tắc rút ra, áp cho mọi mục sau

1. **`grep` xanh chỉ chứng minh hình dạng code, không chứng minh hành vi.** Chốt ở §6 là chống
   hồi quy *cấu trúc*, không phải bằng chứng chạy đúng.
2. **Mọi phép đo phải có ca đối chứng.** Không có nó thì "1 POST" có thể chỉ nghĩa là kịch bản
   click chưa bao giờ chạm tới handler — xem bẫy #8 và #11.
3. **Kiểm ở DB, không ở mã HTTP.** Mọi lỗi đúng đắn dữ liệu ở repo này đều trả `200`.

---

### ✅ 🅴 Sửa thông báo lỗi tiếng Anh ở `OrderService.CheckoutAsync` — **XONG (2026-08-31)**

**TRƯỚC** (`OrderService.cs:257`) — `ex.Message` là chuỗi của EF Core, nên người dùng cuối nhận:

```
Lỗi hệ thống khi đặt hàng: An error occurred while saving the entity changes.
See the inner exception for details.
```

**SAU** — cùng cuộc đua đó, 37/37 request lỗi của S01 đều nhận:

```
Không thể hoàn tất đặt hàng do lỗi hệ thống. Vui lòng thử lại sau ít phút;
nếu vẫn không được, xin liên hệ bộ phận hỗ trợ.
```

Chi tiết **không mất**, nó chuyển vào `ILogger`: **37 request 400 ↔ 37 bản ghi `LogError`**,
khớp 1:1, và log giữ nguyên nhân gốc thật (`Cannot insert duplicate key row … unique index
'IX_Orders_OrderCode'`). Người vận hành vẫn chẩn đoán được; người dùng không phải đọc tiếng Anh.

Bằng chứng đầy đủ:
[`2026-08-31-muc-E-thong-bao-loi-tieng-viet.md`](evidence/loadprobe/2026-08-31-muc-E-thong-bao-loi-tieng-viet.md).

#### 🚨 Cái bẫy của mục này: "trả một câu tiếng Việt cố định" là lời khuyên CHƯA ĐỦ

Bản trước của mục này (và của `CLAUDE.md`) viết: *"Sửa: trả một câu tiếng Việt cố định, còn
chi tiết `ex` thì đẩy vào `ILogger<T>`."* Làm **đúng y như vậy** thì sinh ra một hồi quy im lặng.

Lý do: cùng khối `catch (Exception)` đó **cũng là đường đi của những thông báo nghiệp vụ đúng và
hữu ích** — `"Mã 'X' đã hết hạn hoặc chưa đến thời gian sử dụng."`, `"Mã 'X' đã hết lượt sử
dụng."`, `"Mã giảm giá không tồn tại: …"` — **13 chỗ** `throw` như vậy trong chính file này, phần
lớn nằm trong `ApplyVouchersAsync`. Thay cả khối catch bằng một câu chung sẽ **nuốt sạch 13 thông
báo đó**, và triệu chứng là: khách nhập mã hết hạn → nhận "lỗi hệ thống, vui lòng thử lại" → bấm
lại → hỏng y hệt, vĩnh viễn, vì họ không bao giờ biết phải bỏ cái mã ra. Build sạch, `grep` xanh.

**Cách đã làm — phân loại tại nguồn, không phân loại bằng cách đoán chuỗi:**

- Thêm `BusinessRuleException` (`src/Core/Exceptions/`) — hợp đồng của nó là *"message bên trong
  đã là thông báo soạn cho người dùng cuối"*. 13 `throw` nghiệp vụ đổi sang kiểu này.
- Khối catch thành hai tầng: `catch (BusinessRuleException) { throw; }` **đứng trước**
  `catch (Exception ex) { _logger.LogError(…); throw new Exception("<câu tiếng Việt cố định>", ex); }`.
- **Cấm** phân loại bằng cách kiểm nội dung `ex.Message` (dò tiếng Việt, dò tiền tố "Mã"). Đó là
  cùng loại sai với `ORDER BY Code DESC` ở bẫy #7 — dùng biểu diễn chuỗi thay cho ngữ nghĩa.
- Cùng lý lẽ với `ConcurrentModificationException`: bắt `InvalidOperationException` thay thế là
  **không** an toàn, EF Core dùng chính kiểu đó cho chuyện khác.

`PlaceOrderAsync` (dòng ~385) có **đúng cùng một lỗi** và đã sửa cùng khuôn. Nó hiện **không có
call-site nào** trong toàn repo (kể cả `IOrderService`) — code chết — nên đừng ngạc nhiên khi
không đo được nó; sửa vì nó là cùng một dòng lỗi, không phải vì nó đang chạy.

**Cách đo đã dùng — và vì sao S02 là phép đo tốt nhất cho việc này.** Một lần chạy S02 sinh ra
**cả ba lớp thông báo cùng lúc**, nên nó chứng minh cả hai nửa của bất biến trong một phép đo:

```json
{"message":"Mã 'LP-VQ1' đã hết lượt sử dụng. Vui lòng bỏ mã này và thử lại."}   ← nghiệp vụ, nguyên văn
{"message":"Mã 'LP-VQ1' đã hết lượt sử dụng."}                                  ← nghiệp vụ, nguyên văn (chỗ khác)
{"message":"Không thể hoàn tất đặt hàng do lỗi hệ thống. …"}                    ← hạ tầng, câu chung
```

Kèm **ca đối chứng lái tay** (đã dọn sau khi đo): mã voucher không tồn tại → `400` +
`"Mã giảm giá không tồn tại: KHONGCOMANAY"`; **cùng payload bỏ voucher đi** → `200` +
`ORD-20260831-000014`. Ca thứ hai bắt buộc phải có — thiếu nó thì cái `400` của ca đầu có thể chỉ
nghĩa là payload sai hay hết tồn kho, tức luật voucher chưa từng chạy tới.

⚠️ Nó **không** sửa nguyên nhân gốc của S01 (sinh mã chứng từ đua nhau) — S01 vẫn 🔴 sau khi sửa,
số đơn đặt được vẫn dao động theo thời điểm (9/50 · 13/50 · 16/50 · 18/50 qua bốn lần chạy). Đó
vẫn là việc của đợt 3. Mục 🅴 chỉ đổi **thứ người dùng đọc được** khi cuộc đua đó thua.

Chốt hồi quy đã chạy (bắt buộc vì đụng tầng Service): `--scenarios S02,S05` → **2 ĐẠT, 0 HỎNG,
0 KHÔNG KẾT LUẬN**, xem
[`2026-08-31-hoi-quy-sau-muc-E.md`](evidence/loadprobe/2026-08-31-hoi-quy-sau-muc-E.md).
Build sau khi sửa: `0 Error(s)` / **184** cảnh báo — **đúng bằng số trước khi sửa**.

---

### 🅷 Cùng lỗi của mục 🅴 còn ở 6 chỗ khác — chưa sửa, CỐ Ý

Quét toàn repo tìm `ex.Message` bị chuyển thẳng cho người dùng:

| Chỗ | Dòng | Ghi chú |
|---|---|---|
| `src/Service/Pos/PosService.cs` | 458, **462** | `"Lỗi khi quá trình thanh toán: " + ex.Message` — **bản sinh đôi y hệt** mục 🅴, chỉ khác luồng (POS tại quầy) |
| `src/Service/Inventory/InventoryCheckService.cs` | 678, 858, 999 | `ApiResult.Fail(ex.Message)` |
| `src/Service/Inventory/InventoryExportService.cs` | 192, **198** | `$"Lỗi khi xuất kho: {ex.Message}"` |
| `src/API/Controllers/…` (`Cart`, `Orders`, `ServiceTickets`, `ServiceInvoices`) | ~35 chỗ | `ApiResult.Fail(ex.Message)` ở tầng controller |

Không gộp vào mục 🅴 vì runbook khoanh mục đó đúng vào `OrderService.cs:257`; ghi thành mục riêng
để việc mở rộng phạm vi là **một quyết định**, không phải một tác dụng phụ.

⚠️ **Đừng làm mục này bằng find-and-replace.** Mỗi chỗ phải phân loại nghiệp vụ / hạ tầng đúng
như mục 🅴 đã làm, nếu không thì đổi một lỗi (lộ tiếng Anh) thành một lỗi tệ hơn (nuốt thông báo
nghiệp vụ, người dùng không còn đường tự sửa). Khuôn đã có sẵn: `BusinessRuleException` + `catch`
hai tầng. Ba luồng liên quan (POS · xuất/nhập kho · phiếu dịch vụ) **cũng đúng là ba luồng chưa
chạy thật** ở mục 🧪 ưu tiên 2 — nên làm mục 🅷 **sau khi** seed được dữ liệu, để sửa xong có
đường đo ngay.

---

### 🅶 Đợt 3 — **VẪN BỊ CHẶN CỨNG**

Không bắt đầu đợt 3 trước khi có kết quả `Infrastructure/db/checks/pre_migration_checks.sql`
**chạy trên RDS**. Chạy trên DB local là vô nghĩa: local gần như rỗng (Orders = 0,
ProductSerials = 0, Products = 2).

Hai câu hỏi và hệ quả:

| Câu hỏi | Nếu kết quả là… | Thì… |
|---|---|---|
| `Vouchers.MaxUsesPerUser` có giá trị `> 1` không? | toàn `NULL`/`1` | unique index `(UserId, VoucherId)` là đủ |
| | có `> 1` | phải thêm cột `SeqPerUser` + backfill `ROW_NUMBER()` — **thêm ~1 ngày công** |
| `InventoryAdjustmentLogs` đã có bản ghi trùng chưa? | rỗng | thêm unique index thẳng |
| | **có** | phát sinh **việc nghiệp vụ**: dọn dữ liệu + đối chiếu sổ tổn thất |

Script **đã sửa cho khớp schema thật** và chạy sạch — chỉ cần trỏ connection string sang RDS.

🔴 **Mục C vừa làm câu hỏi thứ hai nặng hơn hẳn.** LoadProbe S06 tái hiện được lỗi nhân
bản `InventoryAdjustmentLogs` (5 lần `approve` → **5 bản ghi** cho cùng một
`(AuditCheckId, SerialId)`) trên DB sạch, ngay lần chạy đầu. Lỗi này đã chạy trên
production một thời gian, nên xác suất bảng đó **đã có** bản ghi trùng là cao. Nếu đúng,
việc phát sinh là **dọn dữ liệu + đối chiếu sổ tổn thất — việc nghiệp vụ, không phải kỹ
thuật**. Biết bây giờ thì còn thời gian xử; biết lúc migration fail thì không.

**Bật RDS một lần rồi chạy script là việc rẻ nhất còn lại trong toàn bộ kế hoạch.**

---

## 3. Chạy môi trường local ($0)

### DB

```bash
cd Infrastructure/db && docker-compose up -d      # container: hushstore_sqlserver_dev, cổng 1433
```

Mật khẩu `sa` nằm ở `Infrastructure/db/.env` (khoá `SA_PASSWORD`). Đã khớp với volume hiện tại.

```bash
PW=$(grep -o '^SA_PASSWORD=.*' Infrastructure/db/.env | cut -d= -f2-)
docker exec hushstore_sqlserver_dev /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$PW" -C -I -d HushStoreDb -Q "SELECT COUNT(*) FROM Products;"
```

> **Bắt buộc `-C` và `-I`.** `-C` bỏ qua kiểm chứng chỉ self-signed. `-I` bật
> `QUOTED_IDENTIFIER` — thiếu nó thì `seed_data.sql` **hỏng ngay câu INSERT đầu tiên**
> (`Msg 1934`), vì DB có filtered index / computed column.
> Cũng lưu ý: `-y` và `-W` **loại trừ nhau**, đừng dùng chung.

### API

```bash
PW=$(grep -o '^SA_PASSWORD=.*' Infrastructure/db/.env | cut -d= -f2-)
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=HushStoreDb;User Id=sa;Password=${PW};TrustServerCertificate=True;MultipleActiveResultSets=True"
export JwtSettings__SecretKey="$(openssl rand -base64 48 | tr -d '\n')"
export ASPNETCORE_ENVIRONMENT=Development

dotnet run --project src/API/API.csproj --no-launch-profile --urls "http://localhost:5222"
```

**Ba cái bẫy đã mất thời gian, đừng vấp lại:**

1. **`JwtSettings__SecretKey` là bắt buộc.** `appsettings.json` để trống nó **có chủ đích**
   (không commit bí mật). Thiếu → mọi endpoint trả 500 `IDX10703: key length is zero`.
2. **Phải có `--no-launch-profile`.** Không có thì `launchSettings.json` **đè** `--urls` và app
   vẫn bind cổng 5111 → `address already in use` nếu đã có instance khác.
3. **Cổng bị chiếm** thì `pkill -f "API.dll"` **không ăn** (tiến trình là `dotnet run`). Dùng:
   ```bash
   lsof -ti tcp:5222 | xargs -r kill -9
   ```

### Client (chỉ khi cần kiểm giao diện/refresh bằng trình duyệt)

`wwwroot/appsettings.json` trỏ về **production** (`https://api.hushstore.io.vn`). Để trỏ về local,
tạo file **tạm** (nhớ xoá sau, **đừng commit**):

```bash
cat > src/Client/wwwroot/appsettings.Development.json <<'EOF'
{ "ApiBaseUrl": "http://localhost:5222" }
EOF
```

Và API phải cho CORS: khởi động API kèm `export AllowedOrigins="http://localhost:5214"`.

```bash
dotnet run --project src/Client/Client.csproj --no-launch-profile --urls "http://localhost:5214"
```

### Hai replica + nginx (chỉ khi cần kiểm tính chất đa-instance)

```bash
export SA_PASSWORD=$(grep -o '^SA_PASSWORD=.*' Infrastructure/db/.env | cut -d= -f2-)
export JWT_SECRET="$(openssl rand -base64 48 | tr -d '\n')"
docker compose -f devops/docker/docker-compose.multi.yml up --build -d

curl -sD- -o /dev/null localhost:8088/health/live | grep -i x-upstream   # xem replica nào trả lời
docker compose -f devops/docker/docker-compose.multi.yml down
```

Ba cổng: **8088** qua nginx (round-robin), **8081** thẳng vào replica A, **8082** thẳng
vào replica B. Hai cổng sau bắt buộc phải có để gửi lệnh vào đúng container A rồi đọc kết
quả ở đúng container B — qua nginx thì không chọn được đích.

⚠️ Dùng chung DB với môi trường dev (`hushstore_sqlserver_dev`) là **cố ý**: hai instance
phải nhìn cùng một nguồn sự thật thì bài kiểm mới có nghĩa.

⚠️ `devops/docker/docker-compose.local.yml` (bản một replica có sẵn từ trước) đang **trỏ
vào một đường dẫn không tồn tại** (`../nginx/nginx.local.conf`). Đừng lấy nó làm mẫu.

---

### Đăng nhập

**Đăng nhập bằng EMAIL, không phải username** (`LoginRequest.Email`).

| Tài khoản | Email | Mật khẩu | Vai trò |
|---|---|---|---|
| admin | `admin@hushstore.com` | `Admin@123` | Admin |
| employee | `employee@hushstore.com` | *(chưa đặt lại)* | Employee |

```bash
TOKEN=$(curl -s -X POST http://localhost:5222/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@hushstore.com","password":"Admin@123"}' \
  | python3 -c "import sys,json;print((json.load(sys.stdin).get('data') or {}).get('accessToken',''))")
```

---

## 4. Công thức kiểm chứng (đã dùng để tạo bằng chứng cho báo cáo)

```bash
B=http://localhost:5222

# Chặn trần pageSize — kỳ vọng 100 / 1 / 20
for p in 1000000 -5 20; do
  curl -s "$B/api/products?pageSize=$p" | python3 -c "import sys,json;print('$p ->',(json.load(sys.stdin).get('data') or {}).get('pageSize'))"
done

# Rate limit theo IP — kỳ vọng: một số lần 400 rồi chuyển sang 429
# ⚠️ Hạn mức là 5 request/phút cho MỖI IP, tính CẢ lần đăng nhập THÀNH CÔNG ở trên.
#    Nên nếu vừa lấy $TOKEN xong, bạn sẽ thấy 4 lần 400 rồi 429 — KHÔNG PHẢI LỖI.
#    Muốn thấy đủ 5, chờ hết cửa sổ 1 phút rồi chạy lại.
for i in $(seq 1 8); do
  curl -s -o /dev/null -w "%{http_code} " -X POST "$B/api/auth/login" \
    -H 'Content-Type: application/json' -d '{"email":"nobody@x.com","password":"wrong"}'
done; echo

# /health/* PHẢI được miễn trừ — kỳ vọng 0 lần khác 200
fails=0; for i in $(seq 1 150); do
  [ "$(curl -s -o /dev/null -w '%{http_code}' $B/health/live)" != "200" ] && fails=$((fails+1)); done
echo "health non-200: $fails"

# Bề mặt ẩn danh — kỳ vọng 401 / 401 / 200
curl -s -o /dev/null -w "available-for-order %{http_code}\n" -X POST "$B/api/vouchers/available-for-order" -H 'Content-Type: application/json' -d '{"subTotal":100000}'
curl -s -o /dev/null -w "technicians        %{http_code}\n" "$B/api/employees/technicians"
curl -s -o /dev/null -w "validate-code      %{http_code}\n" -X POST "$B/api/vouchers/validate-code" -H 'Content-Type: application/json' -d '{"code":"KHONGCO","subTotal":100000}'

# Transaction chạy được dưới retrying strategy — kỳ vọng 200 + sinh mã KK-
curl -s -X POST "$B/api/inventory-checks" -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d '{"scopeType":0,"note":"smoke"}' | head -c 200

# Sinh tài liệu OpenAPI còn chạy sau khi GHIM Microsoft.OpenApi 2.7.5 — kỳ vọng 200 + JSON hợp lệ
# ✅ ĐÃ CHẠY 2026-08-31: cả hai đều 200, 115 path. Swashbuckle 10.1.2 biên dịch với 2.4.1 nhưng
#    chạy được với 2.7.5. Hỏng kiểu này BUILD VẪN SẠCH nên vẫn phải chạy lại sau mỗi lần đổi ghim.
#    ⚠️ Chỉ "200" là CHƯA đủ: phải kèm ca đối chứng xác nhận 2.7.5 thật sự được nạp, nếu không
#       thì NuGet trả lại 2.4.1 cũng ra 200 và phép đo thành rỗng:
#       python3 -c "import json;d=json.load(open('src/API/bin/Debug/net10.0/API.deps.json'));\
#       print([k for t in d['targets'].values() for k in t if 'OpenApi' in k])"   # → Microsoft.OpenApi/2.7.5
for u in /swagger/v1/swagger.json /openapi/v1.json; do
  printf '%s -> ' "$u"
  curl -s "$B$u" | python3 -c "import sys,json;d=json.load(sys.stdin);print('OK, openapi',d.get('openapi'),'| paths:',len(d.get('paths',{})))" 2>&1 | head -1
done

# EPPlus còn xuất được Excel sau khi GHIM Cryptography.Xml 10.0.10 — kỳ vọng 200 + file xlsx thật
curl -s -X POST "$B/api/build-pc/export" -H 'Content-Type: application/json' \
  -d '{"items":[{"slotIndex":1,"slotName":"CPU","variantId":1,"productName":"X","variantName":"Y","sku":"S1","warrantyMonth":12,"unitPrice":1000,"quantity":1}]}' \
  -o /tmp/probe.xlsx -w 'export %{http_code} %{size_download}B\n'
python3 -c "import zipfile;z=zipfile.ZipFile('/tmp/probe.xlsx');print('xlsx hop le,',len(z.namelist()),'entry')"
```

> Nhớ **dọn dữ liệu test** sau khi kiểm (phiếu kiểm kê tạo ra, mô tả sản phẩm đã sửa…).
> Phiên trước để lại DB đúng nguyên trạng: `InventoryChecks = 0`.

**Kiểm luồng refresh bằng trình duyệt** (chỉ khi đụng vào auth): đặt
`JwtSettings__AccessTokenExpirationMinutes=1`, đăng nhập, để token hết hạn **> 78 giây**
(phải vượt `ClockSkew` 60 giây, nếu không server vẫn chấp nhận token cũ và **không có 401 nào**),
rồi bấm một nút bắn nhiều request cùng lúc. Kỳ vọng: **N lời gọi 401 → đúng 1 lời gọi
`/api/auth/refresh-token` → N lời gọi lại thành công**, cả hai token xoay vòng, không về `/login`.

---

**Đo tính đúng đắn dưới tải đồng thời — dùng `tools/LoadProbe/`, không viết tay.**

```bash
PW=$(grep -o '^SA_PASSWORD=.*' Infrastructure/db/.env | cut -d= -f2-)
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=HushStoreDb;User Id=sa;Password=${PW};TrustServerCertificate=True;MultipleActiveResultSets=True"
export JwtSettings__SecretKey="<ĐÚNG khoá API đang chạy đang dùng>"

dotnet run --project tools/LoadProbe -- --out docs/evidence/loadprobe            # 1 instance
dotnet run --project tools/LoadProbe -- --api http://localhost:8088 --out docs/evidence/loadprobe   # 2 instance
```

Nó tự dọn dữ liệu trước và sau. Mã thoát: `0` đạt hết · `1` có bất biến sai ·
`2` có kịch bản **không kết luận được** · `3` lỗi môi trường.

> ⚠️ **`KHÔNG KẾT LUẬN` không được đọc thành "đạt".** Nó nghĩa là phép đo bị rỗng —
> request ăn 429, seed thiếu, API không phản hồi. Kịch bản bị chặn hết sẽ **thoả mọi bất
> biến** vì code cần đo chưa từng chạy. Gặp nó thì tăng `--pace` rồi chạy lại.

> ⚠️ **Chạy cả hai cấu hình.** S04 ĐẠT với 1 instance và HỎNG với 2 — kết luận từ một
> cấu hình là kết luận sai. Xem mục 🅵.

---

## 5. Mười ba cái bẫy im lặng đã gặp — đọc trước khi sửa code

Tất cả đều **không sinh lỗi, không sinh cảnh báo**, và chỉ lộ ra khi đo.

1. **Ghi vào entity `AsNoTracking()`** — lệnh gán rơi vào hư vô, `SaveChanges` không sinh `UPDATE`.
   *Chống tái phát:* các phương thức no-tracking đã đổi tên thành `...ReadOnlyAsync`, nên gọi nhầm
   là **build vỡ**.
2. **Retry + Change Tracker** — lỗi transient lúc commit → entity thành `Unchanged` với snapshot =
   giá trị **mới** → lần thử lại gán cùng giá trị → **không sinh `UPDATE`** → mất dữ liệu.
   Biến thể tệ hơn: `+=` trên entity tracked → **ghi sai số** (xem mục A).
3. **`UseHsts()` đặt trước `UseForwardedHeaders()`** — no-op hoàn toàn im lặng. Container nhận HTTP
   thuần từ ALB nên `Request.IsHttps` chỉ đúng **sau khi** đọc `X-Forwarded-Proto`.
4. **`ClockSkew = 1 phút`** — vòng đời access token thực tế là **15 + 1** phút. Đừng bối rối khi đo.
5. **Cờ bận đặt SAU `await` mà không có `StateHasChanged()`** — `ComponentBase` chỉ tự
   `StateHasChanged()` sau phần **đồng bộ** của handler, nên cờ không bao giờ tới được UI.
   Bốn người đã viết đúng ý định và vẫn sai.
   ⚠️ **Cần cả hai điều kiện.** Đo ở mục B: cờ đặt sau `await` **nhưng có** `StateHasChanged()`
   theo sau thì vẫn chạy đúng, và cờ đặt **trước mọi `await`** cũng chạy đúng dù không gọi
   `StateHasChanged()`. Đừng suy ra "mọi cờ thủ công đều hỏng" — 17/23 nút ở mục B vốn đã đúng.
6. **`CascadingValue` mang `this`** — tham chiếu không đổi nên Blazor **không** render lại component
   con. Chốt chặn vẫn chạy đúng nhưng nút anh em **không chuyển sang mờ**. Phải phát event riêng.
7. **`ORDER BY Code DESC` để tìm mã cuối** — so sánh **chuỗi**, nên `-1000` sắp **trước** `-999`.
   Đây là nguyên nhân gốc của quả bom `{n:D3}`. Nay `IDocumentCodeGenerator` lấy max **theo số**.
8. **Phép đo RỖNG in ra "đạt"** — kịch bản mà 49/50 request ăn 429 **thoả mọi bất biến**,
   vì code cần đo chưa từng chạy. Đây là lý do LoadProbe có hạng `KHÔNG KẾT LUẬN` và kiểm
   nó **trước** phần khẳng định. Bằng chứng an toàn giả nguy hiểm hơn không có bằng chứng.
9. **`upstream` của nginx phân giải DNS ĐÚNG MỘT LẦN lúc khởi động** — với
   `docker compose --scale api=2`, cái tên đó ra một địa chỉ và nginx gửi 100% traffic vào
   đúng một container suốt đời. Bài kiểm "đa instance" âm thầm thành bài kiểm một instance,
   log vẫn đẹp. Phải **liệt kê tường minh** từng host.
   ⚠️ **Biến thể mới, gặp ở phiên 2026-08-31:** đo **quá sớm** sau `compose up` thì header
   `X-Upstream` trả **hai** địa chỉ mỗi phản hồi (`172.21.0.2:8080, 172.21.0.3:8080`). Đó không
   phải round-robin — `$upstream_addr` đang liệt kê **chuỗi đã thử**: replica thứ nhất chưa kịp
   khởi động, nginx thất bại rồi chuyển sang replica thứ hai. Đo trong cửa sổ đó thì mỗi request
   đi qua **cả hai** container và mọi kết luận đa-instance đều vô nghĩa. **Một địa chỉ mỗi phản
   hồi = round-robin; hai địa chỉ = retry.** Chờ ấm rồi kiểm lại tới khi ra tỉ lệ chia đều.
10. **Tên bảng ≠ tên `DbSet`** — `ServiceTicketStatusHistory` là **số ít** trong DB. Viết SQL
    thô theo tên `DbSet` là lỗi 208 *Invalid object name*. Tra trước bằng
    `SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'`.
11. **`dotnet list package --vulnerable` trả về mã thoát `0` KHI CÓ lỗ hổng** — đã đo lúc repo
    còn 10 advisory High đang mở: nó in đủ 10 dòng rồi trả về `0`. Một bước CI viết thẳng lệnh
    đó sẽ **luôn xanh vĩnh viễn**. Cùng họ với bẫy #8: cửa kiểm không bao giờ đỏ trông giống hệt
    cửa kiểm luôn đạt. Cổng phải **đọc báo cáo**, không tin mã thoát — xem
    `devops/scripts/check-vulnerable-packages.sh`.
12. **Kịch bản phụ thuộc thời điểm chuyển từ 🔴 sang ✅ mà không ai sửa gì** — S04 HỎNG với 2
    instance ở mục C, ĐẠT với 2 instance ở phiên sau, `HasOpenTicketForSerialAsync` **không đổi
    một dòng**. Đọc thành "đã sửa" là mất luôn một lỗi thật. Bất đối xứng phải nhớ: một lần 🔴
    **là** bằng chứng hỏng; một lần ✅ **không** là bằng chứng an toàn. Cùng họ với bẫy #8 —
    nhưng nguy hơn, vì ở đây phép đo **không** rỗng, nó chỉ đơn giản là thua xác suất.
    Hệ quả: **đừng ghi kỳ vọng dạng "vẫn đúng N/9 HỎNG"** mà không nói rõ **cấu hình nào** và
    rằng N là **hợp của nhiều lần chạy** — chính bản trước của mục 🧪 đã ghi "5/9" cho một lệnh
    chạy 1 instance, mà 1 instance thì kỳ vọng đúng là 4/9.
13. **Thay khối `catch` bằng "một câu tiếng Việt cố định" nuốt luôn thông báo NGHIỆP VỤ** —
    khối `catch (Exception)` của `CheckoutAsync` là đường đi của **cả hai** loại: lỗi hạ tầng EF
    (tiếng Anh, phải chặn) và **13** thông báo nghiệp vụ đã soạn cho người dùng (`"Mã 'X' đã hết
    lượt sử dụng."`). Sửa theo lời khuyên hiển nhiên là đúng nửa đầu và **phá nửa sau**: khách
    nhập mã hết hạn nhận "lỗi hệ thống, vui lòng thử lại", bấm lại hỏng y hệt vĩnh viễn vì không
    ai nói cho họ biết phải bỏ mã ra. Build sạch, `grep` xanh. Phải phân loại **tại nguồn** bằng
    một kiểu riêng (`BusinessRuleException`) — **không** bằng cách dò nội dung `ex.Message`, đó
    lại là dùng biểu diễn chuỗi thay cho ngữ nghĩa như bẫy #7.

---

## 6. Chốt chống hồi quy (trước đây là "script lấy lại danh sách việc")

Mục A, B và C đều xong, nên các lệnh dưới không còn là danh sách việc — chúng là **chốt
chống hồi quy**. Chạy lại sau khi sửa tầng Service hay thêm nút mutation mới:

```bash
# A — transaction retry-safe
grep -rn "CHƯA RÀ RETRY" --include='*.cs' src/Service/          # kỳ vọng: rỗng
grep -rc "}, retrySafe: true)" --include='*.cs' src/Service/ | grep -v ':0'   # tổng 18

# B — chống double-submit
grep -rn "<ActionButton" --include='*.razor' src/Client/Pages/ | wc -l   # kỳ vọng: 46
grep -rn "<MudButton"    --include='*.razor' src/Client/Pages/ | wc -l   # kỳ vọng: 136

# B — cờ bận thủ công CHỈ được còn ở 3 file, không thêm file nào khác
grep -rln "_isSaving\|_isSubmitting\|_isApproving\|_isConfirming" \
  --include='*.razor' src/Client/Pages/
#   WriteReviewDialog.razor  -> chỉ là COMMENT, không phải code
#   CustomerDialog.razor     -> CỐ Ý giữ: nút ButtonType.Submit, xem cảnh báo 🚨 ở mục B
#   EmployeeDialog.razor     -> CỐ Ý giữ: lý do như trên
```

```bash
# E — thông báo lỗi user-facing không được nối ex.Message trong OrderService
grep -n 'ex\.Message' src/Service/Orders/OrderService.cs | grep -v '//'    # kỳ vọng: rỗng
#   (chi tiết ex CHỈ được đi vào _logger.LogError, không đi vào message trả cho client)
#   ⚠️ Phải có `| grep -v '//'`: bản thân file có MỘT comment nhắc "KHÔNG nối ex.Message…",
#      nên bỏ bộ lọc đi thì chốt này báo động giả vĩnh viễn — và một chốt luôn đỏ sẽ bị bỏ qua
#      y như một chốt luôn xanh (bẫy #11).

# E — 13 throw nghiệp vụ vẫn là BusinessRuleException, không tụt về Exception trần
grep -c 'throw new BusinessRuleException' src/Service/Orders/OrderService.cs   # kỳ vọng: 13
grep -c 'catch (BusinessRuleException)'    src/Service/Orders/OrderService.cs   # kỳ vọng: 2
#   ⚠️ Con số thứ hai quan trọng hơn con số thứ nhất: thiếu catch thì 13 throw kia
#      rơi vào catch (Exception) và bị thay bằng câu chung — đúng cái hồi quy mục 🅴 tránh.
#      Và catch (BusinessRuleException) PHẢI đứng TRƯỚC catch (Exception) trong cùng khối try.
```

```bash
# D — không gói nào mức High/Critical quay lại (cần mạng để tải advisory)
bash devops/scripts/check-vulnerable-packages.sh
#   kỳ vọng: "Sạch: 7 project, 0 lỗ hổng High/Critical." và mã thoát 0.
#   Mã thoát 2 nghĩa là KHÔNG KẾT LUẬN (restore hỏng / mất mạng) — KHÔNG phải sạch.

# D — AutoMapper không được thêm lại (repo dùng projection LINQ, xem CLAUDE.md)
grep -rn "AutoMapper" --include='*.csproj' src/ tools/    # kỳ vọng: rỗng
```

```bash
# C — bộ đo còn chạy được (cần DB + API đang chạy)
dotnet build tools/LoadProbe/LoadProbe.csproj      # kỳ vọng: 0 Error(s)
dotnet run --project tools/LoadProbe -- --scenarios S02,S05 --pace 11
#   kỳ vọng: 2 ĐẠT, 0 KHÔNG KẾT LUẬN. Hai kịch bản này đo chính thứ đợt 1 đã sửa
#   (ExecuteUpdateAsync có vị từ + TryDecideAsync) nên chúng là chốt hồi quy của đợt 1.
#   Nếu chúng chuyển sang HỎNG thì ai đó vừa đưa check-then-act quay lại.
```

> 137 `<MudButton>` còn lại **không phải việc tồn đọng**: chúng là nút điều hướng, đóng dialog,
> lọc, chuyển tab — không gọi mutation nên không cần khoá. Đừng chuyển chúng cho "đủ bộ".

---

## 7. Quy ước bắt buộc — vi phạm là tái tạo đúng lỗi cũ

Chi tiết trong [`CLAUDE.md`](../CLAUDE.md), nhắc lại bốn cái hay quên nhất:

- **Transaction — chỉ qua `IUnitOfWork.ExecuteInTransactionAsync`.** Cấm
  `_context.Database.BeginTransactionAsync()` thủ công: EF Core cấm nó khi có retrying strategy và
  ném **lúc chạy**. Việc làm *sau* commit phải nằm **ngoài** delegate.
- **Sinh mã chứng từ — chỉ qua `IDocumentCodeGenerator`.**
- **Không cache trạng thái phân quyền / khoá tài khoản trong `MemoryCache`.** Hướng nguy hiểm là
  hướng **mở khoá**. Đây là lỗi bảo mật, không phải lỗi hiệu năng.
- **DTO phân trang — kế thừa `PagedRequest`.** Trần `[1, 100]` dùng chung với `ClampPageSizeFilter`;
  đổi một bên phải đổi bên kia.

Và: **mọi thông báo lỗi cho người dùng phải bằng tiếng Việt có dấu.** Trường free-text mới mà sẽ
render bằng `MarkupString` thì **phải** đi qua `IHtmlContentSanitizer` lúc **ghi**.
