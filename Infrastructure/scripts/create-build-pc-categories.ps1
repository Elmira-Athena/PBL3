# Creates the 16 Build PC categories via the HushStore API
# Usage (PowerShell 5+): .\scripts\create-build-pc-categories.ps1
# On Windows 10/11: right-click > "Run with PowerShell" or run from terminal

$API = "https://localhost:7010"

# Bỏ qua lỗi certificate tự ký (localhost)
if ($PSVersionTable.PSVersion.Major -ge 6) {
    $PSDefaultParameterValues['Invoke-RestMethod:SkipCertificateCheck'] = $true
    $PSDefaultParameterValues['Invoke-WebRequest:SkipCertificateCheck'] = $true
} else {
    Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAll : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) { return true; }
}
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAll
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
}

Write-Host ">> Đăng nhập..."

$loginBody = @{ email = "admin@hushstore.com"; password = "Admin@123" } | ConvertTo-Json
try {
    $loginResp = Invoke-RestMethod -Uri "$API/api/auth/login" -Method POST `
        -Body $loginBody -ContentType "application/json; charset=utf-8"
} catch {
    Write-Host "Lỗi: Không kết nối được tới API. Đảm bảo API đang chạy tại $API" -ForegroundColor Red
    exit 1
}

$token = $loginResp.data.accessToken
if (-not $token) {
    Write-Host "Lỗi: Không lấy được access token." -ForegroundColor Red
    exit 1
}

Write-Host ">> Đăng nhập thành công." -ForegroundColor Green
Write-Host ""

$headers = @{ Authorization = "Bearer $token" }

function Create-Category($name, $slug, $sortOrder) {
    $body = @{
        name      = $name
        slug      = $slug
        sortOrder = $sortOrder
        isVisible = $true
    } | ConvertTo-Json

    try {
        $resp = Invoke-RestMethod -Uri "$API/api/categories" -Method POST `
            -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) `
            -ContentType "application/json; charset=utf-8" `
            -Headers $headers

        if ($resp.success) {
            Write-Host "  [OK] $name ($slug)" -ForegroundColor Green
        } else {
            Write-Host "  [SKIP/ERR] $name ($slug) — $($resp.message)" -ForegroundColor Yellow
        }
    } catch {
        $msg = $_.Exception.Message
        Write-Host "  [ERR] $name ($slug) — $msg" -ForegroundColor Red
    }
}

Write-Host ">> Tạo danh mục linh kiện PC..."

Create-Category "Bộ vi xử lý"        "cpu"                  1
Create-Category "Bo mạch chủ"         "bo-mach-chu"          2
Create-Category "RAM"                  "ram"                  3
Create-Category "HDD"                  "hdd"                  4
Create-Category "SSD"                  "ssd"                  5
Create-Category "VGA"                  "vga"                  6
Create-Category "Nguồn"                "nguon"                7
Create-Category "Vỏ Case"              "vo-case"              8
Create-Category "Fan Case"             "fan-case"             9
Create-Category "Màn hình"             "man-hinh"             10
Create-Category "Chuột"                "chuot"                11
Create-Category "Bàn phím"             "ban-phim"             12
Create-Category "Tản nhiệt khí"        "tan-nhiet-khi"        13
Create-Category "Tản nhiệt nước AIO"   "tan-nhiet-nuoc-aio"   14
Create-Category "Tai nghe"             "tai-nghe"             15
Create-Category "Phần mềm"             "phan-mem"             16

Write-Host ""
Write-Host ">> Hoàn tất!" -ForegroundColor Green
