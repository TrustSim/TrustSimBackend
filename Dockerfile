# ─── 1. Build Stage ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Sao chép file solution và các file .csproj để tận dụng Docker layer cache
COPY TrustSimBackend.sln ./
COPY TrustSimBackend.Domain/TrustSimBackend.Domain.csproj TrustSimBackend.Domain/
COPY TrustSimBackend.Application/TrustSimBackend.Application.csproj TrustSimBackend.Application/
COPY TrustSimBackend.Infrastructure/TrustSimBackend.Infrastructure.csproj TrustSimBackend.Infrastructure/
COPY TrustSimBackend.API/TrustSimBackend.API.csproj TrustSimBackend.API/

# Khôi phục dependencies
RUN dotnet restore TrustSimBackend.sln

# Sao chép toàn bộ mã nguồn
COPY . .

# Biên dịch và publish TrustSimBackend.API
WORKDIR /src/TrustSimBackend.API
RUN dotnet publish TrustSimBackend.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# ─── 2. Runtime Stage ────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

EXPOSE 5130
ENV ASPNETCORE_URLS=http://+:5130
ENV ASPNETCORE_ENVIRONMENT=Development

# Sao chép kết quả publish
COPY --from=build /app/publish .

# Tạo sẵn thư mục uploads lưu trữ file cục bộ
RUN mkdir -p /app/wwwroot/uploads

ENTRYPOINT ["dotnet", "TrustSimBackend.API.dll"]
