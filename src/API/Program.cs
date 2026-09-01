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
using PBL3.API.Middleware;
using PBL3.Service.Common;
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
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(
            // Đợt 1 mục 4.1. Điều kiện cần đã đủ: 18 call-site transaction đều đi qua
            // IUnitOfWork.ExecuteInTransactionAsync, tức qua CreateExecutionStrategy().
            // EF Core CẤM BeginTransactionAsync() thủ công khi có retrying strategy và
            // ném LÚC CHẠY chứ không lúc biên dịch — nên thứ tự "gom transaction trước,
            // bật retry sau" là bắt buộc, không phải sở thích.
            //
            // Giá trị lớn nhất của việc bật cờ này KHÔNG nằm ở 18 chỗ có transaction mà
            // ở toàn bộ phần còn lại: mọi query đọc, mọi SaveChanges đơn lẻ, health check
            // — tức gần như toàn bộ lưu lượng — nay tự chịu được lỗi transient. Đó đúng
            // là thứ xảy ra khi RDS failover, khi rolling deploy, và khi pool cạn.
            //
            // 18 chỗ có transaction thì mặc định VẪN KHÔNG retry (retrySafe = false) cho
            // tới khi từng chỗ được rà. Xem hợp đồng retry ở IUnitOfWork.
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null));
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
// Cấp số cho mã chứng từ bằng SQL SEQUENCE (đợt 3). Nằm ở nhóm Repositories vì nó
// chạm DB trực tiếp; xem IDocumentSequence để biết vì sao nó là một lớp trừu tượng riêng.
builder.Services.AddScoped<IDocumentSequence, DocumentSequenceRepository>();

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

// Sinh mã chứng từ (ORD/POS/PN/KK/ST/SRV) — gom 7 khối trùng lặp về một chỗ.
// Đợt 3 đã thay ruột: số thứ tự do SQL SEQUENCE cấp, không còn "đọc mã cuối rồi +1".
builder.Services.AddScoped<IDocumentCodeGenerator, DocumentCodeGenerator>();

// ── Ánh xạ xung đột đồng thời sang 409 (đợt 3, mục 🅶) ──
// Chạy TRƯỚC khối UseExceptionHandler(500) ở phần app phía dưới. AddProblemDetails()
// là điều kiện của IExceptionHandler trong ASP.NET Core — thiếu nó thì handler vẫn
// được gọi nhưng fallback mất ProblemDetails; ta không dùng ProblemDetails cho thân
// phản hồi (repo dùng ApiResult<T>), nhưng vẫn đăng ký cho đúng khuôn framework.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ConflictExceptionHandler>();

// Làm sạch HTML người dùng nhập, áp TRÊN ĐƯỜNG GHI. Singleton vì HtmlSanitizer
// dựng khá tốn (kéo theo AngleSharp) và an toàn để dùng lại sau khi cấu hình.
builder.Services.AddSingleton<IHtmlContentSanitizer, HtmlContentSanitizer>();
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

