using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TrustSimBackend.API.Hubs;
using TrustSimBackend.API.Middlewares;
using TrustSimBackend.API.Services;
using TrustSimBackend.Application;
using TrustSimBackend.Application.Features.Auction.Hubs;
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
builder.Services.AddMemoryCache();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// ─── 2. Cấu hình CORS Hỗ trợ Truyền Cookie (Credentials) & Mọi Domain Test (Live Server / Ngrok) ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowTrustSimClients", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ─── 3. Cấu hình JWT Authentication Hỗ trợ Cả Header & HttpOnly Cookie ─────────
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

    // Cơ chế Hybrid Token Extraction: Query param (SignalR) -> Header Authorization -> HttpOnly Cookie
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // 1. Kiểm tra access_token trong Query String (dành cho kết nối WebSocket SignalR)
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
                return Task.CompletedTask;
            }

            // 2. Kiểm tra Authorization Header (dành cho Mobile App / Postman / Swagger Authorize Modal)
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Token = authHeader["Bearer ".Length..].Trim();
                return Task.CompletedTask;
            }

            // 3. Nếu không có Header, trích xuất từ Cookie 'accessToken'
            if (context.Request.Cookies.TryGetValue("accessToken", out var cookieToken) && !string.IsNullOrWhiteSpace(cookieToken))
            {
                context.Token = cookieToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ─── 3.1. Cấu hình SignalR Hub & Realtime Services ──────────────────────────────
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<IAuctionRealtimeNotifier, AuctionRealtimeNotifier>();

// ─── 4. Controllers và Swagger với hỗ trợ JWT Bearer, Cookie & XML Comments ──────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TrustSim API",
        Version = "v1",
        Description = "Hệ thống sàn giao dịch và đấu giá SIM số đẹp an toàn TrustSim tích hợp cổng SePay (Hỗ trợ xác thực qua HttpOnly Cookie & Bearer Token)"
    });

    // Cấu hình nút Authorize (hình ổ khóa) trên giao diện Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập Token theo cú pháp: Bearer {your_token}. Lưu ý: Khi test trên Swagger UI, sau khi gọi Login/Register thành công, Swagger sẽ tự động gửi kèm HttpOnly Cookie nên không bắt buộc phải nhập Token vào đây.",
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

    // Tránh xung đột SchemaId khi có các DTO cùng tên ở các namespace khác nhau
    c.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);

    // Nạp XML Documentation để hiển thị ghi chú mô tả API trên Swagger
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// ─── 5. Seed dữ liệu ban đầu khi ứng dụng khởi động ────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TrustSimDbContext>();
    await context.Database.MigrateAsync();
    await DbSeeder.SeedAsync(context);
}

// ─── 6. Middleware Pipeline ───────────────────────────────────────────────────────
// Middleware bắt lỗi toàn cục và định dạng JSON ApiResponse
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TrustSim API v1");
        // Kích hoạt Swagger UI gửi kèm Cookie trong tất cả các request
        c.ConfigObject.AdditionalItems["withCredentials"] = true;
    });
}

// CORS phải đặt TRƯỜC UseHttpsRedirection để preflight OPTIONS request không bị redirect trước khi có CORS header
app.UseCors("AllowTrustSimClients");

app.UseHttpsRedirection();

// Thứ tự quan trọng: Authentication -> UserContextMiddleware -> Authorization
app.UseAuthentication();
app.UseMiddleware<UserContextMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AuctionHub>("/hubs/auction");

app.Run();
