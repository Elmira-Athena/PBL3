# Bắt đầu phiên mới — đọc file này trước

**Cập nhật:** 2026-08-31 · **Trạng thái repo:** nhánh `feat/retry-safe-call-sites`, build `0 Error(s)`
· **Đã xong:** đợt 1, mục 4.1, đợt 2, **mục A**, **mục B**, **mục C**, **mục D** · **Kế tiếp:** đợt 3 (đang bị chặn)

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

### 🅵 Kết quả đo — **5/9 bất biến SAI**, đọc trước khi làm đợt 3

`0 KHÔNG KẾT LUẬN` ở cả hai lần chạy, tức không có phép đo nào bị rate limiter làm rỗng.

| # | Kịch bản | 1 instance | 2 instance |
|---|---|---|---|
| S01 | 50 khách checkout đồng thời | 🔴 | 🔴 |
| S02 | Voucher `Quantity=1`, 20 khách | ✅ | ✅ |
| S03 | Cùng khách, `MaxUsesPerUser=1` | ✅ | ✅ |
| S04 | 10 lần `intake` cùng serial | ✅ | 🔴 |
| S05 | 10 lần `accept-quotation` | ✅ | ✅ |
| S06 | 5 lần `approve` phiếu kiểm kê | 🔴 | 🔴 |
| S07 | POS bán S xen kẽ kiểm kê đánh S Lost | ✅ | ✅ |
| S08 | 2 lần `create-quotation` song song | 🔴 | 🔴 |
| S09 | 2 lần `refresh-token` cùng cặp | 🔴 | 🔴 |

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

#### 🔴 S04 — chỉ vỡ khi có HAI instance

✅ với 1 instance, 🔴 với 2 instance (2 phiếu dịch vụ chưa đóng cho cùng một serial).
Đúng loại lỗi mà toàn bộ hạ tầng 2 replica sinh ra để bắt: cửa sổ check-then-act của
`HasOpenTicketForSerialAsync` đủ hẹp để một tiến trình che được, nhưng hai tiến trình thì
không. **Đừng kết luận từ lần chạy một instance.**

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

### 🅴 Đợt 3 — **VẪN BỊ CHẶN CỨNG**

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

## 5. Mười một cái bẫy im lặng đã gặp — đọc trước khi sửa code

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
10. **Tên bảng ≠ tên `DbSet`** — `ServiceTicketStatusHistory` là **số ít** trong DB. Viết SQL
    thô theo tên `DbSet` là lỗi 208 *Invalid object name*. Tra trước bằng
    `SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'`.
11. **`dotnet list package --vulnerable` trả về mã thoát `0` KHI CÓ lỗ hổng** — đã đo lúc repo
    còn 10 advisory High đang mở: nó in đủ 10 dòng rồi trả về `0`. Một bước CI viết thẳng lệnh
    đó sẽ **luôn xanh vĩnh viễn**. Cùng họ với bẫy #8: cửa kiểm không bao giờ đỏ trông giống hệt
    cửa kiểm luôn đạt. Cổng phải **đọc báo cáo**, không tin mã thoát — xem
    `devops/scripts/check-vulnerable-packages.sh`.

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
