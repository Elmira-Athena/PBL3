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
