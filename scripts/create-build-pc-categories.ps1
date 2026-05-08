# Creates the 16 Build PC categories via the HushStore API
# Usage: .\scripts\create-build-pc-categories.ps1
# Requires PowerShell 5+ (Windows) or PowerShell 7+ (cross-platform)
# NOTE: All Vietnamese strings are built from Unicode code points to avoid
#       encoding issues when the file is read on different Windows locales.

$API = "https://localhost:7010"

# ------------------------------------------------------------------
# Skip self-signed certificate validation (localhost dev only)
# ------------------------------------------------------------------
if ($PSVersionTable.PSVersion.Major -ge 6) {
    $PSDefaultParameterValues['Invoke-RestMethod:SkipCertificateCheck'] = $true
} else {
    Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCerts : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint sp, X509Certificate cert,
        WebRequest req, int problem) { return true; }
}
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCerts
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
}

# ------------------------------------------------------------------
# Helper: build a string from an array of Unicode code points
# ------------------------------------------------------------------
function From-Codepoints([int[]] $pts) {
    ($pts | ForEach-Object { [char]$_ }) -join ""
}

# ------------------------------------------------------------------
# Category data (names encoded as code-point arrays; slugs are ASCII)
# ------------------------------------------------------------------
$categories = @(
    @{ name = (From-Codepoints 66,7897,32,118,105,32,120,7917,32,108,253)       ; slug = "cpu"                ; sort = 1  }
    @{ name = (From-Codepoints 66,111,32,109,7841,99,104,32,99,104,7911)         ; slug = "bo-mach-chu"        ; sort = 2  }
    @{ name = "RAM"                                                               ; slug = "ram"                ; sort = 3  }
    @{ name = "HDD"                                                               ; slug = "hdd"                ; sort = 4  }
    @{ name = "SSD"                                                               ; slug = "ssd"                ; sort = 5  }
    @{ name = "VGA"                                                               ; slug = "vga"                ; sort = 6  }
    @{ name = (From-Codepoints 78,103,117,7891,110)                              ; slug = "nguon"              ; sort = 7  }
    @{ name = (From-Codepoints 86,7887,32,67,97,115,101)                        ; slug = "vo-case"            ; sort = 8  }
    @{ name = "Fan Case"                                                          ; slug = "fan-case"           ; sort = 9  }
    @{ name = (From-Codepoints 77,224,110,32,104,236,110,104)                   ; slug = "man-hinh"           ; sort = 10 }
    @{ name = (From-Codepoints 67,104,117,7897,116)                             ; slug = "chuot"              ; sort = 11 }
    @{ name = (From-Codepoints 66,224,110,32,112,104,237,109)                   ; slug = "ban-phim"           ; sort = 12 }
    @{ name = (From-Codepoints 84,7843,110,32,110,104,105,7879,116,32,107,104,237) ; slug = "tan-nhiet-khi"   ; sort = 13 }
    @{ name = (From-Codepoints 84,7843,110,32,110,104,105,7879,116,32,110,432,7899,99,32,65,73,79) ; slug = "tan-nhiet-nuoc-aio" ; sort = 14 }
    @{ name = "Tai nghe"                                                          ; slug = "tai-nghe"          ; sort = 15 }
    @{ name = (From-Codepoints 80,104,7847,110,32,109,7873,109)                 ; slug = "phan-mem"          ; sort = 16 }
)

# ------------------------------------------------------------------
# Login
# ------------------------------------------------------------------
Write-Host ">> Logging in..."
$loginBody = '{"email":"admin@hushstore.com","password":"Admin@123"}'

try {
    $loginResp = Invoke-RestMethod -Uri "$API/api/auth/login" -Method POST `
        -Body $loginBody -ContentType "application/json"
} catch {
    Write-Host "ERROR: Cannot reach API at $API. Make sure the API is running." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

$token = $loginResp.data.accessToken
if (-not $token) {
    Write-Host "ERROR: Login failed. Check credentials." -ForegroundColor Red
    exit 1
}

Write-Host ">> Login OK." -ForegroundColor Green
$headers = @{ Authorization = "Bearer $token" }

# ------------------------------------------------------------------
# Create categories
# ------------------------------------------------------------------
Write-Host ">> Creating categories..."
$ok = 0
$skip = 0

foreach ($cat in $categories) {
    $bodyObj = @{
        name      = $cat.name
        slug      = $cat.slug
        sortOrder = $cat.sort
        isVisible = $true
    }
    $body = $bodyObj | ConvertTo-Json -Compress
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($body)

    try {
        $resp = Invoke-RestMethod -Uri "$API/api/categories" -Method POST `
            -Body $bodyBytes -ContentType "application/json; charset=utf-8" `
            -Headers $headers

        if ($resp.success) {
            Write-Host ("  [OK]   " + $cat.name + " (" + $cat.slug + ")") -ForegroundColor Green
            $ok++
        } else {
            Write-Host ("  [SKIP] " + $cat.name + " (" + $cat.slug + ") - " + $resp.message) -ForegroundColor Yellow
            $skip++
        }
    } catch {
        $errMsg = $_.Exception.Message
        Write-Host ("  [ERR]  " + $cat.name + " (" + $cat.slug + ") - " + $errMsg) -ForegroundColor Red
        $skip++
    }
}

Write-Host ""
Write-Host (">> Done: " + $ok + " created, " + $skip + " skipped/failed.") -ForegroundColor Cyan
