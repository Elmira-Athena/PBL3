# 6 nút double-submit hỏng thật của mục B — **đã đo, cả 6 đều khoá đúng**

> **Vì sao file này quan trọng hơn các bằng chứng khác của mục B.** Đo thực tế ở mục B cho
> thấy chỉ **6/23** nút thật sự không có bảo vệ nào trước khi sửa; 17 nút còn lại đã có cờ thủ
> công và cờ đó chạy đúng. Nghĩa là **toàn bộ phần *vá lỗi thật* của mục B nằm ở đúng 6 nút
> này** — và cho tới phiên 2026-08-31 (chiều) thì **chưa nút nào trong 6 được đo**, vì 5/6 nằm
> trong bốn luồng chưa chạy thật (Checkout · POS · xuất/nhập kho · phiếu dịch vụ). Bằng chứng
> duy nhất mục B từng có là `SupplierDialog` và `CustomerDialog` — hai nút **không** thuộc nhóm
> hỏng thật.
>
> **Kết quả: 6/6 khoá đúng. 0 nút hồi quy.**

- **Ngày đo:** 2026-08-31 · API `http://localhost:5222`, Blazor client `http://localhost:5214`
- **Dữ liệu:** seed bằng `dotnet run --project tools/LoadProbe -- --scenarios S01,S04,S08 --keep`
  → 60 serial `Available`, 17 đơn `Pending`, 2 phiếu dịch vụ chưa đóng
- **Đã dọn sạch sau khi đo** (xem §5)

---

## 1. Bất biến được đo, và vì sao nó phải đo trong MỘT tick

`ActionButton` không dựa vào thuộc tính `disabled` của DOM để chặn — nó đặt **cờ bận của
riêng nút** *trước khi* handler chạy (`BusyState.TryBegin()`). Vì vậy phép đo đúng là:

> **Bấm N lần trong MỘT khối JS đồng bộ**, tức trước khi Blazor có cơ hội render lại, rồi đếm
> xem có bao nhiêu *hành động* thật sự khởi động.

Bấm chậm hai lần thì `disabled` đã kịp lên và phép đo trở nên vô nghĩa — nó chỉ chứng minh
CSS hoạt động. Bấm nhanh trong một tick mới ép đúng vào khe hở mà bẫy #5 nói tới.

**Đếm cái gì thì tuỳ nút,** và đây là chỗ dễ đo sai:

| Loại handler | 3 nút | Đếm |
|---|---|---|
| Mở **dialog xác nhận** trước rồi mới gọi API | `Orders/OrderDetail` ×2, `Storefront/MyOrderDetail` | **số dialog** mở ra |
| Gọi **API thẳng** | `ServiceTicketIntake`, `ServiceTicketQuotation`, `Pos/Index` | **số request POST** |

Đếm request ở nhóm đầu sẽ luôn ra `0` (API chỉ chạy sau khi người dùng xác nhận) và dễ bị đọc
thành "đã khoá" — đúng loại phép đo rỗng của bẫy #8.

## 2. Kết quả

| # | Trang · nút | Ca đối chứng: 1 click | **Bất biến: 3 click / 1 tick** | `disabled` sau tick |
|---|---|---|---|---|
| 1 | `Orders/OrderDetail` · **Duyệt đơn** | 1 dialog | **1 dialog** ✅ | `true` |
| 2 | `Orders/OrderDetail` · **Hủy đơn** | 1 dialog | **1 dialog** ✅ | `true` |
| 3 | `Storefront/MyOrderDetail` · **Hủy đơn** | 1 dialog | **1 dialog** ✅ | `true` |
| 4 | `ServiceTicketIntake` · **Hoàn tất tiếp nhận** | 1 × `POST /api/service-tickets` | **1 POST** ✅ | `true` |
| 5 | `ServiceTicketQuotation` · **Lưu Báo Giá** | 1 × `POST /api/service-tickets/61/quotation` | **1 POST** ✅ | `true` |
| 6 | `Pos/Index` · **Lưu tạm** | 1 × `POST /api/pos/draft` | **1 POST** ✅ | `true` |

Nút 1 và 2 cùng hiện khi đơn ở `Status = 0`, tức đây cũng là phép đo của `BusyScope` — hai nút
**khác nhau** trên cùng một đối tượng, thứ mà khoá riêng từng nút không đủ để chặn.

## 3. 🔴 CA ĐỐI CHỨNG ÂM — thứ làm cả bảng trên có nghĩa

Không có mục này thì "3 click → 1 dialog" **không chứng minh được gì**: nó có thể chỉ nghĩa là
cách bắn click của phép đo bao giờ cũng chỉ ăn cú đầu. Nên đã **tạm phá bảo vệ** của đúng nút
số 1 (`ActionButton` → `MudButton` trần, build lại client), đo lại cùng kịch bản, rồi khôi phục:

