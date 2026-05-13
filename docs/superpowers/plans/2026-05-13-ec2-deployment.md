# EC2 Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deploy HushStore lên AWS EC2 với Docker + nginx + Let's Encrypt SSL, đồng thời vá lỗ hổng bảo mật (credentials bị lộ trong git).

**Architecture:** API ASP.NET Core chạy trong Docker container (port 8080), nginx host-level làm reverse proxy + SSL termination cho cả `api.yourdomain.com` (→ container) và `yourdomain.com` (→ Blazor WASM static files). Database dùng AWS RDS SQL Server (external).

**Tech Stack:** .NET 10, Docker / Docker Compose v2, nginx, Certbot/Let's Encrypt, ASP.NET Core environment variables, Blazor WASM

---

## ⚠️ Cảnh báo bảo mật trước khi bắt đầu

`Infrastructure/db/.env` (chứa `SA_PASSWORD=HushStore@Secure2026!`) và `src/API/appsettings.json` (chứa DB credentials + JWT secret) đang được commit trong git history. Sau khi hoàn thành plan này, nên đổi toàn bộ passwords/secrets và xem xét purge git history nếu repo là public.

---

## File Map

| File | Hành động |
|------|-----------|
| `.gitignore` | Sửa: thêm `.env`, `*.env`, `Infrastructure/db/.env` |
| `src/API/appsettings.json` | Sửa: xóa hardcoded credentials |
| `src/Client/wwwroot/appsettings.json` | Tạo: API URL cho production |
| `src/Client/wwwroot/appsettings.Development.json` | Tạo: API URL cho local dev |
| `src/Client/Program.cs` | Sửa: đọc API URL từ config thay vì hardcode |
| `src/API/Program.cs` | Sửa: CORS đọc từ config, thêm auto-migrate |
| `Dockerfile` | Tạo: multi-stage build cho API |
| `.dockerignore` | Tạo: loại file không cần thiết |
| `docker-compose.yml` | Tạo: production compose |
| `.env.example` | Tạo: template biến môi trường |
| `nginx/hushstore.conf` | Tạo: nginx virtual host config |
| `deploy.sh` | Tạo: script deploy thủ công |
| `README.md` | Viết lại: hướng dẫn deploy đầy đủ |

---

## Task 1: Vá lỗ hổng bảo mật — .gitignore + appsettings.json

**Files:**
- Modify: `.gitignore`
- Modify: `src/API/appsettings.json`

> Không có test tự động trong project này. Verification là chạy lệnh kiểm tra sau mỗi task.

- [ ] **Step 1: Cập nhật .gitignore**

Thêm các dòng sau vào cuối file `.gitignore` (sau dòng `src/API/appsettings.Development.json`):

```gitignore
# Secrets — không bao giờ commit
.env
*.env
Infrastructure/db/.env
src/API/appsettings.Production.json
```

- [ ] **Step 2: Untrack file .env đang bị track**

```bash
git rm --cached Infrastructure/db/.env
```

Expected output:
```
rm 'Infrastructure/db/.env'
```

- [ ] **Step 3: Xóa hardcoded credentials trong appsettings.json**

