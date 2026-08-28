using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình DbContext kết nối PostgreSQL
builder.Services.AddDbContext<TrustSimDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// 2. Thêm Controllers và Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
