# Order Confirm Button & Serial Real-time Validate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Thêm nút "Duyệt đơn" (Pending→Confirmed) cho Admin/Employee và validate mã serial real-time ngay khi nhập trên trang xuất kho.

**Architecture:** Hai tính năng độc lập — Feature A (Duyệt đơn) theo đúng pattern của `CompleteOrderAsync` hiện tại xuyên suốt 5 lớp; Feature B (Serial validate) thêm endpoint GET mới dùng `GetBySerialNumberAsync` đã có trong repo rồi thay đổi logic `OnSerialKeyUp` ở client.

**Tech Stack:** ASP.NET Core 10 Web API, Blazor WASM, MudBlazor, EF Core, `IProductSerialRepository.GetBySerialNumberAsync`

**Lưu ý quan trọng:** Project chưa có automated tests. Mỗi task kết thúc bằng `dotnet build` để xác nhận compile, sau đó test thủ công qua UI.

---

## FEATURE A — Nút Duyệt Đơn

### Task 1: IOrderService — thêm ConfirmOrderAsync

**Files:**
- Modify: `src/Service/Orders/IOrderService.cs`

- [ ] **Bước 1: Thêm method vào interface**

Mở `src/Service/Orders/IOrderService.cs`, thêm dòng cuối cùng trong interface (trước dấu `}`):

```csharp
Task<ApiResult<bool>> ConfirmOrderAsync(int id);
```

File sau khi sửa:
```csharp
using System;
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Sale;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Service.Orders
{
    public interface IOrderService
    {
        Task<ApiResult<CheckoutResponse>> CheckoutAsync(CheckoutRequest request, Guid userId);
        Task<ApiResult<OrderDetailDto>> PlaceOrderAsync(CreateOrderRequest request, Guid userId);
        Task<ApiResult<OrderDetailDto>> GetByIdAsync(int id);
        Task<ApiResult<PagedResult<OrderSummaryResponse>>> GetPagedOrdersAsync(OrderFilterRequest request);
        Task<ApiResult<PagedResult<OrderSummaryResponse>>> GetMyOrdersAsync(Guid userId, OrderFilterRequest request);
        Task<ApiResult<bool>> CancelOrderAsync(int id, CancelOrderRequest request);
        Task<ApiResult<bool>> CompleteOrderAsync(int id);
        Task<ApiResult<bool>> ConfirmOrderAsync(int id);
    }
}
```

- [ ] **Bước 2: Build để xác nhận interface compile (sẽ fail ở OrderService chưa implement)**

```bash
dotnet build PBL3.sln
```

Expected: lỗi "OrderService does not implement ConfirmOrderAsync" — đây là dấu hiệu đúng, chuyển sang Task 2.

---

### Task 2: OrderService — implement ConfirmOrderAsync

**Files:**
- Modify: `src/Service/Orders/OrderService.cs`

- [ ] **Bước 1: Thêm method sau CompleteOrderAsync (dòng ~586)**

Tìm cuối method `CompleteOrderAsync` trong `src/Service/Orders/OrderService.cs` (khoảng dòng 586), thêm method mới ngay sau:

```csharp
public async Task<ApiResult<bool>> ConfirmOrderAsync(int id)
{
    var order = await _orderRepo.GetByIdAsync(id);
    if (order == null)
        return ApiResult<bool>.Fail("Không tìm thấy đơn hàng.");

    if (order.Status != 0)
        return ApiResult<bool>.Fail("Chỉ có thể duyệt đơn hàng đang ở trạng thái 'Chờ duyệt'.");

    order.Status = 1; // Confirmed
    await _unitOfWork.SaveChangesAsync();
    return ApiResult<bool>.Ok(true, "Đã duyệt đơn hàng thành công.");
}
```

- [ ] **Bước 2: Build**

```bash
dotnet build PBL3.sln
```

Expected: build thành công, 0 errors.

- [ ] **Bước 3: Commit**

```bash
git add src/Service/Orders/IOrderService.cs src/Service/Orders/OrderService.cs
git commit -m "feat(orders): add ConfirmOrderAsync service method (Pending -> Confirmed)"
```

---

### Task 3: OrdersController — endpoint PUT /{id}/confirm

**Files:**
- Modify: `src/API/Controllers/OrdersController.cs`

- [ ] **Bước 1: Thêm action sau CompleteOrder (dòng ~116)**

Tìm cuối method `CompleteOrder` trong `src/API/Controllers/OrdersController.cs`, thêm ngay sau (trước dấu `}` đóng class):