| Cấu hình `Orders/OrderDetail` · Duyệt đơn | 1 click | **3 click / 1 tick** | `disabled` sau tick |
|---|---|---|---|
| `ActionButton` — bản thật | 1 dialog | **1 dialog** | `true` |
| `MudButton` trần — **ca đối chứng** | 1 dialog | **🔴 3 dialog** | `false` |

**3 cú click đồng bộ CÓ THỂ mở 3 dialog.** Vậy con số `1` ở bản thật là do cờ chặn thật.

Cột `disabled` sau tick còn nói thêm một điều về *cơ chế*: `ActionButton` → `true`, `MudButton`
trần → `false`. Blazor WASM đơn luồng nên `element.click()` chạy handler C# **đồng bộ tới `await`
đầu tiên**, kèm một lần render đồng bộ — nên với `ActionButton`, `disabled` đã lên **ngay trong
tick**. Đó chính là ý nghĩa vận hành của câu "cờ đặt TRƯỚC mọi `await`" trong `CLAUDE.md`.

> ⚠️ File đã được khôi phục và `git diff` sạch. Ca đối chứng này **không** được commit.

## 4. Ba cái bẫy đã mất thời gian khi dựng phép đo

1. **Điều hướng bằng `Page.navigate` tới trang cần quyền thì KHÔNG tới được.** `AuthorizeRouteView`
   phán quyết **trước khi** `AuthenticationStateProvider` kịp đọc `localStorage`, nên app đẩy về
   trang mặc định (`/admin/reports`) — và log trông như routing sai, dù request `GET /api/orders/502`
   **có** bay đi. Cách đúng: nạp token → reload **một** lần để provider đọc → rồi điều hướng
   **phía client** bằng cách chèn `<a href="...">` và click (router Blazor chặn link nội bộ, không
   reload). Đây là lý do harness có hàm `navClient` riêng.
2. **`innerText` của MudBlazor bị `text-transform: uppercase`.** Tìm nút bằng `includes('Duyệt đơn')`
   luôn trượt vì DOM trả `DUYỆT ĐƠN`. Phải so khớp không phân biệt hoa/thường.
3. **Đặt `input.value` bằng JS KHÔNG kích hoạt `@bind-Value`.** Phải gọi setter gốc của prototype
   rồi phát cả `input` và `change` — MudTextField mặc định bind ở `change`.

Và một trở ngại môi trường: **Chrome DevTools MCP không dùng được** vì một phiên Claude Code khác
(mở 10 tiếng trước) đang giữ profile `~/.cache/chrome-devtools-mcp/chrome-profile`. Không kill nó —
đó là trạng thái của phiên khác. Thay vào đó tự bật một Chrome headless với `--user-data-dir` riêng
+ `--remote-debugging-port=9333` và lái bằng CDP qua `WebSocket` **built-in của Node 23** (không cài
gói nào). Driver ~73 dòng, giữ trong scratchpad.

## 5. Dọn dẹp

Đã dọn: khách tự đăng ký + địa chỉ + đơn của họ · phiếu dịch vụ và báo giá do phép đo tạo · đơn
nháp POS · serial bị chuyển sang `Sold` được trả về `Available` với `OrderId = NULL`. Dữ liệu `LP-`
dọn bằng cách chạy lại probe không có `--keep`. `src/Client/wwwroot/appsettings.Development.json`
là file tạm (đã có trong `.gitignore`) và đã xoá.

**DB về đúng nguyên trạng, đã kiểm bằng truy vấn:**

```
Products = 2 · ProductSerials = 0 · Orders = 0 · ServiceTickets = 0
Quotations = 0 · InventoryChecks = 0 · tài khoản rác = 0
```

Và chốt hồi quy chạy lại sau khi đo xong: `--scenarios S02,S05` → **2 ĐẠT, 0 HỎNG,
0 KHÔNG KẾT LUẬN**. (Phép đo UI không sửa code nào — ca đối chứng âm đã được khôi phục và
`git diff` sạch — nên đây chỉ là chốt xác nhận, không phải phép đo mới.)

---

## Cái này KHÔNG chứng minh điều gì

⚠️ **Không bảo vệ server.** Hai tab, F5 giữa chừng, hay `curl` vẫn double-submit như thường. Phép
đo trên thuần tuý là UX: ngăn người dùng **thật** vô tình bấm hai lần. Phòng tuyến thật vẫn là
conditional update + unique index ở tầng DB — và LoadProbe vẫn đang báo **4 bất biến HỎNG mỗi lần
chạy** ở đúng tầng đó. Đừng đọc file này thành "nhóm lỗi đồng thời đã xong".

⚠️ **Không phủ `ButtonType.Submit`.** `Admin/Customers/CustomerDialog` và
`Admin/Employees/EmployeeDialog` cố ý giữ cờ thủ công vì `ActionButton` **vô hiệu** trên nút submit
của `EditForm` — đã đo riêng ở mục B (bản đổi sang `ActionButton` cho **3 POST**). Hai file đó
không nằm trong 6 nút này.
