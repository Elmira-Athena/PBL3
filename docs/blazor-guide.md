# Blazor WebAssembly — Hướng dẫn từ dự án PBL3

> **Mục tiêu:** Giải thích cấu trúc project Blazor WebAssembly, syntax file `.razor`, vòng đời component, và các pattern phổ biến — tất cả đều dựa trên code thực tế của project này.

---

## 1. Blazor là gì?

**Blazor** là framework của Microsoft để xây dựng web UI bằng C# thay vì JavaScript. Có 2 kiểu chạy:

| Kiểu | Mô tả | Project này |
|---|---|---|
| **Blazor WebAssembly (WASM)** | Chạy C# trực tiếp trên trình duyệt thông qua WebAssembly | ✅ |
| **Blazor Server** | C# chạy trên server, giao tiếp qua SignalR | ❌ |

Trong Blazor WASM, **toàn bộ ứng dụng** (DLL, runtime .NET) được tải về trình duyệt và chạy client-side — không cần server để render.

---

## 2. Cấu trúc project Client

```
src/Client/
│
├── Program.cs              ← Điểm vào (entry point), đăng ký DI
├── App.razor               ← Root component, cấu hình Router
├── _Imports.razor          ← Global @using cho toàn bộ .razor files
├── Client.csproj           ← Cấu hình project, NuGet packages
│
├── Layout/                 ← Layout components (shell của trang)
│   ├── StorefrontLayout.razor
│   ├── AdminLayout.razor
│   ├── EmployeeLayout.razor
│   ├── LoginLayout.razor
│   ├── AdminNavMenu.razor
│   └── EmployeeNavMenu.razor
│
├── Pages/                  ← Các trang (có @page route)
│   ├── Home.razor
│   ├── NotFound.razor
│   ├── Auth/
│   ├── Storefront/         ← ProductDetail, Cart, Checkout, ...
│   ├── Admin/
│   ├── Pos/
│   └── ...
│
├── Shared/                 ← Reusable components (không có @page)
│   └── Components/
│       ├── Storefront/     ← ProductCard, ComparisonTray, ...
│       └── ServiceTickets/
│
├── Services/               ← Client services (gọi HTTP API)
│   ├── Product/
│   │   ├── IProductClientService.cs
│   │   └── ProductClientService.cs
│   ├── Cart/
│   ├── Auth/
│   └── ...
│
├── Auth/                   ← Xác thực JWT
│   ├── JwtAuthenticationStateProvider.cs
│   ├── AuthHeaderHandler.cs
│   └── RedirectToLogin.razor
│
└── wwwroot/                ← Static files (CSS, images, appsettings.json)
    ├── index.html          ← HTML shell duy nhất của SPA
    ├── css/
    ├── images/
    └── appsettings.json    ← Cấu hình runtime (ApiBaseUrl, ...)
```

---

## 3. Điểm vào — `Program.cs`

```csharp
// Tạo WebAssembly host builder
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Đăng ký root component vào element #app trong index.html
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after"); // cho <PageTitle>

// ===== Đăng ký services (Dependency Injection) =====
builder.Services.AddBlazoredLocalStorage();       // lưu JWT vào localStorage
builder.Services.AddAuthorizationCore();           // hệ thống phân quyền
builder.Services.AddMudServices();                 // MudBlazor UI library

// Đăng ký HttpClient có gắn JWT header
builder.Services.AddHttpClient("HushStoreAPI", client => {
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<AuthHeaderHandler>();

// Đăng ký các client services theo interface
builder.Services.AddScoped<IProductClientService, ProductClientService>();
builder.Services.AddScoped<ICartClientService, CartClientService>();
// ...

await builder.Build().RunAsync();
```

> **Điểm khác biệt:** Không có `Startup.cs`, không có middleware pipeline như ASP.NET MVC. Chỉ có DI container và root component.

---

## 4. Root component — `App.razor`

