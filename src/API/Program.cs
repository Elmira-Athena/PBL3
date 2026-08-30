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
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using PBL3.Infrastructure.Repositories;
using PBL3.API.Filters;
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

builder.Services.AddControllers(options =>
{
    // Chặn trần pageSize cho toàn bộ 148 endpoint, kể cả endpoint viết sau này.
    options.Filters.Add<ClampPageSizeFilter>();
});

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
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    // ServiceInvoice intentionally omits the query filter so financial records
    // remain queryable even after the parent ServiceTicket is soft-deleted.
    options.ConfigureWarnings(w => w.Ignore(
        Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId
            .PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
});

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
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

// DI: Repositories
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductVariantRepository, ProductVariantRepository>();
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
builder.Services.AddScoped<IProductVariantService, ProductVariantService>();
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
// KHÔNG dùng credential tĩnh (static access key/secret key). AmazonS3Client
// không truyền credential sẽ dùng default credential chain: trên ECS nó tự
// lấy credential tạm thời của task role qua
// AWS_CONTAINER_CREDENTIALS_RELATIVE_URI. Nhờ vậy trong toàn hệ thống không
// còn static access key nào.
// Local dev: đặt AWS_PROFILE=hushstore trước khi chạy để upload ảnh hoạt động.
var awsCfg = builder.Configuration.GetSection("AwsSettings");
builder.Services.AddSingleton<IAmazonS3>(_ =>
    new AmazonS3Client(RegionEndpoint.GetBySystemName(awsCfg["Region"] ?? "ap-southeast-1")));
builder.Services.AddScoped<IStorageService, PBL3.Service.Storage.S3StorageService>();

// DI: Auth
builder.Services.AddScoped<IAuthService, AuthService>();

// Health check cho ALB target group. /health/ready CHẠM DB thật — nếu RDS
// chết thì ALB phải rút instance khỏi target group, không được báo healthy.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<HushStoreDbContext>("database");

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

// ALB terminate TLS rồi forward HTTP xuống container. Không có block này thì
// app không biết request gốc là HTTPS, khiến mọi URL sinh ra bị sai scheme.
//
// KnownNetworks/KnownProxies bị clear vì container chạy bridge network mode:
// source IP mà app thấy là gateway của docker bridge (172.17.0.1), KHÔNG phải
// IP của ALB trong VPC — nên không thể whitelist theo VPC CIDR. An toàn vì
// sg-web chỉ nhận traffic từ sg-alb, không ai khác chạm tới được container.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // ForwardLimit = 1 là lớp phòng thủ đi cùng việc clear KnownProxies ở trên.
    // Vì đã bỏ whitelist proxy, mọi giá trị trong X-Forwarded-For đều được coi
    // là "do proxy tin cậy ghi" — nên số phần tử được đọc chính là ranh giới
    // an toàn duy nhất còn lại.
    //
    // Cơ chế: ALB APPEND vào X-Forwarded-For, không replace. Client gửi
    //     X-Forwarded-For: 1.2.3.4        (giả mạo)
    // thì container nhận
    //     X-Forwarded-For: 1.2.3.4, <IP thật của client>
    // ASP.NET đọc từ PHẢI sang trái, nên đọc đúng 1 phần tử là lấy đúng IP mà
    // ALB quan sát được, và phần client tự bơm bị bỏ lại. Đặt 2 trở lên là tự
    // tay tin vào chuỗi do client kiểm soát — RemoteIpAddress sẽ thành giá trị
    // kẻ tấn công chọn, và mọi thứ dựa trên nó (rate limit theo IP, log audit,
    // chặn theo IP) đều bị lách.
    //
    // Đây cũng là giá trị mặc định của ASP.NET Core. Ghi tường minh vì
    // `drop_invalid_header_fields` trên ALB KHÔNG chặn được X-Forwarded-*
    // (ALB append, không phải reject), nên dòng này là chỗ duy nhất trong toàn
    // hệ thống chặn giả mạo X-Forwarded-For — không phải chỗ nên để mặc định
    // ngầm rồi có người đổi mà không biết mình đang mở gì.
    options.ForwardLimit = 1;
});

var app = builder.Build();

// Migration KHÔNG chạy ở startup nữa. Từ Task 13 trở đi, schema do một ECS
// task riêng chạy EF Core migration bundle dựng lên, và pipeline chỉ deploy
// khi task đó exit 0. Lý do: migration ở startup không ai gate được, fail thì
// container crash-loop, và nhiều task cùng lên sẽ race trên bảng
// __EFMigrationsHistory.
// Xem docs/superpowers/specs/2026-08-17-aws-terraform-ecs-infra-design.md

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

// UseForwardedHeaders phải chạy TRƯỚC mọi middleware đọc scheme hoặc IP.
app.UseForwardedHeaders();

// Ở Production, ALB đã redirect 80 -> 443 ở tầng listener rồi. Bật thêm ở
// đây sẽ gây redirect loop khi ALB forward request HTTP xuống container.
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowClient");

app.UseRateLimiter();
app.UseAuthentication();

// Kiểm tra IsActive sau khi JWT đã được xác thực — ĐỌC THẲNG DB, KHÔNG CACHE.
//
// Vì sao bỏ MemoryCache 30 giây (đợt 1):
//   MemoryCache nằm trong RAM của MỘT tiến trình. Với 1 task, admin khoá tài khoản
//   thì phiên của người đó còn sống thêm tối đa 30 giây — chấp nhận được.
//   Với 2 task sau ALB, lệnh xoá cache chỉ chạm cache của task NHẬN request khoá;
//   task còn lại vẫn giữ bản cũ và vẫn cho vào. Triệu chứng là "lúc được lúc không
//   tuỳ ALB định tuyến" — loại lỗi không tái hiện được.
//
//   Điểm mấu chốt: hướng nguy hiểm là hướng MỞ KHOÁ (cache nói còn hoạt động trong
//   khi DB đã khoá), không phải hướng khoá. Đó là lỗi bảo mật, không phải lỗi hiệu năng.
//
//   Đây cũng chính là thứ làm cho việc chạy nhiều task KHÔNG cần Redis.
//
// Đổi luôn FindByIdAsync (kéo TOÀN BỘ hàng AppUsers về) sang projection đúng 2 cột —
// theo quy tắc "DTO Projection" của CLAUDE.md, và rẻ hơn hẳn kể cả khi còn cache.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        !context.Request.Path.StartsWithSegments("/api/auth"))
    {
        var userIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userIdRaw) && Guid.TryParse(userIdRaw, out var userId))
        {
            var db = context.RequestServices.GetRequiredService<HushStoreDbContext>();
            var userData = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsActive, u.LockReason })
                .FirstOrDefaultAsync();

            if (userData is null || !userData.IsActive)
            {
                var reason = !string.IsNullOrEmpty(userData?.LockReason)
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

// /health/live: chỉ trả lời "process còn sống", không chạm dependency nào.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// /health/ready: chạy toàn bộ health check, gồm cả DbContextCheck.
// Trả 200 Healthy / 503 Unhealthy. Đây là endpoint ALB tg-api trỏ vào.
app.MapHealthChecks("/health/ready").AllowAnonymous();

// Giữ /health cũ để tương thích với script và bookmark hiện có.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

app.MapControllers();

app.Run();