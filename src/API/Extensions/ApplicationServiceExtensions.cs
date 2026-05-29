using System.Threading.RateLimiting;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using PBL3.Application.Analytics;
using PBL3.Application.Auth;
using PBL3.Core.Interfaces;
using PBL3.Application.Banners;
using PBL3.Application.Cart;
using PBL3.Application.Categories;
using PBL3.Application.Customers;
using PBL3.Application.Employees;
using PBL3.Application.ImportReceipts;
using PBL3.Application.Inventory;
using PBL3.Application.Manufacturers;
using PBL3.Application.Orders;
using PBL3.Application.Pos;
using PBL3.Application.ProductSerials;
using PBL3.Application.Products;
using PBL3.Application.Reviews;
using PBL3.Application.ServiceInvoices;
using PBL3.Application.ServiceTickets;
using PBL3.Application.Storage;
using PBL3.Application.Storefront;
using PBL3.Application.Suppliers;
using PBL3.Application.Vouchers;
using PBL3.Shared.DTOs.Banners;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.DTOs.Reviews;
using PBL3.Shared.Validators.Banners;
using PBL3.Shared.Validators.Inventory;
using PBL3.Shared.Validators.Reviews;

namespace PBL3.API.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Application services
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IManufacturerService, ManufacturerService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IImportReceiptService, ImportReceiptService>();
        services.AddScoped<IProductSerialService, ProductSerialService>();
        services.AddScoped<IInventorySyncService, InventorySyncService>();
        services.AddScoped<IInventoryCheckService, InventoryCheckService>();
        services.AddScoped<IInventoryExportService, InventoryExportService>();
        services.AddScoped<IPosService, PosService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IStorefrontService, StorefrontService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IVoucherService, VoucherService>();
        services.AddScoped<PBL3.Application.BuildPc.IBuildPcService, PBL3.Application.BuildPc.BuildPcService>();
        services.AddScoped<IServiceTicketService, ServiceTicketService>();
        services.AddScoped<IServiceInvoiceService, ServiceInvoiceService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IProductReviewService, ProductReviewService>();
        services.AddScoped<IBannerService, BannerService>();
        services.AddScoped<IAuthService, AuthService>();

        // FluentValidation validators
        services.AddScoped<IValidator<CreateReviewRequest>, CreateReviewRequestValidator>();
        services.AddScoped<IValidator<CreateBannerRequest>, CreateBannerRequestValidator>();
        services.AddScoped<IValidator<UpdateBannerRequest>, UpdateBannerRequestValidator>();
        services.AddScoped<IValidator<CreateInventoryCheckRequest>, CreateInventoryCheckRequestValidator>();
        services.AddScoped<IValidator<ScanSerialRequest>, ScanSerialRequestValidator>();
        services.AddScoped<IValidator<UpdateScanReasonRequest>, UpdateScanReasonRequestValidator>();
        services.AddScoped<IValidator<RejectInventoryCheckRequest>, RejectInventoryCheckRequestValidator>();

        // AWS S3 Storage
        var awsCfg = configuration.GetSection("AwsSettings");
        services.AddSingleton<IAmazonS3>(_ =>
        {
            var credentials = new BasicAWSCredentials(awsCfg["AccessKeyId"], awsCfg["SecretAccessKey"]);
            var region = RegionEndpoint.GetBySystemName(awsCfg["Region"] ?? "ap-southeast-1");
            return new AmazonS3Client(credentials, region);
        });
        services.AddScoped<IStorageService, PBL3.Application.Storage.S3StorageService>();

        // Rate Limiting: Chống DoS & Brute-force cho Login
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("LoginRateLimit", limiter =>
            {
                limiter.PermitLimit = 5;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;  // Reject ngay, không queue
            });
        });

        return services;
    }
}