```razor
<MudThemeProvider Theme="_theme" />   <!-- Provider của MudBlazor (theme, dialog, snackbar) -->
<MudDialogProvider />
<MudSnackbarProvider />

<CascadingAuthenticationState>       <!-- Đẩy auth state xuống toàn bộ cây component -->
    <Router AppAssembly="@typeof(App).Assembly" NotFoundPage="typeof(Pages.NotFound)">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="@routeData" DefaultLayout="@typeof(StorefrontLayout)">
                <NotAuthorized>
                    @if (context.User.Identity?.IsAuthenticated == true)
                    {
                        <!-- Đã login nhưng không có quyền -->
                        <MudAlert Severity="Severity.Error">Truy cập bị từ chối!</MudAlert>
                    }
                    else
                    {
                        <Client.Auth.RedirectToLogin />   <!-- Chưa login → redirect -->
                    }
                </NotAuthorized>
            </AuthorizeRouteView>
        </Found>
    </Router>
</CascadingAuthenticationState>

@code {
    private MudTheme _theme = new MudTheme { ... };   // Cấu hình màu sắc, font
}
```

**Luồng routing:**
1. URL thay đổi → `Router` tìm component có `@page` khớp
2. `AuthorizeRouteView` kiểm tra quyền truy cập
3. Nếu được phép → render component với layout tương ứng
4. Nếu không → hiện `<NotAuthorized>`

---

## 5. Cấu trúc file `.razor` — Anatomy

Một file `.razor` có thể có **tối đa 3 phần** theo thứ tự:

```
┌──────────────────────────────────┐
│  1. DIRECTIVES (chỉ thị đầu file) │
├──────────────────────────────────┤
│  2. HTML TEMPLATE (markup)        │
├──────────────────────────────────┤
│  3. @code { ... } (C# logic)     │
└──────────────────────────────────┘
```

### Ví dụ đầy đủ — `Cart.razor`

```razor
@* ====== PHẦN 1: DIRECTIVES ====== *@
@page "/cart"                                    @* ← Route URL *@
@using PBL3.Shared.DTOs.Cart                     @* ← Import namespace *@
@inject NavigationManager NavigationManager      @* ← Inject DI service *@
@inject ISnackbar Snackbar
@inject Client.Services.Cart.ICartClientService CartClientService

@* ====== PHẦN 2: HTML TEMPLATE ====== *@
<PageTitle>Giỏ hàng - HushStore</PageTitle>

@if (_isLoading)
{
    <MudSkeleton />   @* Loading skeleton *@
}
else if (_cartData == null || !_cartData.Items.Any())
{
    <p>Giỏ hàng trống</p>
}
else
{
    @foreach (var item in _cartData.Items)
    {
        <div @onclick="() => NavigationManager.NavigateTo($"/product/{item.ProductSlug}")">
            @item.ProductName
        </div>
    }
}

@* ====== PHẦN 3: C# CODE ====== *@
@code {
    private bool _isLoading = true;
    private CartResponse _cartData = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadCartAsync();
    }

    private async Task LoadCartAsync()
    {
        _isLoading = true;
        var result = await CartClientService.GetMyCartAsync();
        if (result.Success) _cartData = result.Data!;
        _isLoading = false;
    }
}
```

---

## 6. Directives — Chi tiết

### 6.1 `@page` — Định nghĩa route

```razor
@page "/cart"                    // route tĩnh
@page "/product/{slug}"          // route có tham số → slug là [Parameter]
@page "/product/{id:int}"        // route với type constraint (chỉ nhận số nguyên)
```

### 6.2 `@layout` — Chỉ định layout

```razor
@layout StorefrontLayout    // Ghi đè DefaultLayout trong App.razor
@layout AdminLayout
```

> Nếu không có `@layout`, component sẽ dùng `DefaultLayout` khai báo trong `AuthorizeRouteView`.

### 6.3 `@inject` — Dependency Injection

```razor
@inject NavigationManager Navigation        // điều hướng URL
@inject ISnackbar Snackbar                  // toast notifications (MudBlazor)
@inject IDialogService DialogService        // mở dialog/modal
@inject IAuthClientService AuthService      // service tự viết
@inject AuthenticationStateProvider AuthStateProvider  // lấy thông tin user đăng nhập
@inject IJSRuntime JS                       // gọi JavaScript
```

### 6.4 `@using` — Import namespace

```razor
@using PBL3.Shared.DTOs.Cart         // import trong file
```

Để không phải viết lặp, tất cả được đặt trong `_Imports.razor` — file này tự động áp dụng cho **toàn bộ project**.