Thay toàn bộ nội dung `src/API/appsettings.json` thành:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "JwtSettings": {
    "SecretKey": "",
    "Issuer": "HushStoreAPI",
    "Audience": "HushStoreBlazorClient",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "AwsSettings": {
    "AccessKeyId": "",
    "SecretAccessKey": "",
    "BucketName": "hushstore-images",
    "Region": "ap-southeast-1"
  },
  "AllowedOrigins": "https://yourdomain.com"
}
```

- [ ] **Step 4: Kiểm tra không còn secrets trong tracked files**

```bash
git diff --cached src/API/appsettings.json
git status Infrastructure/db/.env
```

Expected: `Infrastructure/db/.env` xuất hiện trong "Changes to be committed" (bị untrack), không còn thấy credentials trong appsettings.json.

- [ ] **Step 5: Commit**

```bash
git add .gitignore src/API/appsettings.json
git commit -m "security: remove hardcoded credentials from tracked files"
```

---

## Task 2: Cập nhật CORS và thêm auto-migration trong API Program.cs

**Files:**
- Modify: `src/API/Program.cs`

- [ ] **Step 1: Cập nhật CORS để đọc origins từ config**

Trong `src/API/Program.cs`, tìm đoạn CORS (dòng 41-49) và thay thế bằng:

```csharp
// CORS: Cho phép Frontend (Blazor WASM) gọi API
var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',')
    ?? ["http://localhost:5214", "https://localhost:7107"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

- [ ] **Step 2: Thêm auto-migration khi khởi động**

Tìm dòng `var app = builder.Build();` (khoảng dòng 172) và chèn NGAY SAU nó:

```csharp
var app = builder.Build();

// Auto-apply EF Core migrations khi khởi động — chỉ chạy trên Production
// (tránh lỗi khi dev chạy local với DB chưa up)
if (app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HushStoreDbContext>();
    await db.Database.MigrateAsync();
}
```

- [ ] **Step 3: Kiểm tra build thành công**

```bash
dotnet build src/API/API.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/API/Program.cs
git commit -m "feat: read CORS origins from config, add auto-migration on startup"
```

---

## Task 3: Cấu hình API URL động cho Blazor WASM Client

**Files:**
- Create: `src/Client/wwwroot/appsettings.json`
- Create: `src/Client/wwwroot/appsettings.Development.json`
- Modify: `src/Client/Program.cs`

- [ ] **Step 1: Tạo wwwroot/appsettings.json (production)**

Tạo file `src/Client/wwwroot/appsettings.json`:

```json
{
  "ApiBaseUrl": "https://api.yourdomain.com"
}
```

- [ ] **Step 2: Tạo wwwroot/appsettings.Development.json (local dev)**

Tạo file `src/Client/wwwroot/appsettings.Development.json`:

```json
{
  "ApiBaseUrl": "https://localhost:7010"
}
```

- [ ] **Step 3: Cập nhật Client Program.cs để đọc ApiBaseUrl từ config**

Trong `src/Client/Program.cs`, tìm đoạn HttpClient registration (dòng 36-39):

```csharp
builder.Services.AddHttpClient("HushStoreAPI", client =>
{
    client.BaseAddress = new Uri("https://localhost:7010");
}).AddHttpMessageHandler<AuthHeaderHandler>();
```

Thay thành:

```csharp
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl chưa được cấu hình trong wwwroot/appsettings.json.");

builder.Services.AddHttpClient("HushStoreAPI", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<AuthHeaderHandler>();
```

- [ ] **Step 4: Build Client để xác nhận không lỗi**

```bash
dotnet build src/Client/Client.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/Client/wwwroot/appsettings.json src/Client/wwwroot/appsettings.Development.json src/Client/Program.cs
git commit -m "feat: read API base URL from config for Blazor WASM"
```

---

## Task 4: Tạo Dockerfile cho API (multi-stage build)

**Files:**
- Create: `Dockerfile`
- Create: `.dockerignore`

- [ ] **Step 1: Tạo .dockerignore**

Tạo file `.dockerignore` tại root repo:

```dockerignore
# Build output
**/bin/
**/obj/

# IDE
.idea/
.vscode/
.DS_Store

# Secrets
.env
*.env

# Docs (không cần trong image)
docs/
AI_context/
scratch/
Infrastructure/

# Git
.git/
.gitignore

# Client (không build trong API image)
src/Client/
```

- [ ] **Step 2: Tạo Dockerfile**

Tạo file `Dockerfile` tại root repo:

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files và restore (tận dụng Docker layer cache)
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Core/Core.csproj src/Core/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Service/Service.csproj src/Service/
COPY src/API/API.csproj src/API/
RUN dotnet restore src/API/API.csproj

# Copy toàn bộ source và publish
COPY src/ src/
RUN dotnet publish src/API/API.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Không chạy với root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "API.dll"]
```

- [ ] **Step 3: Verify build Docker image thành công**

```bash
docker build -t hushstore-api:test .
```

Expected: `Successfully built <image_id>` và `Successfully tagged hushstore-api:test`

- [ ] **Step 4: Xóa test image**

```bash
docker rmi hushstore-api:test
```

- [ ] **Step 5: Commit**

```bash
git add Dockerfile .dockerignore
git commit -m "feat: add multi-stage Dockerfile for API"
```

---

## Task 5: Tạo docker-compose.yml và .env.example

**Files:**
- Create: `docker-compose.yml`
- Create: `.env.example`

- [ ] **Step 1: Tạo .env.example**

Tạo file `.env.example` tại root repo:

```bash
# =============================================================
# HushStore — Production Environment Variables
# Copy file này thành .env và điền giá trị thật
# KHÔNG bao giờ commit file .env lên git
# =============================================================