```csharp
[HttpPut("{id}/confirm")]
[Authorize(Roles = "Admin, Employee")]
public async Task<IActionResult> ConfirmOrder(int id)
{
    try
    {
        var result = await _orderService.ConfirmOrderAsync(id);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
    catch (Exception ex)
    {
        return BadRequest(ApiResult<bool>.Fail(ex.Message));
    }
}
```

- [ ] **Bước 2: Build**

```bash
dotnet build PBL3.sln
```

Expected: build thành công, 0 errors.

- [ ] **Bước 3: Commit**

```bash
git add src/API/Controllers/OrdersController.cs
git commit -m "feat(api): add PUT /api/orders/{id}/confirm endpoint for Admin/Employee"
```

---

### Task 4: IOrderClientService + OrderClientService

**Files:**
- Modify: `src/Client/Services/Orders/IOrderClientService.cs`
- Modify: `src/Client/Services/Orders/OrderClientService.cs`

- [ ] **Bước 1: Thêm vào interface**

Mở `src/Client/Services/Orders/IOrderClientService.cs`, thêm dòng cuối interface:

```csharp
Task<ApiResult<bool>> ConfirmOrderAsync(int id);
```

File sau khi sửa:
```csharp
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Sale;

namespace Client.Services.Orders
{
    public interface IOrderClientService
    {
        Task<ApiResult<OrderDetailDto>> GetByIdAsync(int id);
        Task<PagedResult<OrderSummaryResponse>> GetPagedOrdersAsync(OrderFilterRequest request);
        Task<ApiResult<PagedResult<OrderSummaryResponse>>> GetMyOrdersAsync(OrderFilterRequest request);
        Task<ApiResult<bool>> CancelOrderAsync(int id, string cancelReason);
        Task<ApiResult<CheckoutResponse>> CheckoutAsync(CheckoutRequest request);
        Task<ApiResult<bool>> CompleteOrderAsync(int id);
        Task<ApiResult<bool>> ConfirmOrderAsync(int id);
    }
}
```

- [ ] **Bước 2: Implement trong OrderClientService**

Mở `src/Client/Services/Orders/OrderClientService.cs`, thêm method sau `CompleteOrderAsync` (dòng ~82):

```csharp
public async Task<ApiResult<bool>> ConfirmOrderAsync(int id)
{
    var response = await _httpClient.PutAsJsonAsync($"/api/orders/{id}/confirm", new { });

    if (response.IsSuccessStatusCode)
    {
        var result = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
        return result ?? ApiResult<bool>.Fail("Không nhận được phản hồi từ máy chủ");
    }

    var errorResult = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
    return errorResult ?? ApiResult<bool>.Fail($"Lỗi HTTP: {response.StatusCode}");
}
```

- [ ] **Bước 3: Build**

```bash
dotnet build PBL3.sln
```

Expected: build thành công, 0 errors.

- [ ] **Bước 4: Commit**

```bash
git add src/Client/Services/Orders/IOrderClientService.cs src/Client/Services/Orders/OrderClientService.cs
git commit -m "feat(client): add ConfirmOrderAsync to order client service"
```

---

### Task 5: OrderList.razor — thêm nút Duyệt

**Files:**
- Modify: `src/Client/Pages/Orders/OrderList.razor`

- [ ] **Bước 1: Thêm nút Duyệt vào cột Hành động**

Trong `src/Client/Pages/Orders/OrderList.razor`, tìm khối `@if (context.Status == 0 || context.Status == 1)` trong `<MudTd DataLabel="Hành động">` (khoảng dòng 73). Thêm nút Duyệt **trước** nút Hủy, nhưng chỉ hiện khi `context.Status == 0`:

```razor
<MudTd DataLabel="Hành động" Style="text-align: right;">
    <MudTooltip Text="Xem chi tiết">
        <MudIconButton Icon="@Icons.Material.Filled.Visibility" Color="Color.Info" Size="Size.Small" OnClick="@(() => ViewDetails(context.Id))" />
    </MudTooltip>
    @if (context.Status == 0)
    {
        <MudTooltip Text="Duyệt đơn">
            <MudIconButton Icon="@Icons.Material.Filled.CheckCircle" Color="Color.Success" Size="Size.Small" OnClick="@(() => ConfirmOrder(context.Id))" />
        </MudTooltip>
    }
    @if (context.Status == 0 || context.Status == 1)
    {
        <MudTooltip Text="Hủy đơn">
            <MudIconButton Icon="@Icons.Material.Filled.Cancel" Color="Color.Error" Size="Size.Small" OnClick="@(() => ConfirmCancelOrder(context.Id))" />
        </MudTooltip>
    }
</MudTd>
```