### 6.5 `@inherits` — Kế thừa class base

```razor
@inherits LayoutComponentBase    // bắt buộc cho Layout components, cung cấp @Body
```

### 6.6 `@implements` — Implement interface

```razor
@implements IDisposable    // trong StorefrontLayout để unsubscribe event
```

### 6.7 `@attribute` — Gắn attribute

```razor
@attribute [Authorize]                    // yêu cầu đăng nhập
@attribute [Authorize(Roles = "Admin")]   // yêu cầu role Admin
```

---

## 7. Razor Syntax — Viết C# trong HTML

### 7.1 Biểu thức inline `@`

```razor
<h1>@_product.Name</h1>
<span>@DateTime.Now.Year</span>
<p>@(_cartData?.Items?.Count ?? 0) sản phẩm</p>   <!-- dùng () khi biểu thức phức tạp -->
```

### 7.2 String interpolation

```razor
<img src="@product.ThumbnailUrl" alt="@product.Name" />
<a href="/product/@item.ProductSlug">...</a>
<!-- Với $"..." phải bọc trong @() -->
<div style="@($"color:{color}; width:{width}px")">...</div>
```

### 7.3 Câu lệnh điều kiện `@if`

```razor
@if (_isLoading)
{
    <MudProgressLinear Indeterminate="true" />
}
else if (_product == null)
{
    <p>Không tìm thấy sản phẩm</p>
}
else
{
    <h1>@_product.Name</h1>
}
```

### 7.4 Vòng lặp `@foreach` / `@for`

```razor
@foreach (var item in _cartData.Items)
{
    <div>@item.ProductName - @item.UnitPrice.ToString("N0") đ</div>
}

@for (int i = 0; i < 4; i++)
{
    <MudSkeleton Width="80px" Height="80px" />   <!-- skeleton loading -->
}
```

### 7.5 Comment

```razor
@* Đây là comment Razor — không xuất ra HTML *@
<!-- Đây là comment HTML — vẫn xuất ra HTML -->
```

### 7.6 `@((MarkupString)html)` — Render HTML raw

```razor
<!-- Dùng khi description chứa HTML (rich text) -->
@((MarkupString)_product.Description)
```

> ⚠️ **Cẩn thận XSS:** Chỉ dùng khi dữ liệu đến từ nguồn tin cậy.

---

## 8. Event Handling — Xử lý sự kiện

### 8.1 Cú pháp cơ bản

```razor
<!-- Gọi method -->
<button @onclick="LoadCartAsync">Tải lại</button>

<!-- Lambda expression -->
<button @onclick="() => NavigationManager.NavigateTo("/")">Về trang chủ</button>

<!-- Lambda với tham số từ vòng lặp (phải capture biến) -->
@foreach (var item in items)
{
    var captured = item;   // ← quan trọng! tránh closure bug
    <div @onclick="() => OnItemClick(captured)">@captured.Name</div>
}
```

### 8.2 Async event handler

```razor
<button @onclick="AddToCartAsync">Thêm vào giỏ</button>
<MudIconButton OnClick="() => UpdateQuantity(item, -1)" />

@code {
    private async Task AddToCartAsync()
    {
        _isProcessing = true;
        StateHasChanged();   // ← cập nhật UI ngay lập tức
        var result = await CartClientService.AddToCartAsync(...);
        _isProcessing = false;
    }
}
```

### 8.3 Keyboard / Mouse events

```razor
<input @onkeydown="OnSearchKeyDown" />

@code {
    private void OnSearchKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter") PerformSearch();
        if (args.Key == "Escape") _showSuggestions = false;
    }
}
```

### 8.4 Ngăn chặn hành vi mặc định

```razor
<div @onmousedown:preventDefault>...</div>   <!-- ngăn input mất focus khi click suggestion -->
```

---

## 9. Data Binding — Liên kết dữ liệu

### 9.1 One-way binding (đọc)

```razor
<h1>@_product.Name</h1>   <!-- C# → HTML, chỉ đọc -->
<input value="@_searchKeyword" />   <!-- one-way, không cập nhật _searchKeyword khi gõ -->
```

### 9.2 Two-way binding `@bind`

