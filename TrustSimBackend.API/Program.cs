using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TrustSimBackend.API.Middlewares;
using TrustSimBackend.Application;
using TrustSimBackend.Infrastructure;
using TrustSimBackend.Infrastructure.Persistence;

// ─── 0. Nạp biến môi trường từ file .env (nếu có) ──────────────────────────────
var currentDir = Directory.GetCurrentDirectory();
var candidatePaths = new[]
{
    Path.Combine(currentDir, ".env"),
    Path.Combine(currentDir, "..", ".env"),
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env"),
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".env")
};

foreach (var path in candidatePaths)
{
    if (File.Exists(path))
    {
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;
            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2)
            {
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
            }
        }
        break;
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// ─── 1. Đăng ký tầng Application & Infrastructure vào DI Container ─────────────
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// ─── 2. Cấu hình JWT Authentication ──────────────────────────────────────────────
var jwtSecretKey = builder.Configuration["JwtSettings:SecretKey"] 
    ?? "TrustSimSuperSecretKey2026WithSufficientLengthForHMACSHA256!";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "TrustSimBackend";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "TrustSimClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ─── 3. Controllers và Swagger với hỗ trợ JWT Bearer ─────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TrustSim API",
        Version = "v1",
        Description = "Hệ thống sàn giao dịch và đấu giá SIM số đẹp an toàn TrustSim"
    });

    // Cấu hình nút Authorize (hình ổ khóa) trên giao diện Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập Token theo cú pháp: Bearer {your_token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ─── 4. Seed dữ liệu ban đầu khi ứng dụng khởi động ────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TrustSimDbContext>();
    await context.Database.MigrateAsync();
    await DbSeeder.SeedAsync(context);
}

// ─── 5. Middleware Pipeline ───────────────────────────────────────────────────────
// Middleware bắt lỗi toàn cục và định dạng JSON ApiResponse
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Thứ tự quan trọng: Authentication -> Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
