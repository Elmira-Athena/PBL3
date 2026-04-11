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
using Client.Services.Pos;
using Client.Services.Product;
using Client.Services.Supplier;
using Client.Services.Inventory;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ===== Authentication & Authorization =====
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddValidatorsFromAssemblyContaining<PBL3.Shared.DTOs.Auth.LoginRequestValidator>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();
builder.Services.AddTransient<AuthHeaderHandler>();

// ===== HttpClient trỏ về API Backend (có gắn AuthHeaderHandler) =====
builder.Services.AddHttpClient("HushStoreAPI", client =>
{
    client.BaseAddress = new Uri("https://localhost:7010");
}).AddHttpMessageHandler<AuthHeaderHandler>();

// Đăng ký HttpClient mặc định (inject HttpClient trực tiếp) dùng Named client ở trên
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("HushStoreAPI"));

// MudBlazor
builder.Services.AddMudServices();

// Client Services (DI)
builder.Services.AddScoped<ICategoryClientService, CategoryClientService>();
builder.Services.AddScoped<IProductClientService, ProductClientService>();
builder.Services.AddScoped<ISupplierClientService, SupplierClientService>();
builder.Services.AddScoped<IImportReceiptClientService, ImportReceiptClientService>();
builder.Services.AddScoped<IProductSerialClientService, ProductSerialClientService>();
builder.Services.AddScoped<IAuthClientService, AuthClientService>();
builder.Services.AddScoped<IPosClientService, PosClientService>();
builder.Services.AddScoped<ICustomerClientService, CustomerClientService>();

await builder.Build().RunAsync();