```razor
<!-- @bind tự động sinh ra value + onchange handler -->
<input @bind="_searchKeyword" />

<!-- Với component MudBlazor -->
<MudTextField @bind-Value="_searchKeyword" />
<MudNumericField @bind-Value="_quantity" Min="1" Max="99" />
<MudDrawer @bind-Open="_drawerOpen" />   <!-- mở/đóng drawer -->
<MudThemeProvider @bind-IsDarkMode="_isDarkMode" />
```

### 9.3 Binding với event cụ thể

```razor
<input @bind="_searchKeyword" @bind:event="oninput" />   <!-- cập nhật theo từng ký tự -->
```

---

## 10. Component Parameters

### 10.1 Nhận tham số từ parent

```razor
@* ProductCard.razor *@
@code {
    [Parameter]
    public ProductCardResponse Product { get; set; } = default!;

    [Parameter]
    public EventCallback<int> OnAddToCart { get; set; }   // callback về parent
}
```

### 10.2 Truyền tham số từ parent

```razor
<ProductCard Product="@p" OnAddToCart="HandleAddToCart" />
```

### 10.3 Route parameter là [Parameter]

```razor
@page "/product/{Slug}"

@code {
    [Parameter]
    public string Slug { get; set; } = string.Empty;   // khớp với {Slug} trong route

    protected override async Task OnParametersSetAsync()
    {
        // Gọi khi Slug thay đổi (navigate sang sản phẩm khác)
        await LoadDataAsync();
    }
}
```

### 10.4 `[CascadingParameter]` — Nhận từ ancestor

```razor
@code {
    [CascadingParameter]
    private Task<AuthenticationState>? AuthState { get; set; }
}
```

---

## 11. Lifecycle Methods — Vòng đời component

```
Component khởi tạo
       │
       ▼
  SetParametersAsync()          ← Nhận parameters từ parent (tự động)
       │
       ▼
  OnInitialized()               ← Chạy 1 lần, đồng bộ
       │
       ▼
  OnInitializedAsync()          ← Chạy 1 lần, bất đồng bộ (gọi API tại đây)
       │
       ▼
  OnParametersSet()             ← Mỗi khi parameters thay đổi
       │
       ▼
  OnParametersSetAsync()        ← Async version (dùng cho route param như Slug)
       │
       ▼
  OnAfterRender(firstRender)    ← Sau khi render xong DOM
       │
       ▼
  OnAfterRenderAsync(...)       ← Dùng khi cần JS interop sau render
       │
       ▼
  Dispose() / DisposeAsync()    ← Cleanup (nếu implements IDisposable)
```

### Ví dụ thực tế

```razor
@code {
    // Khởi tạo một lần — gọi API, load dữ liệu
    protected override async Task OnInitializedAsync()
    {
        var result = await StorefrontService.GetActiveCategoriesAsync();
        if (result.Success) _menuCategories = result.Data!;
    }

    // Gọi khi route param (Slug) thay đổi
    protected override async Task OnParametersSetAsync()
    {
        await LoadDataAsync();
    }

    // Subscribe event trong OnInitialized (không async)
    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
    }

    // Cleanup khi component bị hủy
    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        _suggestCts?.Cancel();
        _suggestCts?.Dispose();
    }
}
```

---

## 12. `StateHasChanged()` — Cập nhật UI thủ công

Blazor tự động re-render sau event handler. Nhưng đôi khi cần trigger thủ công:

```csharp
// Khi thay đổi state trong async method sau await
_isProcessing = true;
StateHasChanged();      // ← UI cập nhật ngay (hiện spinner)
await CartClientService.AddToCartAsync(...);
_isProcessing = false;  // ← UI sẽ tự update sau khi method kết thúc

// Khi cập nhật từ event bên ngoài (background thread)
private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
{
    _searchKeyword = string.Empty;
    InvokeAsync(StateHasChanged);   // ← phải dùng InvokeAsync từ non-UI thread
}
```

---

## 13. Layout Components

Layout bọc ngoài tất cả các page. Vị trí `@Body` là nơi page được render.