# ---- Database (AWS RDS SQL Server) ----
# Lấy endpoint từ AWS Console > RDS > your-instance > Endpoint
ConnectionStrings__DefaultConnection=Server=<RDS_ENDPOINT>,1433;Database=HushStoreDB;User Id=<DB_USER>;Password=<DB_PASSWORD>;TrustServerCertificate=True;

# ---- JWT Secret ----
# Tạo chuỗi ngẫu nhiên 256-bit: openssl rand -base64 32
JwtSettings__SecretKey=<REPLACE_WITH_RANDOM_256BIT_STRING>

# ---- AWS S3 (image storage) ----
AwsSettings__AccessKeyId=<YOUR_IAM_ACCESS_KEY_ID>
AwsSettings__SecretAccessKey=<YOUR_IAM_SECRET_ACCESS_KEY>

# ---- CORS: production domain (dùng dấu phẩy nếu nhiều origins) ----
AllowedOrigins=https://yourdomain.com
```

- [ ] **Step 2: Tạo docker-compose.yml**

Tạo file `docker-compose.yml` tại root repo:

```yaml
services:
  api:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: hushstore_api
    ports:
      - "8080:8080"
    env_file:
      - .env
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

- [ ] **Step 3: Thêm health check endpoint đơn giản trong API**

Trong `src/API/Program.cs`, ngay trước dòng `app.MapControllers();`, thêm:

```csharp
// Health check endpoint cho Docker
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();
```

- [ ] **Step 4: Build lại để xác nhận**

```bash
dotnet build src/API/API.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add docker-compose.yml .env.example src/API/Program.cs
git commit -m "feat: add docker-compose, .env.example, health check endpoint"
```

---

## Task 6: Tạo nginx config

**Files:**
- Create: `nginx/hushstore.conf`

- [ ] **Step 1: Tạo thư mục và file nginx config**

Tạo file `nginx/hushstore.conf`:

```nginx
# =============================================================
# HushStore nginx config
# Thay "yourdomain.com" bằng domain thật của bạn
# Deploy: sudo cp nginx/hushstore.conf /etc/nginx/sites-available/hushstore
#         sudo ln -s /etc/nginx/sites-available/hushstore /etc/nginx/sites-enabled/
#         sudo nginx -t && sudo systemctl reload nginx
# =============================================================

# ── Blazor WASM (Static files) ────────────────────────────────
server {
    listen 80;
    server_name yourdomain.com www.yourdomain.com;
    return 301 https://yourdomain.com$request_uri;
}

server {
    listen 443 ssl http2;
    server_name yourdomain.com www.yourdomain.com;

    ssl_certificate     /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers on;

    root  /var/www/hushstore/wwwroot;
    index index.html;

    # Blazor SPA routing — mọi path 404 đều trả index.html
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache static assets
    location ~* \.(js|css|wasm|woff2?|png|jpg|ico|svg)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}

# ── ASP.NET Core API (Reverse Proxy) ─────────────────────────
server {
    listen 80;
    server_name api.yourdomain.com;
    return 301 https://api.yourdomain.com$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.yourdomain.com;

    ssl_certificate     /etc/letsencrypt/live/api.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.yourdomain.com/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers on;

    client_max_body_size 20M;

    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 120s;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add nginx/hushstore.conf
git commit -m "feat: add nginx virtual host config for production"
```

---

## Task 7: Tạo deploy.sh

**Files:**
- Create: `deploy.sh`

- [ ] **Step 1: Tạo deploy.sh**

Tạo file `deploy.sh` tại root repo:

