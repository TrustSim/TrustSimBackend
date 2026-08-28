using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Application;
using TrustSimBackend.Infrastructure;
using TrustSimBackend.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ─── 1. Đăng ký tầng Application & Infrastructure vào DI Container ─────────────
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// ─── 2. Controllers và Swagger ───────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ─── 3. Seed dữ liệu ban đầu khi ứng dụng khởi động ────────────────────────────
// Dùng IServiceScopeFactory để tạo scope tạm, lấy DbContext và gọi DbSeeder
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TrustSimDbContext>();

    // Áp dụng Migration chưa chạy (nếu có) — an toàn khi chạy nhiều lần
    await context.Database.MigrateAsync();

    // Nạp dữ liệu master
    await DbSeeder.SeedAsync(context);
}

// ─── 4. Middleware Pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
