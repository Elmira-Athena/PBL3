using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using PBL3.Shared.DTOs.Common;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using PBL3.Infrastructure.Repositories;
using PBL3.Service.Auth;
using PBL3.Service.Categories;
using PBL3.Service.ImportReceipts;
using PBL3.Service.Banners;
using PBL3.Service.Manufacturers;
using PBL3.Service.Products;
using PBL3.Service.ProductSerials;
using PBL3.Service.Suppliers;
using PBL3.Service.Inventory;
using PBL3.Service.Pos;
using PBL3.Service.Customers;
using PBL3.Service.Employees;
using PBL3.Service.Storage;
using PBL3.Service.Storefront;
using PBL3.Service.Cart;
using PBL3.Service.Orders;
using PBL3.Service.Vouchers;
using PBL3.Service.ServiceTickets;
using PBL3.Service.ServiceInvoices;
using PBL3.Service.Analytics;
using PBL3.Service.Reviews;
using PBL3.Shared.DTOs.Banners;
using PBL3.Shared.DTOs.Reviews;
using FluentValidation;
using PBL3.Shared.Validators.Banners;
using PBL3.Shared.Validators.Reviews;
using PBL3.Shared.DTOs.Inventory;
using PBL3.Shared.Validators.Inventory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddMemoryCache();

// CORS: Cho phép Frontend (Blazor WASM) gọi API
var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',')
    ?? ["http://localhost:5214", "https://localhost:7107"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("X-Account-Status");
    });
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Swagger
builder.Services.AddSwaggerGen();

// Add DbContext
builder.Services.AddDbContext<HushStoreDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<AppUser, AppRole>(options =>
{
    // Password settings (Tùy chỉnh độ khó theo thực tế)
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;

    // Lockout settings (Chống Brute-force)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<HushStoreDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSecretKey = builder.Configuration["JwtSettings:SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey chưa được cấu hình trong appsettings.json.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero // Không cho phép sai lệch thời gian
    };
});

// DI: Repositories
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IManufacturerRepository, ManufacturerRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IImportReceiptRepository, ImportReceiptRepository>();
builder.Services.AddScoped<IProductSerialRepository, ProductSerialRepository>();
builder.Services.AddScoped<IInventoryCheckRepository, InventoryCheckRepository>();
builder.Services.AddScoped<IWarrantyRepository, WarrantyRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IVoucherRepository, VoucherRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<IUserAddressRepository, UserAddressRepository>();
builder.Services.AddScoped<IProductReviewRepository, ProductReviewRepository>();
builder.Services.AddScoped<IServiceTicketRepository, ServiceTicketRepository>();
builder.Services.AddScoped<IQuotationRepository, QuotationRepository>();
builder.Services.AddScoped<IRmaShipmentRepository, RmaShipmentRepository>();
builder.Services.AddScoped<IServiceInvoiceRepository, ServiceInvoiceRepository>();
builder.Services.AddScoped<ISerialRepairLogRepository, SerialRepairLogRepository>();
builder.Services.AddScoped<IBannerRepository, BannerRepository>();

// DI: Unit of Work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// DI: Services
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IManufacturerService, ManufacturerService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IImportReceiptService, ImportReceiptService>();
builder.Services.AddScoped<IProductSerialService, ProductSerialService>();
builder.Services.AddScoped<IInventorySyncService, InventorySyncService>();
builder.Services.AddScoped<IInventoryCheckService, InventoryCheckService>();
builder.Services.AddScoped<IInventoryExportService, InventoryExportService>();
builder.Services.AddScoped<IPosService, PosService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IStorefrontService, StorefrontService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddScoped<PBL3.Service.BuildPc.IBuildPcService, PBL3.Service.BuildPc.BuildPcService>();
builder.Services.AddScoped<IServiceTicketService, ServiceTicketService>();
builder.Services.AddScoped<IServiceInvoiceService, ServiceInvoiceService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IProductReviewService, ProductReviewService>();
builder.Services.AddScoped<IValidator<CreateReviewRequest>, CreateReviewRequestValidator>();
builder.Services.AddScoped<IBannerService, BannerService>();
builder.Services.AddScoped<IValidator<CreateBannerRequest>, CreateBannerRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateBannerRequest>, UpdateBannerRequestValidator>();
builder.Services.AddScoped<IValidator<CreateInventoryCheckRequest>, CreateInventoryCheckRequestValidator>();
builder.Services.AddScoped<IValidator<ScanSerialRequest>, ScanSerialRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateScanReasonRequest>, UpdateScanReasonRequestValidator>();
builder.Services.AddScoped<IValidator<RejectInventoryCheckRequest>, RejectInventoryCheckRequestValidator>();