```bash
#!/usr/bin/env bash
# =============================================================
# HushStore — Deploy script (chạy trên EC2)
# Usage: bash deploy.sh
# Prerequisites: Docker, Docker Compose v2, .NET SDK 10, nginx
# =============================================================

set -euo pipefail

REPO_DIR="$(cd "$(dirname "$0")" && pwd)"
CLIENT_WWWROOT="/var/www/hushstore/wwwroot"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  HushStore Deploy — $(date '+%Y-%m-%d %H:%M:%S')"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# 1. Pull latest code
echo "[1/4] Pulling latest code..."
git -C "$REPO_DIR" pull origin main

# 2. Build Blazor WASM client
echo "[2/4] Building Blazor WASM client..."
TEMP_PUBLISH=$(mktemp -d)
dotnet publish "$REPO_DIR/src/Client/Client.csproj" \
    -c Release \
    -o "$TEMP_PUBLISH" \
    --nologo -v q

# Deploy static files
sudo mkdir -p "$CLIENT_WWWROOT"
sudo rsync -a --delete "$TEMP_PUBLISH/wwwroot/" "$CLIENT_WWWROOT/"
rm -rf "$TEMP_PUBLISH"
echo "  → Static files deployed to $CLIENT_WWWROOT"

# 3. Build & restart API container
echo "[3/4] Building and restarting API container..."
docker compose -f "$REPO_DIR/docker-compose.yml" build api
docker compose -f "$REPO_DIR/docker-compose.yml" up -d api
echo "  → API container restarted"

# 4. Verify health check
echo "[4/4] Checking API health..."
sleep 5
if curl -sf http://localhost:8080/health > /dev/null; then
    echo "  → API is healthy ✓"
else
    echo "  ✗ API health check FAILED — kiểm tra logs:"
    echo "    docker compose logs api --tail=50"
    exit 1
fi

echo ""
echo "✓ Deploy hoàn thành!"
```

- [ ] **Step 2: Commit**

```bash
git add deploy.sh
git commit -m "feat: add manual deploy script for EC2"
```

---

## Task 8: Viết lại README.md

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Viết lại README.md**

Thay toàn bộ nội dung `README.md` bằng:

```markdown
# HushStore — IT Hardware E-commerce & Management System

Hệ thống thương mại điện tử và quản lý phần cứng IT, xây dựng trên ASP.NET Core 10 (API) + Blazor WebAssembly (Frontend) + SQL Server 2025.

---

## Kiến trúc

    EC2 Instance
    ├── nginx (SSL termination + reverse proxy)
    │   ├── yourdomain.com      → Blazor WASM static files
    │   └── api.yourdomain.com  → ASP.NET Core API (Docker)
    ├── Docker: hushstore_api (port 8080)
    └── AWS RDS SQL Server (external)

---

## Deploy lên AWS EC2 (lần đầu)

### 1. Chuẩn bị EC2

```bash
# Cài dependencies (Ubuntu 22.04+)
sudo apt update && sudo apt upgrade -y
sudo apt install -y nginx certbot python3-certbot-nginx docker.io rsync

# Cài .NET SDK 10 (để build Blazor WASM)
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 10.0
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.bashrc && source ~/.bashrc

# Cài Docker Compose v2
sudo apt install -y docker-compose-plugin
sudo systemctl enable --now docker
sudo usermod -aG docker $USER  # logout & login lại để có hiệu lực
```

### 2. Clone repo & cấu hình secrets

```bash
git clone https://github.com/<your-org>/PBL3.git /opt/hushstore
cd /opt/hushstore

# Tạo file .env từ template
cp .env.example .env
nano .env   # Điền RDS endpoint, JWT secret, AWS credentials
```

### 3. Cấu hình API URL cho Blazor WASM

Sửa `src/Client/wwwroot/appsettings.json`:
```json
{
  "ApiBaseUrl": "https://api.yourdomain.com"
}
```

### 4. Cấu hình nginx

```bash
sudo cp nginx/hushstore.conf /etc/nginx/sites-available/hushstore
# Thay "yourdomain.com" bằng domain thật:
sudo sed -i 's/yourdomain.com/your-actual-domain.com/g' /etc/nginx/sites-available/hushstore

sudo ln -s /etc/nginx/sites-available/hushstore /etc/nginx/sites-enabled/
sudo nginx -t   # Kiểm tra config hợp lệ