- [ ] **Bước 2: Thêm method ConfirmOrder vào @code**

Trong `src/Client/Pages/Orders/OrderList.razor`, tìm khối `@code {`, thêm method `ConfirmOrder` sau `ConfirmCancelOrder`:

```csharp
private async Task ConfirmOrder(int orderId)
{
    var apiResult = await OrderService.ConfirmOrderAsync(orderId);
    if (apiResult.Success)
    {
        Snackbar.Add("Đã duyệt đơn hàng thành công.", Severity.Success);
        await LoadDataAsync();
    }
    else
    {
        Snackbar.Add(apiResult.Message ?? "Lỗi khi duyệt đơn hàng.", Severity.Error);
    }
}
```

- [ ] **Bước 3: Build**

```bash
dotnet build PBL3.sln
```

Expected: build thành công, 0 errors.

- [ ] **Bước 4: Commit**

```bash
git add src/Client/Pages/Orders/OrderList.razor
git commit -m "feat(ui): add confirm order button to order list for Pending orders"
```

---

### Task 6: OrderDetail.razor — thêm nút Duyệt

**Files:**
- Modify: `src/Client/Pages/Orders/OrderDetail.razor`

- [ ] **Bước 1: Thêm nút Duyệt trong khu vực action header**

Trong `src/Client/Pages/Orders/OrderDetail.razor`, tìm khối `<div class="d-flex gap-2">` (khoảng dòng 26). Thêm nút Duyệt **trước** nút Hủy:

```razor
<div class="d-flex gap-2">
    @if (_order.Status == 2) // Shipping
    {
        <MudButton Variant="Variant.Filled" Color="Color.Success" Size="Size.Small" OnClick="ConfirmCompleteOrder">
            Xác nhận đã giao
        </MudButton>
    }
    @if (_order.Status == 0) // Pending — chờ duyệt
    {
        <MudButton Variant="Variant.Filled" Color="Color.Success" Size="Size.Small" OnClick="ConfirmOrderApprove">
            Duyệt đơn
        </MudButton>
    }
    @if (_order.Status == 0 || _order.Status == 1) // Pending or Confirmed
    {
        <MudButton Variant="Variant.Filled" Color="Color.Error" Size="Size.Small" OnClick="ConfirmCancelOrder">
            Hủy đơn
        </MudButton>
    }
    <MudChip T="string" Size="Size.Small" Color="@GetStatusColor(_order.Status)" Variant="Variant.Filled">
        @GetStatusText(_order.Status)
    </MudChip>
</div>
```

- [ ] **Bước 2: Thêm method ConfirmOrderApprove vào @code**

Trong `src/Client/Pages/Orders/OrderDetail.razor`, thêm method sau `ConfirmCompleteOrder` (khoảng dòng 279):

```csharp
private async Task ConfirmOrderApprove()
{
    var result = await OrderService.ConfirmOrderAsync(Id);
    if (result.Success)
    {
        Snackbar.Add("Đã duyệt đơn hàng thành công.", Severity.Success);
        await LoadOrder();
    }
    else
    {
        Snackbar.Add(result.Message ?? "Lỗi khi duyệt đơn hàng.", Severity.Error);
    }
}
```

- [ ] **Bước 3: Build**

```bash
dotnet build PBL3.sln
```

Expected: build thành công, 0 errors.

- [ ] **Bước 4: Commit**

```bash
git add src/Client/Pages/Orders/OrderDetail.razor
git commit -m "feat(ui): add confirm order button to order detail page for Pending orders"
```

---

## FEATURE B — Validate Serial Real-time

### Task 7: IInventoryExportService + InventoryExportService — ValidateSerialAsync

**Files:**
- Modify: `src/Service/Inventory/IInventoryExportService.cs`
- Modify: `src/Service/Inventory/InventoryExportService.cs`

- [ ] **Bước 1: Thêm vào interface**

Mở `src/Service/Inventory/IInventoryExportService.cs`, thêm method:

```csharp
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace PBL3.Service.Inventory
{
    public interface IInventoryExportService
    {
        Task<ApiResult<bool>> ExportOrderAsync(ExportOrderRequest request);
        Task<ApiResult<bool>> ValidateSerialAsync(string serialNo, int variantId);
    }
}
```

- [ ] **Bước 2: Implement ValidateSerialAsync trong InventoryExportService**

Mở `src/Service/Inventory/InventoryExportService.cs`, thêm method sau `ExportOrderAsync` (dòng ~141, trước dấu `}` đóng class):