// DI: AWS S3 Storage
var awsCfg = builder.Configuration.GetSection("AwsSettings");
builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var credentials = new BasicAWSCredentials(awsCfg["AccessKeyId"], awsCfg["SecretAccessKey"]);
    var region = RegionEndpoint.GetBySystemName(awsCfg["Region"] ?? "ap-southeast-1");
    return new AmazonS3Client(credentials, region);
});
builder.Services.AddScoped<IStorageService, PBL3.Service.Storage.S3StorageService>();

// DI: Auth
builder.Services.AddScoped<IAuthService, AuthService>();

// Rate Limiting: Chống DoS & Brute-force cho Login
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("LoginRateLimit", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;  // Reject ngay, không queue
    });
});

var app = builder.Build();

// Auto-apply EF Core migrations khi khởi động — chỉ chạy trên Production
// (tránh lỗi khi dev chạy local với DB chưa up)
if (app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HushStoreDbContext>();
    await db.Database.MigrateAsync();
}

// Seed "Technician" role if it doesn't exist
using (var roleScope = app.Services.CreateScope())
{
    var roleManager = roleScope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
    if (!await roleManager.RoleExistsAsync("Technician"))
    {
        await roleManager.CreateAsync(new AppRole { Name = "Technician", RoleCode = "KTV" });
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "HushStore API v1");
    });
}

app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/json";
    var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var message = app.Environment.IsDevelopment()
        ? feature?.Error?.Message ?? "Lỗi máy chủ nội bộ."
        : "Lỗi máy chủ nội bộ.";
    var result = ApiResult<object>.Fail(message);
    await ctx.Response.WriteAsync(JsonSerializer.Serialize(result,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
}));

app.UseHttpsRedirection();

app.UseCors("AllowClient");

app.UseRateLimiter();
app.UseAuthentication();

// Kiểm tra IsActive sau khi JWT đã được xác thực — dùng MemoryCache 30 giây để giảm DB query
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        !context.Request.Path.StartsWithSegments("/api/auth"))
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
            var cacheKey = $"user_isactive_{userId.ToLowerInvariant()}";

            if (!cache.TryGetValue(cacheKey, out (bool IsActive, string? LockReason) userData))
            {
                var userManager = context.RequestServices.GetRequiredService<UserManager<AppUser>>();
                var user = await userManager.FindByIdAsync(userId);
                userData = (user?.IsActive ?? false, user?.LockReason);
                cache.Set(cacheKey, userData, TimeSpan.FromSeconds(30));
            }

            if (!userData.IsActive)
            {
                var reason = !string.IsNullOrEmpty(userData.LockReason)
                    ? userData.LockReason
                    : "Vui lòng liên hệ quản trị viên.";
                var message = $"Tài khoản của bạn đã bị khóa. Lý do: {reason}";
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                context.Response.Headers["X-Account-Status"] = "locked";
                await context.Response.WriteAsJsonAsync(ApiResult<object>.Fail(message));
                return;
            }
        }
    }
    await next();
});

app.UseAuthorization();

// Health check endpoint for Docker
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

app.MapControllers();

app.Run();