// ══════════════════════════════════════════════════════════════════════════
// Rate Limiting — viết lại ở đợt 2
// ══════════════════════════════════════════════════════════════════════════
//
// LỖI CỦA BẢN CŨ (đang chạy trên production, không phải chuyện lý thuyết):
//   options.AddFixedWindowLimiter("LoginRateLimit", ...) — overload này tạo
//   MỘT limiter DUY NHẤT, KHÔNG PHÂN VÙNG, cho TOÀN BỘ endpoint. Tức 5 lần
//   đăng nhập mỗi phút cho TẤT CẢ người dùng CỘNG LẠI. Một kẻ tấn công đốt hết
//   hạn mức là khoá đăng nhập của mọi khách hàng.
//
//   Đó là một cuộc DoS TỰ GÂY RA, và nó đang xảy ra ngay bây giờ với 1 task —
//   tệ hơn hẳn vấn đề "2 task = 2× hạn mức" mà tài liệu cũ lo.
//
// Bản mới: mọi policy đều PHÂN VÙNG THEO IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // ── Trần chung theo IP: 100 request / 10 giây ──
    //
    // 🔴 MIỄN TRỪ /health/* LÀ BẮT BUỘC, KHÔNG PHẢI TỐI ƯU.
    //
    // Container chạy bridge network nên source IP mà app thấy là gateway của
    // docker cho MỌI request. UseForwardedHeaders viết lại thành IP thật cho
    // traffic đi qua ALB — nhưng HEALTH CHECK CỦA ALB GỌI THẲNG VÀO INSTANCE,
    // KHÔNG MANG X-Forwarded-For. Nên health check rơi vào cùng phân vùng
    // "gateway docker" với traffic thật.
    //
    // Hậu quả nếu không miễn trừ: một đợt tải làm health check bị 429 → ALB
    // kết luận unhealthy sau ~45 giây → ECS GIẾT TASK ĐANG CHẠY TỐT, ĐÚNG LÚC
    // TẢI CAO. Đây là chế độ chết tệ nhất có thể nghĩ ra cho một hệ thống đang
    // phải chứng minh khả năng chịu tải.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            GetClientIp(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromSeconds(10),
                QueueLimit = 0
            });
    });

    // ── Đăng nhập: 5 lần/phút MỖI IP (bản cũ là 5 lần/phút cho cả thế giới) ──
    AddPerIpFixedWindow(options, "LoginRateLimit", permitLimit: 5, TimeSpan.FromMinutes(1));

    // ── Đăng ký: 3 lần/giờ mỗi IP. `register` ghi DB không giới hạn là đường
    //    làm phình bảng AppUsers rẻ nhất. ──
    AddPerIpFixedWindow(options, "RegisterRateLimit", permitLimit: 3, TimeSpan.FromHours(1));

    // ── Refresh token: 10 lần/phút mỗi IP. Refresh token xoay vòng mỗi lần
    //    dùng nên nhịp bình thường rất thấp; vượt xa mức này là dấu hiệu dò. ──
    AddPerIpFixedWindow(options, "RefreshRateLimit", permitLimit: 10, TimeSpan.FromMinutes(1));

    // ── Hai endpoint tra cứu là ORACLE: tra serial và dò mã voucher. Chúng trả
    //    lời "có tồn tại hay không" cho người chưa đăng nhập, tức cho phép quét
    //    sạch không gian mã nếu không chặn nhịp. ──
    AddPerIpFixedWindow(options, "LookupRateLimit", permitLimit: 10, TimeSpan.FromMinutes(1));

    // ── Đọc công khai (catalogue, tìm kiếm): 60 lần/phút mỗi IP. ──
    AddPerIpFixedWindow(options, "PublicReadRateLimit", permitLimit: 60, TimeSpan.FromMinutes(1));

    // ── Thân phản hồi khi bị chặn ──
    // Bản cũ trả 429 với THÂN RỖNG, vi phạm quy tắc "mọi thông báo lỗi cho
    // người dùng phải bằng tiếng Việt có dấu" của CLAUDE.md.
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";

        // Gợi ý cho client biết khi nào thử lại được, nếu limiter tính được.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResult<object>.Fail(
                "Bạn thao tác quá nhanh. Vui lòng chờ trong giây lát rồi thử lại."),
            cancellationToken);
    };
});

// Phân vùng theo IP client. Đặt SAU UseForwardedHeaders trong pipeline nên
// RemoteIpAddress đã là IP thật cho traffic qua ALB.
//
// Độ tin cậy của toàn bộ cơ chế này phụ thuộc vào ForwardLimit = 1 ở khối
// ForwardedHeadersOptions ngay bên dưới — đổi nó thành >= 2 là mở đường cho kẻ
// tấn công tự chọn phân vùng của mình bằng cách bơm X-Forwarded-For, và mọi
// hạn mức ở đây thành vô nghĩa.
static string GetClientIp(HttpContext context)
    => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static void AddPerIpFixedWindow(
    RateLimiterOptions options, string policyName, int permitLimit, TimeSpan window)
{
    options.AddPolicy(policyName, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientIp(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0  // Reject ngay, không queue
            }));
}

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

// Tham số HSTS (đợt 2). Bắt đầu THẬN TRỌNG, vì HSTS là quyết định KHÓ HOÀN TÁC:
// trình duyệt đã ghi nhớ thì trong suốt max-age nó TỪ CHỐI mọi kết nối HTTP tới
// host này, kể cả khi ta đã gỡ header đi.
builder.Services.AddHsts(options =>
{
    // 1 ngày, không phải 1 năm. Nếu cấu hình TLS có vấn đề thì thiệt hại giới hạn
    // trong 24 giờ. Nâng dần lên sau khi chạy ổn định.
    options.MaxAge = TimeSpan.FromDays(1);

    // api là host riêng, không phải domain gốc — không được thay mặt các
    // subdomain khác quyết định thay chúng.
    options.IncludeSubDomains = false;

    // TUYỆT ĐỐI KHÔNG bật. Vào preload list là bị nhúng cứng vào binary của
    // trình duyệt; gỡ ra mất hàng tháng và phải qua quy trình bên ngoài.
    options.Preload = false;
});

var app = builder.Build();

// Migration KHÔNG chạy ở startup nữa. Từ Task 13 trở đi, schema do một ECS
// task riêng chạy EF Core migration bundle dựng lên, và pipeline chỉ deploy
// khi task đó exit 0. Lý do: migration ở startup không ai gate được, fail thì
// container crash-loop, và nhiều task cùng lên sẽ race trên bảng
// __EFMigrationsHistory.
// Xem docs/superpowers/specs/2026-08-17-aws-terraform-ecs-infra-design.md