```razor
@* AdminLayout.razor *@
@inherits LayoutComponentBase    @* ← bắt buộc *@
@inject IAuthClientService AuthService

<MudLayout>
    <MudAppBar>
        <MudIconButton OnClick="ToggleDrawer" />
    </MudAppBar>

    <MudDrawer @bind-Open="_drawerOpen">
        <AdminNavMenu />         @* ← component điều hướng *@
    </MudDrawer>

    <MudMainContent>
        @Body                    @* ← PAGE được inject vào đây *@
    </MudMainContent>
</MudLayout>

@code {
    private bool _drawerOpen = true;
    private void ToggleDrawer() => _drawerOpen = !_drawerOpen;   // expression body
}
```

---

## 14. Authorization & Authentication

### 14.1 Bảo vệ page bằng attribute

```razor
@attribute [Authorize]                     // Bất kỳ user đã đăng nhập
@attribute [Authorize(Roles = "Admin")]    // Chỉ Admin
@attribute [Authorize(Roles = "Admin,Employee")]  // Admin hoặc Employee
```

### 14.2 Hiện/ẩn UI theo role — `<AuthorizeView>`

```razor
<AuthorizeView>
    <Authorized>
        <!-- Hiện khi đã đăng nhập -->
        <MudMenuItem>@context.User.Identity?.Name</MudMenuItem>
        <MudMenuItem OnClick="Logout">Đăng xuất</MudMenuItem>
    </Authorized>
    <NotAuthorized>
        <!-- Hiện khi chưa đăng nhập -->
        <MudButton Href="/login">Đăng nhập</MudButton>
    </NotAuthorized>
</AuthorizeView>

<!-- Theo role cụ thể -->
<AuthorizeView Roles="Admin">
    <Authorized>
        <MudButton Href="/pos">Giao diện Nhân viên</MudButton>
    </Authorized>
</AuthorizeView>
```

### 14.3 Kiểm tra auth trong code

```razor
@inject AuthenticationStateProvider AuthStateProvider

@code {
    private async Task AddToCartAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            Snackbar.Add("Vui lòng đăng nhập.", Severity.Warning);
            NavigationManager.NavigateTo($"/login?returnUrl=/product/{Slug}");
            return;
        }

        if (user.IsInRole("Admin"))
        {
            // xử lý riêng cho Admin
        }
    }
}
```

### 14.4 `JwtAuthenticationStateProvider` (custom)

Project này implement `AuthenticationStateProvider` riêng để:
1. Đọc JWT token từ `localStorage`
2. Parse claims từ token
3. Cung cấp `AuthenticationState` cho toàn bộ ứng dụng

```csharp
// Auth/JwtAuthenticationStateProvider.cs
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        // Parse JWT → ClaimsPrincipal → AuthenticationState
        ...
    }
}
```

---

## 15. Client Services — Gọi HTTP API

### Interface

```csharp
// Services/Product/IProductClientService.cs
public interface IProductClientService
{
    Task<ApiResult<PagedResult<ProductListDto>>> GetListAsync(ProductFilterRequest request);
    Task<ApiResult<ProductDetailDto>> GetByIdAsync(int id);
    Task<ApiResult<ProductDetailDto>> CreateAsync(CreateProductRequest request);
    Task<ApiResult<bool>> DeleteAsync(int id);
}
```

### Sử dụng trong component

```razor
@inject IProductClientService ProductService
@inject ISnackbar Snackbar

@code {
    private async Task LoadProductAsync(int id)
    {
        var result = await ProductService.GetByIdAsync(id);

        if (result.Success && result.Data != null)
        {
            _product = result.Data;
        }
        else
        {
            Snackbar.Add(result.Message ?? "Lỗi tải sản phẩm", Severity.Error);
        }
    }
}
```

### Pattern `ApiResult<T>`

```csharp
// Shared/DTOs/Common/ApiResult.cs
public class ApiResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public ApiErrorCode ErrorCode { get; set; }
}
```

---

## 16. Inline CSS trong `.razor`

Blazor thường dùng inline style vì không có scoped CSS tự động như Vue/Angular:

