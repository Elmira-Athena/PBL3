***

# IMPLEMENTATION PLAN: ORDER MANAGEMENT UI (BLAZOR)
**Domain:** Back-Office Web Client
**Mục tiêu:** Xây dựng giao diện Danh sách đơn hàng cho Admin/Sales và giao diện chuyên biệt cho nhân viên Kho.

## 1. CLIENT SERVICES (KẾT NỐI API)
Cập nhật `IOrderClientService` và `OrderClientService`:
* Thêm hàm `Task<PagedResult<OrderSummaryResponse>> GetPagedOrdersAsync(OrderFilterRequest request)` gọi `GET /api/orders`.
* Thêm hàm `Task<ApiResult<bool>> CancelOrderAsync(int id, string cancelReason)` gọi `PUT /api/orders/{id}/cancel`.

## 2. GIAO DIỆN 1: QUẢN LÝ ĐƠN HÀNG (DÀNH CHO ADMIN/SALES)
* **Đường dẫn:** `/orders` (hoặc `/admin/orders`).
* **Quyền truy cập:** `Admin`, `Employee`.
* **Component `OrderList.razor`:**
    * **Bộ lọc (Filters):** Input tìm kiếm theo SĐT/Mã đơn, Dropdown lọc theo `Status` (Tất cả, Chờ duyệt, Chờ xuất kho, Đang giao, Thành công, Đã hủy).
    * **DataGrid (Bảng dữ liệu):** Hiển thị các cột Mã đơn, Khách hàng, SĐT, Ngày tạo, Tổng tiền, Trạng thái (dùng các Badge/Chip màu sắc khác nhau cho dễ nhìn).
    * **Hành động (Actions):** * Nút "Xem chi tiết" (Mở Dialog hoặc chuyển trang sang trang chi tiết).
        * Nút "Hủy đơn": Chỉ hiển thị nếu `Status` là 0 hoặc 1. Khi bấm vào, mở ra một `MudMessageBox` (hoặc Dialog tương đương) bắt buộc nhập `CancelReason` rồi mới gọi API Hủy.

## 3. GIAO DIỆN 2: DANH SÁCH CHỜ XUẤT KHO (DÀNH CHO WAREHOUSE)
* **Đường dẫn:** `/inventory/pending-export`.
* **Quyền truy cập:** `WarehouseManager`, `Admin`.
* **Component `PendingExportOrders.razor`:**
    * **Logic Cốt lõi:** Khi component khởi tạo (`OnInitializedAsync`), **Tự động ép cứng** tham số `Status = 1` (Confirmed) vào `OrderFilterRequest` trước khi gọi API. Tuyệt đối không cho nhân viên kho đổi filter trạng thái này.
    * **DataGrid:** Hiển thị danh sách các đơn hàng ĐÃ CHỐT chờ xuất kho.
    * **Hành động (Nối luồng):**
        * Mỗi dòng có một nút to, nổi bật: **"🚀 Tiến hành Xuất kho"**.
        * Khi bấm nút này, dùng `NavigationManager` chuyển hướng (Redirect) sang trang `/inventory/export/{OrderId}` (Cái màn hình dùng súng quét mã vạch mà chúng ta đã làm ở Phase trước).

***

### PROMPT DÀNH CHO CLAUDE OPUS (DỰNG UI)

Bạn hãy copy đoạn Prompt này ném cho AI Coder. Thằng Opus làm UI bằng MudBlazor rất mượt, chỉ cần dặn kỹ luồng điều hướng là nó làm ngon ơ:

> "Backend API quản lý đơn hàng đã xong. Giờ tao cần mày dựng giao diện Blazor Client dựa theo bản thiết kế `@order_management_ui_phase.md`.
> 
> **Mày phải tạo 2 Component riêng biệt:**
> 1. `OrderList.razor` cho Admin: Hiển thị full filter. Nhớ xử lý cái popup Dialog bắt nhập lý do khi bấm Hủy đơn.
> 2. `PendingExportOrders.razor` cho Kho: **Bắt buộc** ép cứng tham số `request.Status = 1` khi gọi API `GetPagedOrdersAsync`. Ở mỗi dòng đơn hàng, làm một nút 'Tiến hành Xuất kho' để Navigate thẳng sang trang `/inventory/export/{id}`.
> 
> Nhớ cập nhật lại cái component `ExportOrder.razor` ở Phase trước để nó nhận tham số `{id}` từ URL (Route Parameter) thay vì bắt người dùng tự gõ tay ô input nhé!
> 
> Hãy viết code UI thật chuyên nghiệp, dùng các thẻ Chip/Badge để phân biệt màu sắc các trạng thái đơn hàng (ví dụ: Thành công = Xanh lá, Đã Hủy = Đỏ, Chờ xuất = Vàng)."
