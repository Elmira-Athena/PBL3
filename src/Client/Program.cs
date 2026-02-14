using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Client;
using Client.Services;
using Client.Services.Inventory;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient trỏ về API Backend
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7010")
});

// MudBlazor
builder.Services.AddMudServices();

// Client Services (DI)
builder.Services.AddScoped<ICategoryClientService, CategoryClientService>();
builder.Services.AddScoped<IProductClientService, ProductClientService>();
builder.Services.AddScoped<ISupplierClientService, SupplierClientService>();
builder.Services.AddScoped<IImportReceiptClientService, ImportReceiptClientService>();
builder.Services.AddScoped<IProductSerialClientService, ProductSerialClientService>();

await builder.Build().RunAsync();