```csharp
public async Task<ApiResult<bool>> ValidateSerialAsync(string serialNo, int variantId)
{
    var serial = await _serialRepo.GetBySerialNumberAsync(serialNo);

    if (serial == null)
        return ApiResult<bool>.Fail($"Mã Serial '{serialNo}' không tồn tại trong hệ thống.");

    if (serial.VariantId != variantId)
        return ApiResult<bool>.Fail($"Mã Serial '{serialNo}' không thuộc sản phẩm yêu cầu.");

    if (serial.Status != (byte)PBL3.Shared.Enums.SerialStatus.Available)
        return ApiResult<bool>.Fail($"Mã Serial '{serialNo}' không ở trạng thái Available (có thể đã bán hoặc hỏng).");

    return ApiResult<bool>.Ok(true);
}
```

- [ ] **Bước 3: Build**

```bash
dotnet build PBL3.sln
```

Expected: build thành công, 0 errors.

- [ ] **Bước 4: Commit**

```bash
git add src/Service/Inventory/IInventoryExportService.cs src/Service/Inventory/InventoryExportService.cs
git commit -m "feat(inventory): add ValidateSerialAsync service method for real-time serial check"
```

---

### Task 8: InventoryController — GET /api/inventory/serials/validate

**Files:**
- Modify: `src/API/Controllers/InventoryController.cs`

- [ ] **Bước 1: Thêm action ValidateSerial**

Mở `src/API/Controllers/InventoryController.cs`, thêm action sau `ExportOrder` (trước dấu `}` đóng class):

```csharp
[HttpGet("serials/validate")]
[ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> ValidateSerial([FromQuery] string serialNo, [FromQuery] int variantId)
{
    if (string.IsNullOrWhiteSpace(serialNo) || variantId <= 0)
        return BadRequest(ApiResult<bool>.Fail("Tham số không hợp lệ."));

    var result = await _inventoryExportService.ValidateSerialAsync(serialNo, variantId);

    if (!result.Success)
        return BadRequest(result);

    return Ok(result);
}
```

- [ ] **Bước 2: Build**

```bash
dotnet build PBL3.sln
```

Expected: build thành công, 0 errors.

- [ ] **Bước 3: Commit**

```bash
git add src/API/Controllers/InventoryController.cs
git commit -m "feat(api): add GET /api/inventory/serials/validate endpoint"
```

---

### Task 9: IInventoryExportClientService + InventoryExportClientService

**Files:**
- Modify: `src/Client/Services/Inventory/IInventoryExportClientService.cs`
- Modify: `src/Client/Services/Inventory/InventoryExportClientService.cs`

- [ ] **Bước 1: Thêm vào interface**

Mở `src/Client/Services/Inventory/IInventoryExportClientService.cs`:

```csharp
using System.Threading.Tasks;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace Client.Services.Inventory
{
    public interface IInventoryExportClientService
    {
        Task<ApiResult<bool>> ExportOrderAsync(ExportOrderRequest request);
        Task<ApiResult<bool>> ValidateSerialAsync(string serialNo, int variantId);
    }
}
```

- [ ] **Bước 2: Implement trong InventoryExportClientService**

Mở `src/Client/Services/Inventory/InventoryExportClientService.cs`, thêm method sau `ExportOrderAsync`:

```csharp
public async Task<ApiResult<bool>> ValidateSerialAsync(string serialNo, int variantId)
{
    var url = $"/api/inventory/serials/validate?serialNo={Uri.EscapeDataString(serialNo)}&variantId={variantId}";
    var response = await _httpClient.GetAsync(url);

    if (response.IsSuccessStatusCode)
    {
        return await response.Content.ReadFromJsonAsync<ApiResult<bool>>()
               ?? ApiResult<bool>.Fail("Không nhận được dữ liệu hợp lệ từ server.");
    }

    var errorResult = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
    return errorResult ?? ApiResult<bool>.Fail($"Lỗi HTTP: {response.StatusCode}");
}
```

**Lý do dùng `GetAsync` thay vì `GetFromJsonAsync`:** `GetFromJsonAsync` ném exception khi status 4xx, không đọc được body lỗi. Dùng `GetAsync` giống pattern của `ExportOrderAsync` để đọc được thông báo lỗi tiếng Việt từ server.

- [ ] **Bước 3: Build**

```bash
dotnet build PBL3.sln
```

Expected: build thành công, 0 errors.

- [ ] **Bước 4: Commit**

