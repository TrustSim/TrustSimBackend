# ─────────────────────────────────────────────────────────────────────────────
# TrustSim - RAG Document Ingestion Automation Script
# Tự động nạp và tạo Vector Chunks thực tế từ 2 tài liệu Google Docs
# ─────────────────────────────────────────────────────────────────────────────

param (
    [string]$BaseUrl = "http://localhost:5130",
    [string]$AdminUsername = "admin",
    [string]$AdminPassword = "Password123@"
)

# Bỏ qua SSL certificate validation trong Windows PowerShell 5.1 và Core
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  TRUSTSIM AI - BẮT ĐẦU QUY TRÌNH RAG INGESTION TÀI LIỆU THỰC TẾ" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Đăng nhập với tài khoản Quản trị viên ADMIN
Write-Host "`n[1/3] Đang xác thực tài khoản Quản trị viên ($AdminUsername)..." -ForegroundColor Yellow
$loginBody = @{
    username = $AdminUsername
    password = $AdminPassword
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $loginResponse.data.accessToken
    Write-Host " -> Đăng nhập thành công! Token đã được cấp phát." -ForegroundColor Green
}
catch {
    Write-Host " -> Lỗi khi đăng nhập API: $_" -ForegroundColor Red
    Write-Host " -> Hãy đảm bảo Backend TrustSim API đang chạy tại $BaseUrl." -ForegroundColor Yellow
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

# 2. Danh sách 2 tài liệu Google Docs cần Ingest
$documents = @(
    @{
        DocumentName = "Định Giá Sim Cho AI"
        LinkUrl      = "https://docs.google.com/document/d/1_dvURkshUfTtUMLzpLTX-HwTqhT3GNVBqZ5yOQPBK_c/edit?tab=t.0"
        DocumentType = "PRICING_GUIDELINE"
    },
    @{
        DocumentName = "Tài Liệu RAG Sim Phong Thủy"
        LinkUrl      = "https://docs.google.com/document/d/1thrdHxxrJZANJoRg_mKY0C9daWMKh1YxCTTDFIpcgtc/edit?tab=t.0"
        DocumentType = "FENG_SHUI_RULE"
    }
)

# 3. Gửi lệnh Ingestion cho từng tài liệu
Write-Host "`n[2/3] Đang gửi yêu cầu nạp RAG Ingestion Pipeline..." -ForegroundColor Yellow

$ingestedIds = @()

foreach ($doc in $documents) {
    Write-Host " -> Đang kích hoạt Ingestion cho: '$($doc.DocumentName)'..." -ForegroundColor Gray
    $ingestBody = $doc | ConvertTo-Json
    
    try {
        $ingestRes = Invoke-RestMethod -Uri "$BaseUrl/api/documents/ai/ingest" -Method Post -Headers $headers -Body $ingestBody -ContentType "application/json"
        $docId = $ingestRes.data.documentId
        $ingestedIds += @{ Id = $docId; Name = $doc.DocumentName }
        Write-Host "    [OK] Document ID: $docId - Tiếp nhận thành công, đang bóc tách và embedding trong nền..." -ForegroundColor Green
    }
    catch {
        Write-Host "    [FAIL] Lỗi khi nạp $($doc.DocumentName): $_" -ForegroundColor Red
    }
}

# 4. Theo dõi tiến độ hoàn tất Vector Chunks
Write-Host "`n[3/3] Đang theo dõi tiến độ xử lý và trích xuất Vector Chunks..." -ForegroundColor Yellow

foreach ($item in $ingestedIds) {
    $statusUrl = "$BaseUrl/api/documents/ai/$($item.Id)/status"
    $maxAttempts = 15
    $attempt = 0
    $completed = $false

    while (-not $completed -and $attempt -lt $maxAttempts) {
        $attempt++
        Start-Sleep -Seconds 2
        try {
            $statusRes = Invoke-RestMethod -Uri $statusUrl -Method Get -Headers $headers
            $d = $statusRes.data
            if ($d.status -eq "INDEXED") {
                Write-Host "`n -> Tài liệu: '$($item.Name)' [INDEXED - HOÀN TẤT]" -ForegroundColor Green
                Write-Host "    Tổng Chunks:  $($d.totalChunks) đoạn chunks" -ForegroundColor White
                Write-Host "    Tổng Tokens:  $($d.totalTokens) tokens" -ForegroundColor White
                Write-Host "    Thời điểm:    $($d.crawledAt)" -ForegroundColor White
                $completed = $true
            }
            elseif ($d.status -eq "FAILED") {
                Write-Host "`n -> Tài liệu: '$($item.Name)' [FAILED - THẤT BẠI]" -ForegroundColor Red
                Write-Host "    Lỗi: $($d.errorMessage)" -ForegroundColor Red
                $completed = $true
            }
            else {
                Write-Host "    ... Đang xử lý ($($d.status))..." -ForegroundColor Gray
            }
        }
        catch {
            Write-Host " -> Chưa thể lấy trạng thái của $($item.Name): $_" -ForegroundColor Yellow
        }
    }
}

Write-Host "`n=================================================================" -ForegroundColor Green
Write-Host "  HOÀN TẤT QUY TRÌNH NẠP RAG CHO TRI THỨC AI TRUSTSIM" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