// Seed "Technician" role if it doesn't exist.
//
// LƯỚI AN TOÀN TẠM THỜI — sẽ xoá hẳn ở đợt 5.
// Nguồn tạo role này giờ là Infrastructure/db/seed_data.sql (RoleCode 'KTV').
// Khối dưới đây chỉ còn để đỡ cho môi trường nào chưa chạy lại seeder.
// Điều kiện xoá: xác nhận `SELECT * FROM AppRoles WHERE RoleCode = 'KTV'`
// trả về 1 dòng trên production.
//
// VÌ SAO PHẢI CÓ try/catch: đây là top-level statement chạy trước mọi middleware.
// Hai ECS task cold-start cùng lúc (tức đúng lúc deploy) thì cả hai cùng thấy
// role chưa có và cùng INSERT. Task thua vi phạm HAI unique index cùng lúc —
// RoleNameIndex trên NormalizedName và IX_AppRoles_RoleCode. Vi phạm unique ở
// tầng DB NÉM EXCEPTION chứ không trả IdentityResult thất bại, nên kiểm
// result.Succeeded cũng không cứu được. Exception chưa bắt ở đây => process
// exit khác 0 => TASK CHẾT NGAY LÚC BOOT.
//
// Triệu chứng khó chịu: lần khởi động lại sẽ thành công (role đã tồn tại), nên
// biểu hiện là "một trong hai task chết đúng một lần rồi tự lành" — rất dễ trôi
// qua trong log deploy mà không ai để ý.
//
// Bắt exception ở đây cũng gỡ luôn việc startup của API phụ thuộc vào chuyện
// DB đang sống và cho ghi: RDS chớp tắt lúc task đang lên sẽ không còn gây
// crash-loop. /health/ready tồn tại chính là để xử lý DB chết một cách mềm mại.
using (var roleScope = app.Services.CreateScope())
{
    try
    {
        var roleManager = roleScope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        if (!await roleManager.RoleExistsAsync("Technician"))
        {
            await roleManager.CreateAsync(new AppRole { Name = "Technician", RoleCode = "KTV" });
        }
    }
    catch (Exception ex)
    {
        // Nuốt có chủ đích. Nếu role thật sự thiếu, EmployeeService sẽ báo lỗi khi
        // gán quyền kỹ thuật viên — ồn ào và đúng chỗ, thay vì giết cả tiến trình.
        app.Logger.LogWarning(ex,
            "Không seed được role Technician lúc khởi động. Bỏ qua để tiến trình " +
            "tiếp tục lên. Nếu role thiếu thật, chạy lại task seeder (seed_data.sql).");
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

// ── Xung đột đồng thời → 409, KHÔNG phải 500 (đợt 3) ──
//
// ⚠️ ConflictExceptionHandler được đăng ký bằng AddExceptionHandler<T>() ở phần
// builder phía trên. UseExceptionHandler chạy danh sách IExceptionHandler TRƯỚC,
// và chỉ rơi xuống khối 500 dưới đây khi mọi handler trả về false.
// Đổi thứ tự hai thứ này là làm ConflictExceptionHandler thành code chết mà KHÔNG
// có gì báo lỗi — đã kiểm chạy thật, xem bằng chứng của mục 🅶.
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
else
{
    // ── HSTS (đợt 2) ──
    //
    // Trước đợt 2, production KHÔNG CÓ CẢ redirect LẪN HSTS: khối
    // UseHttpsRedirection nằm trong `if (!IsProduction())`, và UseHsts chưa từng
    // được gọi. Việc ép HTTPS hoàn toàn do ALB và Cloudflare gánh, tầng ứng dụng
    // không đóng góp gì. Đây chính là điều mục A7 của tài liệu rà soát muốn sửa.
    //
    // 🔴 VỊ TRÍ QUAN TRỌNG HƠN BẢN THÂN LỜI GỌI: khối này PHẢI đứng SAU
    // app.UseForwardedHeaders() ở ngay trên. UseHsts() chỉ phát header khi
    // Request.IsHttps == true, mà container nhận HTTP THUẦN từ ALB — chỉ sau khi
    // ForwardedHeaders đọc X-Forwarded-Proto thì IsHttps mới thành true.
    // Đảo thứ tự là một NO-OP HOÀN TOÀN IM LẶNG: không lỗi, không cảnh báo,
    // không header, và không có cách nào biết ngoài việc tự đi curl kiểm tra.
    //
    // KHÔNG thêm UseHttpsRedirection ở nhánh này: ALB đã 301 ở listener, bật
    // thêm sẽ thành redirect loop.
    app.UseHsts();
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