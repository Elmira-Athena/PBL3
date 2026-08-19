# Build context là REPO ROOT, không phải src/Client.
#   docker build -f src/Client/Dockerfile -t hushstore-web:tag .

# ─── Stage 1: publish Blazor WASM ────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Client chỉ tham chiếu Shared, Shared không tham chiếu project nào.
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Client/Client.csproj src/Client/
RUN dotnet restore src/Client/Client.csproj

COPY src/Shared/ src/Shared/
COPY src/Client/ src/Client/

# ApiBaseUrl bị bake vào bundle lúc build — đó là bản chất của Blazor WASM
# standalone, không đọc được biến môi trường lúc runtime.
ARG API_BASE_URL=https://api.hushstore.io.vn
RUN sed -i "s|\"ApiBaseUrl\": *\"[^\"]*\"|\"ApiBaseUrl\": \"${API_BASE_URL}\"|" \
        src/Client/wwwroot/appsettings.json \
    && grep ApiBaseUrl src/Client/wwwroot/appsettings.json

RUN dotnet publish src/Client/Client.csproj -c Release -o /app/publish --no-restore

# ─── Stage 2: nginx serve static ─────────────────────────────────
FROM nginx:alpine AS runtime

COPY src/Client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80
