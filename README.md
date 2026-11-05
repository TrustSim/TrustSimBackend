# 🛡️ TrustSimBackend

Hệ thống Backend cho nền tảng Đấu giá, Định giá và Chuyển nhượng SIM số đẹp số hóa **TrustSim**.  
Dự án được xây dựng theo kiến trúc **Clean Architecture** kết hợp mô hình **CQRS (MediatR)** trên nền tảng **.NET 8**.

---

## 📋 Mục Lục

1. [Yêu Cầu Hệ Thống (Prerequisites)](#-yêu-cầu-hệ-thống-prerequisites)
2. [Cấu Trúc Giải Pháp (Solution Architecture)](#-cấu-trúc-giải-pháp-solution-architecture)
3. [Hướng Dẫn Khởi Chạy Dự Án (Getting Started)](#-hướng-dẫn-khởi-chạy-dự-án-getting-started)
   - [Cách 1: Khởi Chạy Trọn Gói Bằng Docker Compose (Khuyến nghị)](#-cách-1-khởi-chạy-trọn-gói-bằng-docker-compose-khuyến-nghị)
   - [Cách 2: Chạy Database Trên Docker + Khởi Chạy BE Cục Bộ](#-cách-2-chạy-database-trên-docker--khởi-chạy-be-cục-bộ-dành-cho-lập-trình--debug)
   - [Truy Cập Giao Diện Quản Lý (Management UIs)](#-truy-cập-giao-diện-quản-lý-dịch-vụ-management-uis)
   - [Các Lệnh Tiện Ích Cho Docker](#5-các-lệnh-tiện-ích-cho-docker)
4. [Quản Lý Cơ Sở Dữ Liệu (EF Core Migrations)](#-quản-lý-cơ-sở-dữ-liệu-ef-core-migrations)
5. [Các Lệnh Tự Động Hóa & AI Scripts](#-các-lệnh-tự-động-hóa--ai-scripts)
   - [Sinh Architecture Repo Map (Aider Algorithm)](#1-sinh-architecture-repo-map-cho-ai-agent)
   - [Nạp Dữ Liệu RAG Knowledge Ingestion](#2-tự-động-nạp-dữ-liệu-rag-tri-thức)
   - [Quản Lý Codebase Knowledge Graph (Graphify)](#3-quản-lý-codebase-knowledge-graph-graphify)
6. [Quy Chuẩn Hoạt Động Của AI Agent](#-quy-chuẩn-hoạt-động-của-ai-agent)

---

## ⚙️ Yêu Cầu Hệ Thống (Prerequisites)

- **.NET SDK**: `8.0.401` trở lên (xác thực qua [global.json](file:///d:/PersonalProject/TrustSim/TrustSimBackend/global.json))
- **Docker & Docker Compose**: Dành cho môi trường cơ sở dữ liệu và lưu trữ cục bộ (PostgreSQL pgvector, Redis, Supabase Storage, pgAdmin, Redis Commander)
- **Cơ sở dữ liệu**: PostgreSQL 16 (chạy qua Docker Compose) hoặc Microsoft SQL Server / LocalDB
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
├── .agents/                         # Cấu hình Rules & Skills dành cho AI Agent
├── docker-compose.yml               # Cấu hình Docker Compose trọn gói (Postgres, pgAdmin, Redis, Backend API)
├── Dockerfile                       # Dockerfile đa tầng build & run TrustSimBackend.API trên .NET 8
└── .dockerignore                    # Danh sách loại trừ khi build Docker image
```

---

## 🚀 Hướng Dẫn Khởi Chạy Dự Án (Getting Started)

Dự án hỗ trợ 2 chế độ khởi chạy linh hoạt tùy theo nhu cầu:

---

### 👉 Cách 1: Khởi Chạy Trọn Gói Bằng Docker Compose (Khuyến nghị)

Trong chế độ này, Docker Compose sẽ tự động thực hiện tuần tự:
1. Khởi động **PostgreSQL (pgvector)** và **Redis**, kiểm tra trạng thái **Healthy**.
2. Khởi chạy **TrustSimBackend.API** (`backend`), tự động chạy **EF Core Migrations** (`context.Database.MigrateAsync()`) và nạp dữ liệu ban đầu (**Seed Data**) vào database.
3. Sau khi cập nhật database thành công, Backend API bắt đầu lắng nghe và phục vụ tại **cổng 5130**.

```powershell
# Khởi chạy toàn bộ dịch vụ (tự động build Backend và chạy ngầm)
docker compose up --build -d

# Kiểm tra trạng thái các container đang chạy
docker compose ps

# Theo dõi logs tiến trình chạy migration và khởi động BE
docker compose logs -f backend
```

- **Swagger UI**: Mở trình duyệt tại [http://localhost:5130/swagger](http://localhost:5130/swagger)
- **Health Check**: Truy cập [http://localhost:5130/health](http://localhost:5130/health)

---

### 👉 Cách 2: Chạy Database Trên Docker + Khởi Chạy BE Cục Bộ (Dành cho Lập Trình & Debug)

Khi cần code, debug từng dòng hoặc sử dụng Hot Reload trực tiếp trong Visual Studio / VS Code:

#### Bước 1: Khởi động Database & Cache bằng Docker Compose
```powershell
# Chỉ khởi động các dịch vụ cơ sở dữ liệu và giao diện quản trị
docker compose up -d postgres redis pgadmin redis-commander
```

#### Bước 2: Cập nhật Migration vào Database
```powershell
# Chạy migration thủ công để tạo/cập nhật bảng vào database
dotnet ef database update --project TrustSimBackend.Infrastructure --startup-project TrustSimBackend.API
```

#### Bước 3: Khởi chạy Backend API (Cổng 5130)
```powershell
# Khởi chạy Web API với profile HTTP (Cổng mặc định: http://localhost:5130)
dotnet run --project TrustSimBackend.API --launch-profile http

# Hoặc khởi chạy với profile HTTPS (Cổng mặc định: https://localhost:7120)
dotnet run --project TrustSimBackend.API --launch-profile https
```

---

### 🌐 Truy cập Giao diện Quản lý Dịch vụ (Management UIs)

| Dịch vụ / Giao diện | Địa chỉ truy cập | Cổng | Tài khoản / Mật khẩu mặc định | Chức năng chính |
| :--- | :--- | :--- | :--- | :--- |
| **TrustSim Web API / Swagger** | [http://localhost:5130/swagger](http://localhost:5130/swagger) | **`5130`** | — | Giao diện kiểm thử toàn bộ API nghiệp vụ |
| **pgAdmin 4 (Postgres UI)** | [http://localhost:5050](http://localhost:5050) | `5050` | `admin@trustsim.com` / `admin` | Quản lý bảng, dữ liệu, truy vấn SQL trên PostgreSQL |
| **Redis Commander (Redis UI)**| [http://localhost:8081](http://localhost:8081) | `8081` | Không cần mật khẩu | Quản lý keys, xem cache và message queue của Redis |
| **PostgreSQL Database** | `localhost:5433` | `5433` | `postgres` / `postgres` (DB: `trustsim_db`) | Kết nối trực tiếp qua DBeaver, DataGrip, VS Code |
| **Redis Server** | `localhost:6379` | `6379` | Không mật khẩu | Kết nối trực tiếp từ Backend & Redis CLI |
| **MinIO Console (S3 UI)** | [http://localhost:9001](http://localhost:9001) | `9001` | `minioadmin` / `minioadmin` | Giao diện quản lý Bucket, tệp hợp đồng PDF, ảnh eKYC |
| **MinIO S3 API Endpoint** | `http://localhost:9000` | `9000` | `minioadmin` / `minioadmin` | Endpoint S3 API để tải lên & tải về file trực tiếp |

#### 🔑 Hướng dẫn đăng nhập & cấu hình chi tiết:

1. **pgAdmin 4 (Web UI & Kết nối Database)**:
   - Mở trình duyệt tại [http://localhost:5050](http://localhost:5050) và đăng nhập với `admin@trustsim.com` / `admin`.
   - Chuột phải vào **Servers** $\rightarrow$ chọn **Register** $\rightarrow$ **Server...**:
     - Tab **General**: Nhập **Name**: `TrustSim Local`.
     - Tab **Connection**: 
       - **Host name/address**: `postgres` *(tên service trong mạng Docker `trustsim-net`)* hoặc `host.docker.internal`
       - **Port**: `5432` *(cổng nội bộ mạng Docker)*
       - **Maintenance database**: `trustsim_db`
       - **Username**: `postgres`
       - **Password**: `postgres`
       - Tích chọn **Save password**
     - Bấm **Save** để kết nối và bắt đầu duyệt dữ liệu.

2. **Redis Commander (Web UI)**:
   - Truy cập trực tiếp tại [http://localhost:8081](http://localhost:8081) (không cần tài khoản/mật khẩu).
   - Hệ thống tự động kết nối cụm Redis tại `local:redis:6379`. Xem keys bên cây thư mục trái (`db0`) hoặc mở Redis Console ở chân trang để chạy lệnh CLI.

3. **MinIO Console (Quản trị Tệp tin PDF & S3 Object Storage)**:
   - Truy cập giao diện quản trị tại [http://localhost:9001](http://localhost:9001).
   - Đăng nhập với tài khoản:
     - **Username**: `minioadmin`
     - **Password**: `minioadmin`
   - **Cấu trúc lưu trữ**:
     - Bucket chính: **`pdf-files`** (Hệ thống tự động khởi tạo và cấp quyền **Public Read** khi Backend chạy).
     - Thư mục **`contracts/`**: Lưu toàn bộ Văn bản 1 (Phiếu chốt giá trúng `PhieuChotGia_*.pdf`), Văn bản 2 (Hợp đồng chuyển nhượng điện tử `HopDong_*.pdf`), Văn bản 3 (Biên bản thanh lý `BienBanThanhLy_*.pdf`).
     - Thư mục **`kyc/`**: Lưu trữ ảnh chân dung, CCCD/CMND phục vụ xác minh danh tính eKYC.
   - **Xem & Tải trực tiếp file PDF**:
     - Định dạng URL công khai: `http://localhost:9000/pdf-files/contracts/<ten-file>.pdf`.
     - Trong MinIO Console, click vào bucket `pdf-files` $\rightarrow$ thư mục `contracts` $\rightarrow$ chọn file PDF để xem trước (Preview) hoặc tải về (Download).

4. **Lệnh truy cập Terminal / Shell trực tiếp vào Container (`docker exec`)**:
   ```powershell
   # Vào shell bên trong container pgAdmin:
   docker exec -it trustsim-pgadmin /bin/sh

   # Vào shell bên trong container Redis Commander:
   docker exec -it trustsim-redis-commander /bin/sh

   # Tiện ích: Vào thẳng PostgreSQL CLI (psql) trong container postgres:
   docker exec -it trustsim-postgres psql -U postgres -d trustsim_db

   # Tiện ích: Vào thẳng Redis CLI trong container redis:
   docker exec -it trustsim-redis redis-cli
   ```

---

### 5. Các Lệnh Tiện Ích Cho Docker
```powershell
# Xem logs thời gian thực của toàn bộ dịch vụ
docker compose logs -f

# Xem logs của riêng một dịch vụ (ví dụ: postgres hoặc supabase-storage)
docker compose logs -f postgres
docker compose logs -f supabase-storage

# Khởi động lại toàn bộ dịch vụ
docker compose restart

# Dừng toàn bộ dịch vụ (giữ nguyên container)
docker compose stop

# Dừng và giải phóng container (giữ nguyên dữ liệu trong Docker Volumes)
docker compose down

# Dừng và xóa sạch toàn bộ volumes (làm mới dữ liệu từ đầu)
docker compose down -v
```

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

### 3. Quản Lý Codebase Knowledge Graph (Graphify)
Dự án tích hợp **Graphify** để phân tích AST mã nguồn C#, xây dựng đồ thị tri thức (Knowledge Graph) phục vụ việc trực quan hóa kiến trúc và hỗ trợ AI Agent điều hướng, định vị quan hệ giữa các thành phần nghiệp vụ:

#### A. Khởi tạo & Trích xuất Graph lần đầu
```powershell
# 1. Trích xuất AST & dựng đồ thị (Code-only, xử lý cục bộ, không tốn API key)
graphify extract . --code-only

# 2. Xuất giao diện trực quan hóa đồ thị HTML (xem tương tác trên trình duyệt)
graphify export html

# 3. Xuất tài liệu tóm tắt Markdown Wiki (cho phép AI Agent đọc nhanh với chi phí token tối ưu)
graphify export wiki
```
> Kết quả được lưu tại thư mục: [graphify-out/](file:///d:/PersonalProject/TrustSim/TrustSimBackend/graphify-out) (gồm `graph.json`, `graph.html`, thư mục `wiki/`,...).

#### B. Cập nhật Graph khi thay đổi code
```powershell
# Cập nhật nhanh các file C# vừa chỉnh sửa/thêm mới (AST incremental update cực nhanh)
graphify update .

# Làm mới lại giao diện trực quan hóa HTML sau khi update
graphify export html
```

#### C. Tích hợp AI Agent (Google Antigravity & AI IDEs)
```powershell
# Tự động cấu hình Rule và Workflow chuẩn cho Antigravity AI Agent (.agents/rules/graphify.md)
graphify antigravity install
```

#### D. Tra cứu nhanh dành cho Lập trình viên & Agent (CLI Traversal)
```powershell
# Tra cứu subgraph theo câu hỏi/chức năng (trả về dưới 2000 tokens)
graphify query "AcceptDutchBuyCommandHandler"

# Tìm luồng liên kết/đường đi phụ thuộc giữa 2 class hoặc interface
graphify path "DoubleEntryLedgerService" "LedgerHashHelper"

# Giải thích khái niệm/cụm thành phần tập trung
graphify explain "AuctionDelayQueueDispatcher"
```

---

## 🛡️ Quy Chuẩn Hoạt Động Của AI Agent

Khi làm việc trong workspace này, mọi AI Assistant phải tuân thủ nghiêm ngặt các quy tắc trong [AGENTS.md](file:///d:/PersonalProject/TrustSim/TrustSimBackend/AGENTS.md):
1. **User Approval First**: Luôn phân tích, đề xuất kế hoạch (`implementation_plan.md`) và chờ người dùng bấm **Accept** trước khi can thiệp vào mã nguồn.
2. **Clean Architecture**: Tuân thủ hướng phụ thuộc một chiều `Domain` $\leftarrow$ `Application` $\leftarrow$ `Infrastructure` $\leftarrow$ `API`.
3. **CQRS & MediatR**: Tách biệt luồng Commands và Queries.
4. **Không dùng Fully Qualified Namespaces Inline**: Đưa toàn bộ namespace lên chỉ thị `using` ở đầu file.
5. **Đọc Repo Map & Tra Cứu Graph Trước Khi Khảo Sát**: Sử dụng lệnh `graphify query` để nắm cấu trúc tổng thể và quan hệ phụ thuộc trước khi tra cứu file nguồn chi tiết.
