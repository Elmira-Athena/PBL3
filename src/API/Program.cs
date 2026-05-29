using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PBL3.API.Extensions;
using PBL3.API.Middlewares;
using PBL3.Application.Common.Exceptions;
using PBL3.Core.Entities;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Common;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddOpenApi();
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

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddApplicationServices(builder.Configuration);

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
    var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var ex = feature?.Error;

    ctx.Response.ContentType = "application/json";
    var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    switch (ex)
    {
        case NotFoundException notFound:
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(ApiResult<object>.Fail(notFound.Message), opts));
            break;
        case ForbiddenException forbidden:
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(ApiResult<object>.Fail(forbidden.Message), opts));
            break;
        case BusinessRuleException rule:
            ctx.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(ApiResult<object>.Fail(rule.Message), opts));
            break;
        case ConflictException conflict:
            ctx.Response.StatusCode = StatusCodes.Status409Conflict;
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(ApiResult<object>.Fail(conflict.Message), opts));
            break;
        default:
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var message = app.Environment.IsDevelopment()
                ? ex?.Message ?? "Lỗi máy chủ nội bộ."
                : "Lỗi máy chủ nội bộ.";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(ApiResult<object>.Fail(message), opts));
            break;
    }
}));

app.UseHttpsRedirection();
app.UseCors("AllowClient");
app.UseRateLimiter();
app.UseAuthentication();

// Kiểm tra IsActive sau khi JWT đã được xác thực — dùng MemoryCache 30 giây để giảm DB query
app.UseMiddleware<UserStatusMiddleware>();

app.UseAuthorization();

// Health check endpoint for Docker
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

app.MapControllers();

app.Run();