```bash
git add src/Client/Services/Inventory/IInventoryExportClientService.cs src/Client/Services/Inventory/InventoryExportClientService.cs
git commit -m "feat(client): add ValidateSerialAsync to inventory export client service"
```

---

### Task 10: ExportOrder.razor — validate serial real-time

**Files:**
- Modify: `src/Client/Pages/Inventory/ExportOrder.razor`

- [ ] **Bước 1: Sửa OnSerialKeyUp để gọi ValidateSerialAsync trước khi thêm**

Tìm method `OnSerialKeyUp` trong `src/Client/Pages/Inventory/ExportOrder.razor` (khoảng dòng 288). Thay toàn bộ method bằng:

```csharp
private async Task OnSerialKeyUp(KeyboardEventArgs e)
{
    if (e.Key != "Enter") return;
    if (_activeDetail == null) return;

    var code = _scannedCode?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(code)) return;

    if (_activeDetail.ScannedSerials.Count >= _activeDetail.RequiredQuantity)
    {
        Snackbar.Add("Đã xuất đủ số lượng cho sản phẩm này!", Severity.Warning);
        _scannedCode = string.Empty;
        return;
    }

    if (_activeDetail.ScannedSerials.Contains(code) || _details.Any(d => d != _activeDetail && d.ScannedSerials.Contains(code)))
    {
        Snackbar.Add("Mã Serial này đã được quét trong đơn này!", Severity.Error);
        _scannedCode = string.Empty;
        return;
    }

    // Validate real-time với backend
    _isScanning = true;
    StateHasChanged();

    var validateResult = await InventoryExportService.ValidateSerialAsync(code, _activeDetail.VariantId);

    _isScanning = false;

    if (!validateResult.Success)
    {
        Snackbar.Add(validateResult.Message ?? "Mã Serial không hợp lệ.", Severity.Error);
        _scannedCode = string.Empty;
        StateHasChanged();
        if (_serialInput != null)
        {
            await Task.Delay(50);
            await _serialInput.FocusAsync();
        }
        return;
    }

    _activeDetail.ScannedSerials.Add(code);
    _scannedCode = string.Empty;

    if (_activeDetail.ScannedSerials.Count >= _activeDetail.RequiredQuantity)
        Snackbar.Add($"Đã quét đủ {_activeDetail.RequiredQuantity} mã cho {_activeDetail.ProductName}!", Severity.Success);

    StateHasChanged();
    if (_serialInput != null)
    {
        await Task.Delay(50);
        await _serialInput.FocusAsync();
    }
}
```

- [ ] **Bước 2: Build**

```bash
dotnet build PBL3.sln
```

Expected: build thành công, 0 errors.

- [ ] **Bước 3: Commit**

```bash
git add src/Client/Pages/Inventory/ExportOrder.razor
git commit -m "feat(ui): validate serial real-time on Enter keypress before adding to export list"
```

---

## Kiểm tra thủ công sau khi hoàn thành

### Feature A — Duyệt đơn
1. Khởi động `dotnet run --project src/API/API.csproj` + `dotnet run --project src/Client/Client.csproj`
2. Đăng nhập Admin/Employee
3. Vào `/orders` — đơn hàng Pending (status=0) phải hiện icon ✓ xanh bên cạnh icon ❌ đỏ
4. Click icon ✓ → snackbar "Đã duyệt đơn hàng thành công." → đơn chuyển sang "Chờ xuất kho"
5. Vào `/orders/{id}` của đơn Pending → nút "Duyệt đơn" hiển thị cạnh "Hủy đơn"
6. Click "Duyệt đơn" → snackbar thành công → status chip cập nhật thành "Chờ xuất kho"
7. Thử duyệt đơn đã Confirmed → API trả về lỗi → snackbar hiện thông báo lỗi tiếng Việt

### Feature B — Serial validate
1. Vào `/inventory/export/{orderId}` với đơn hàng Confirmed
2. Chọn sản phẩm → click "Nhập mã"
3. Nhập mã serial **không tồn tại** → Enter → snackbar đỏ "Mã Serial 'xxx' không tồn tại trong hệ thống." → mã **không được thêm**
4. Nhập mã serial **đúng tồn tại nhưng sai variant** → Enter → snackbar đỏ "Mã Serial 'xxx' không thuộc sản phẩm yêu cầu." → mã không được thêm
5. Nhập mã serial **đã bán (Sold)** → Enter → snackbar đỏ "Mã Serial 'xxx' không ở trạng thái Available..." → mã không được thêm
6. Nhập mã serial **hợp lệ** → Enter → icon spinner hiện trong lúc gọi API → mã được thêm vào danh sách