# Tạm thời dùng HTTP để lấy cert (certbot sẽ tự sửa config)
sudo nginx -s reload
```

### 5. Lấy SSL certificate (Let's Encrypt)

```bash
sudo certbot --nginx -d yourdomain.com -d www.yourdomain.com -d api.yourdomain.com
```

> Certbot sẽ tự động cập nhật nginx config với SSL. Sau đó: `sudo systemctl reload nginx`

### 6. Deploy lần đầu

```bash
cd /opt/hushstore
bash deploy.sh
```

### 7. Kiểm tra

- **Frontend:** Mở `https://yourdomain.com` → trang chủ load
- **API:** `curl https://api.yourdomain.com/health` → `{"status":"healthy"}`
- **Login:** Đăng nhập với tài khoản test → nhận JWT token thành công

---

## Update sau khi có thay đổi code

```bash
cd /opt/hushstore && bash deploy.sh
```

---

## AWS Security Groups (EC2)

| Port | Protocol | Source | Mục đích |
|------|----------|--------|----------|
| 22   | TCP | Your IP only | SSH |
| 80   | TCP | 0.0.0.0/0 | HTTP (redirect → HTTPS) |
| 443  | TCP | 0.0.0.0/0 | HTTPS |

> Port 8080 (API container) **không mở** ra internet — nginx proxy nội bộ.

---

## Development (local)

```bash
# Khởi động SQL Server
docker-compose -f Infrastructure/db/docker-compose.yml up -d

# Chạy API (https://localhost:7010)
dotnet run --project src/API/API.csproj

# Chạy Blazor WASM (https://localhost:7107)
dotnet run --project src/Client/Client.csproj

# Tạo migration mới
dotnet ef migrations add <TênMigration> --project src/Infrastructure --startup-project src/API

# Áp dụng migrations
dotnet ef database update --project src/Infrastructure --startup-project src/API
```

---

## Troubleshooting

**API container không start:**
```bash
docker compose logs api --tail=100
```

**Lỗi migration khi startup:**
```bash
# Xem log để biết lỗi cụ thể
docker compose logs api | grep -i "migration\|error"
```

**nginx 502 Bad Gateway:**
```bash
# Kiểm tra container có running không
docker compose ps
# Restart
docker compose restart api
```

**SSL cert hết hạn:**
```bash
sudo certbot renew --dry-run  # Test trước
sudo certbot renew
```
```

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: rewrite README with complete EC2 deployment guide"
```

---

## Task 9: Verify toàn bộ trên local trước khi deploy

- [ ] **Step 1: Build toàn bộ solution**

```bash
dotnet build PBL3.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 2: Test Docker build**

```bash
docker build -t hushstore-api:local .
```

Expected: `Successfully built` và không có lỗi

- [ ] **Step 3: Test container chạy (không có .env thật — chỉ test startup)**

```bash
docker run --rm -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__DefaultConnection="Server=localhost" \
  -e JwtSettings__SecretKey="test-key-only-for-smoke-test-not-real" \
  -p 8080:8080 hushstore-api:local &
sleep 5
curl -sf http://localhost:8080/health && echo "✓ Health check passed"
kill %1 2>/dev/null || true
docker rmi hushstore-api:local
```

Expected: `{"status":"healthy",...}` và `✓ Health check passed`

- [ ] **Step 4: Commit cuối và push**

```bash
git log --oneline -8   # Xem lại các commits trong task này
git push origin module-RMA
```

---

## Checklist deploy trên EC2 (sau khi push)

Chạy các bước này trên EC2 sau khi hoàn thành plan:

- [ ] EC2 instance đang chạy, Security Groups mở port 22/80/443
- [ ] RDS SQL Server đã tạo, VPC security group cho phép EC2 kết nối port 1433
- [ ] DNS records: `yourdomain.com` và `api.yourdomain.com` trỏ vào IP public của EC2
- [ ] File `.env` đã điền đúng RDS endpoint + credentials + JWT secret mới
- [ ] `src/Client/wwwroot/appsettings.json` đã sửa sang domain thật
- [ ] `bash deploy.sh` chạy thành công
- [ ] `curl https://api.yourdomain.com/health` trả về HTTP 200
- [ ] Mở `https://yourdomain.com` → giao diện hiện, login được
