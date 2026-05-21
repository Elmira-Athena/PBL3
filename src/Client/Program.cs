using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using Blazored.LocalStorage;
using Blazored.FluentValidation;
using FluentValidation;
using Client;
using Client.Auth;
using Client.Services;
using Client.Services.Auth;
using Client.Services.Category;
using Client.Services.Customer;
using Client.Services.Banner;
using Client.Services.Employee;
using Client.Services.Manufacturer;
using Client.Services.Pos;
using Client.Services.Product;
using Client.Services.Supplier;
using Client.Services.Inventory;
using Client.Services.Voucher;
using Client.Services.Image;
using Client.Services.ServiceTickets;
using Client.Services.Analytics;
using Client.Services.Comparison;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ===== Authentication & Authorization =====
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddValidatorsFromAssemblyContaining<PBL3.Shared.DTOs.Auth.LoginRequestValidator>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddTransient<AuthHeaderHandler>();

// ===== HttpClient trỏ về API Backend (có gắn AuthHeaderHandler) =====
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl chưa được cấu hình trong wwwroot/appsettings.json.");

builder.Services.AddHttpClient("HushStoreAPI", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<AuthHeaderHandler>();

// HttpClient cho Provinces API (public, không cần auth)
builder.Services.AddHttpClient("ProvincesAPI", client =>
{
    client.BaseAddress = new Uri("https://provinces.open-api.vn");
});

// Đăng ký HttpClient mặc định (inject HttpClient trực tiếp) dùng Named client ở trên
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("HushStoreAPI"));

// MudBlazor
builder.Services.AddMudServices();

// Client Services (DI)
builder.Services.AddScoped<ICategoryClientService, CategoryClientService>();
builder.Services.AddScoped<IProductClientService, ProductClientService>();
builder.Services.AddScoped<IManufacturerClientService, ManufacturerClientService>();
builder.Services.AddScoped<ISupplierClientService, SupplierClientService>();
builder.Services.AddScoped<IImportReceiptClientService, ImportReceiptClientService>();
builder.Services.AddScoped<IProductSerialClientService, ProductSerialClientService>();
builder.Services.AddScoped<IAuthClientService, AuthClientService>();
builder.Services.AddScoped<IPosClientService, PosClientService>();
builder.Services.AddScoped<ICustomerClientService, CustomerClientService>();
builder.Services.AddScoped<IEmployeeClientService, EmployeeClientService>();
builder.Services.AddScoped<IUserAddressClientService, UserAddressClientService>();
builder.Services.AddScoped<Client.Services.Orders.IOrderClientService, Client.Services.Orders.OrderClientService>();
builder.Services.AddScoped<IInventoryExportClientService, InventoryExportClientService>();
builder.Services.AddScoped<Client.Services.Storefront.IStorefrontClientService, Client.Services.Storefront.StorefrontClientService>();
builder.Services.AddScoped<Client.Services.Cart.ICartClientService, Client.Services.Cart.CartClientService>();
builder.Services.AddScoped<IVoucherClientService, VoucherClientService>();
builder.Services.AddScoped<Client.Services.BuildPc.IBuildPcClientService, Client.Services.BuildPc.BuildPcClientService>();
builder.Services.AddScoped<IImageClientService, ImageClientService>();
builder.Services.AddScoped<IServiceTicketClientService, ServiceTicketClientService>();
builder.Services.AddScoped<IServiceInvoiceClientService, ServiceInvoiceClientService>();
builder.Services.AddScoped<IAnalyticsClientService, AnalyticsClientService>();
builder.Services.AddScoped<Client.Services.Reviews.IReviewClientService, Client.Services.Reviews.ReviewClientService>();
builder.Services.AddScoped<IBannerClientService, BannerClientService>();
builder.Services.AddScoped<IComparisonService, ComparisonService>();
builder.Services.AddScoped<IInventoryCheckClientService, InventoryCheckClientService>();

await builder.Build().RunAsync();