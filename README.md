# 🛡️ TrustSimBackend

Hệ thống Backend cho nền tảng Đấu giá, Định giá và Chuyển nhượng SIM số đẹp số hóa **TrustSim**.  
Dự án được xây dựng theo kiến trúc **Clean Architecture** kết hợp mô hình **CQRS (MediatR)** trên nền tảng **.NET 8**.

---

## 📋 Mục Lục

1. [Yêu Cầu Hệ Thống (Prerequisites)](#-yêu-cầu-hệ-thống-prerequisites)
2. [Cấu Trúc Giải Pháp (Solution Architecture)](#-cấu-trúc-giải-pháp-solution-architecture)
3. [Hướng Dẫn Khởi Chạy Dự Án (Getting Started)](#-hướng-dẫn-khởi-chạy-dự-án-getting-started)
4. [Quản Lý Cơ Sở Dữ Liệu (EF Core Migrations)](#-quản-lý-cơ-sở-dữ-liệu-ef-core-migrations)
5. [Các Lệnh Tự Động Hóa & AI Scripts](#-các-lệnh-tự-động-hóa--ai-scripts)
   - [Sinh Architecture Repo Map (Aider Algorithm)](#1-sinh-architecture-repo-map-cho-ai-agent)
   - [Nạp Dữ Liệu RAG Knowledge Ingestion](#2-tự-động-nạp-dữ-liệu-rag-tri-thức)
6. [Quy Chuẩn Hoạt Động Của AI Agent](#-quy-chuẩn-hoạt-động-của-ai-agent)

---

## ⚙️ Yêu Cầu Hệ Thống (Prerequisites)

- **.NET SDK**: `8.0.401` trở lên (xác thực qua [global.json](file:///d:/PersonalProject/TrustSim/TrustSimBackend/global.json))
- **Cơ sở dữ liệu**: Microsoft SQL Server / LocalDB hoặc PostgreSQL (theo cấu hình `appsettings.json`)
- **Python**: `3.10+` (dành cho các script AI và sinh Repo Map)
- **PowerShell**: `5.1` hoặc `PowerShell 7+`

---

## 🏛️ Cấu Trúc Giải Pháp (Solution Architecture)

```
TrustSimBackend/
├── TrustSimBackend.Domain/          # Tầng thực thể nghiệp vụ: Entities, Enums, Value Objects, Domain Events
├── TrustSimBackend.Application/     # Tầng ứng dụng: CQRS (Commands/Queries), MediatR Handlers, DTOs, Mappings
├── TrustSimBackend.Infrastructure/  # Tầng hạ tầng: EF Core DbContext, Repositories, Background Workers, Services
├── TrustSimBackend.API/             # Tầng giao diện API: Controllers, Filters, Middlewares, Program.cs
├── docs/                            # Tài liệu hệ thống, feedbacks, báo cáo & architecture repo map
├── scripts/                         # Script tự động hóa (Repo Map Generator, RAG Ingestion)
└── .agents/                         # Cấu hình Rules & Skills dành cho AI Agent
```

---

## 🚀 Hướng Dẫn Khởi Chạy Dự Án (Getting Started)

### 1. Khôi phục thư viện & Biên dịch dự án
```powershell
# Khôi phục các gói NuGet
dotnet restore

# Biên dịch toàn bộ solution
dotnet build
```

### 2. Khởi chạy Web API
```powershell
# Chạy với cấu hình HTTP (Cổng mặc định: http://localhost:5130)
dotnet run --project TrustSimBackend.API --launch-profile http

# Hoặc chạy với cấu hình HTTPS (Cổng mặc định: https://localhost:7120)
dotnet run --project TrustSimBackend.API --launch-profile https
```

- **Swagger UI**: Mở trình duyệt tại [http://localhost:5130/swagger](http://localhost:5130/swagger)
- **Health Check**: Truy cập [http://localhost:5130/health](http://localhost:5130/health)

---

## 🗄️ Quản Lý Cơ Sở Dữ Liệu (EF Core Migrations)

Cần cài đặt công cụ EF Core CLI nếu chưa có:
```powershell
dotnet tool install --global dotnet-ef
```

### Các lệnh Migration thường dùng:

```powershell
# 1. Thêm một migration mới
dotnet ef migrations add <TenMigration> --project TrustSimBackend.Infrastructure --startup-project TrustSimBackend.API

# 2. Cập nhật migration vào Database
dotnet ef database update --project TrustSimBackend.Infrastructure --startup-project TrustSimBackend.API

# 3. Rollback về một migration cụ thể trước đó
dotnet ef database update <TenMigrationTruoc> --project TrustSimBackend.Infrastructure --startup-project TrustSimBackend.API

# 4. Xóa migration mới nhất (nếu chưa áp dụng vào DB)
dotnet ef migrations remove --project TrustSimBackend.Infrastructure --startup-project TrustSimBackend.API

# 5. Xuất SQL script từ migration để review
dotnet ef migrations script --project TrustSimBackend.Infrastructure --startup-project TrustSimBackend.API -o docs/migrations_script.sql
```

---

## 🤖 Các Lệnh Tự Động Hóa & AI Scripts

### 1. Sinh Architecture Repo Map Cho AI Agent
Dự án tích hợp thuật toán **Aider Repo Map** sử dụng **Tree-sitter AST** + **PageRank** + **Tiktoken** để nén toàn bộ 900+ file mã nguồn C# thành một bản đồ kiến trúc trọng tâm vừa vặn context window của AI:

```powershell
# Bước 1: Cài đặt thư viện Python cần thiết (chỉ cần chạy lần đầu)
pip install tree-sitter tree-sitter-c-sharp networkx tiktoken

# Bước 2: Chạy sinh Repo Map mặc định (ngân sách 3500 tokens)
python scripts/generate_repo_map.py

# Tùy chọn 1: Mở rộng ngân sách token cho AI đọc sâu hơn (ví dụ: 5000 tokens)
python scripts/generate_repo_map.py --tokens 5000

# Tùy chọn 2: Hiển thị toàn bộ file không giới hạn ngân sách
python scripts/generate_repo_map.py --all-files

# Tùy chọn 3: Thay đổi đường dẫn file đầu ra
python scripts/generate_repo_map.py --output "docs/architecture/custom_repo_map.md"
```
> Kết quả được lưu tại: [docs/architecture/repo_map.md](file:///d:/PersonalProject/TrustSim/TrustSimBackend/docs/architecture/repo_map.md)

---

### 2. Tự Động Nạp Dữ Liệu RAG Tri Thức (Knowledge Ingestion)
Script PowerShell tự động crawl nội dung từ tài liệu Google Docs, băm nhỏ chunk và nạp vào vector database cho tính năng AI Valuation & Consultant:

```powershell
# Chạy với cấu hình mặc định (BaseUrl: http://localhost:5130, User: admin)
powershell -ExecutionPolicy Bypass -File scripts/IngestKnowledgeDocs.ps1

# Hoặc truyền tham số tùy biến:
powershell -ExecutionPolicy Bypass -File scripts/IngestKnowledgeDocs.ps1 `
    -BaseUrl "http://localhost:5130" `
    -AdminUsername "admin" `
    -AdminPassword "YourStrongPassword@"
```

---

## 🛡️ Quy Chuẩn Hoạt Động Của AI Agent

Khi làm việc trong workspace này, mọi AI Assistant phải tuân thủ nghiêm ngặt các quy tắc trong [AGENTS.md](file:///d:/PersonalProject/TrustSim/TrustSimBackend/AGENTS.md):
1. **User Approval First**: Luôn phân tích, đề xuất kế hoạch (`implementation_plan.md`) và chờ người dùng bấm **Accept** trước khi can thiệp vào mã nguồn.
2. **Clean Architecture**: Tuân thủ hướng phụ thuộc một chiều `Domain` $\leftarrow$ `Application` $\leftarrow$ `Infrastructure` $\leftarrow$ `API`.
3. **CQRS & MediatR**: Tách biệt luồng Commands và Queries.
4. **Không dùng Fully Qualified Namespaces Inline**: Đưa toàn bộ namespace lên chỉ thị `using` ở đầu file.
5. **Đọc Repo Map Trước Khi Khảo Sát**: Khuyến nghị đọc file [docs/architecture/repo_map.md](file:///d:/PersonalProject/TrustSim/TrustSimBackend/docs/architecture/repo_map.md) để nắm cấu trúc tổng thể trước khi tra cứu file.