```razor
<!-- Inline style thông thường -->
<div style="font-size: 1.2rem; font-weight: 700; color: var(--hs-red);">
    @_product.Name
</div>

<!-- Conditional style với ternary -->
<div style="border: 2px solid @(isSelected ? "var(--hs-red)" : "var(--hs-border)");">

<!-- Dynamic style với $"..." -->
<MudChip Style="@($"border-radius: 8px; {(isActive ? "font-weight: 700;" : "")}")">

<!-- Global CSS trong <style> block (scoped to file) -->
<style>
    .pd-description { line-height: 1.8; }
    .pd-description p { margin-bottom: 1rem; }

    /* Media query phải escape @ thành @@ */
    @@media (min-width: 600px) {
        .cart-col-header { display: flex !important; }
    }
</style>
```

> **Lưu ý:** `@` trong CSS media query phải viết thành `@@` để Razor không nhầm với C# expression.

---

## 17. Dialog (Modal) — MudBlazor

### Mở dialog từ parent

```razor
@inject IDialogService DialogService

@code {
    private async Task OpenProfileDialog()
    {
        await DialogService.ShowAsync<UserProfileDialog>(
            string.Empty,   // title
            new DialogOptions
            {
                MaxWidth = MaxWidth.Small,
                FullWidth = true,
                CloseButton = true
            }
        );
    }
}
```

### Dialog component

```razor
@* UserProfileDialog.razor *@
@inject MudBlazor.IDialogService DialogService

<MudDialog>
    <TitleContent>Hồ sơ cá nhân</TitleContent>
    <DialogContent>
        <!-- nội dung form -->
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Hủy</MudButton>
        <MudButton Color="Color.Primary" OnClick="Submit">Lưu</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    private void Cancel() => MudDialog.Cancel();
    private void Submit() => MudDialog.Close(DialogResult.Ok(true));
}
```

---

## 18. `_Imports.razor` — Global Imports

File đặc biệt, áp dụng `@using` cho **tất cả** `.razor` files trong cùng thư mục và thư mục con:

```razor
@* _Imports.razor *@
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Authorization
@using Microsoft.JSInterop
@using Client
@using Client.Layout
@using Client.Services
@using MudBlazor
@using Blazored.FluentValidation
@using PBL3.Shared.DTOs.Products
@using PBL3.Shared.DTOs.Common
@* ... *@
```

> Không cần khai báo lại `@using MudBlazor` hay `@using PBL3.Shared.DTOs.Products` trong từng file `.razor`.

---

## 19. Tóm tắt — Quick Reference

| Syntax | Ý nghĩa |
|--------|---------|
| `@page "/path"` | Định nghĩa URL route cho page |
| `@layout AdminLayout` | Chỉ định layout wrapper |
| `@inject IService Svc` | Inject DI service |
| `@using Namespace` | Import namespace |
| `@inherits BaseClass` | Kế thừa class base |
| `@implements IInterface` | Implement interface |
| `@attribute [Authorize]` | Gắn attribute (phân quyền) |
| `@code { }` | Block C# logic |
| `@expression` | Render giá trị C# ra HTML |
| `@(complex expr)` | Biểu thức C# phức tạp |
| `@if / @else` | Điều kiện |
| `@foreach` | Vòng lặp |
| `@* comment *@` | Comment Razor |
| `@bind-Value` | Two-way data binding |
| `@onclick="Handler"` | Event binding |
| `@onclick:preventDefault` | Ngăn hành vi mặc định |
| `@@` | Ký tự `@` literal trong CSS/string |
| `[Parameter]` | Thuộc tính nhận từ parent component |
| `[CascadingParameter]` | Nhận từ ancestor component |

---

## 20. So sánh với các framework khác

| Khái niệm | React/Vue | Blazor |
|-----------|-----------|--------|
| Component file | `.jsx` / `.vue` | `.razor` |
| Template language | JSX / Vue template | Razor syntax |
| Logic language | JavaScript/TypeScript | C# |
| State management | useState / ref | fields trong `@code` |
| Two-way binding | `v-model` / controlled | `@bind` |
| Lifecycle | useEffect / mounted | `OnInitializedAsync` |
| HTTP calls | fetch / axios | `HttpClient` (C#) |
| Routing | React Router / Vue Router | Built-in `Router` component |
| DI / Services | Context API / Pinia | .NET DI Container |

---

*Tài liệu này được tạo tự động dựa trên phân tích codebase PBL3 — `src/Client/`*
