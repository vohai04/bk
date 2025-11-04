# ===== OPTIMIZED DOCKERFILE =====
# Multi-stage build để giảm image size

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy chỉ project file trước để cache dependencies tốt hơn
COPY ["BookInfoFinder.csproj", "./"]
RUN dotnet restore "BookInfoFinder.csproj" --verbosity minimal

# Copy source code
COPY . .

# Build với optimization
RUN dotnet build "BookInfoFinder.csproj" -c Release -o /app/build --no-restore

# Stage 2: Publish  
FROM build AS publish
RUN dotnet publish "BookInfoFinder.csproj" -c Release -o /app/publish \
    --no-restore --no-build \
    /p:UseAppHost=false \
    /p:PublishTrimmed=false

# Stage 3: Final runtime (chỉ runtime, không có SDK)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-slim AS final
WORKDIR /app

# Cài packages cần thiết trong 1 RUN command để giảm layers
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgdiplus \
    libc6-dev \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* \
    && rm -rf /tmp/* \
    && rm -rf /var/tmp/*

# Copy chỉ published files, không copy build artifacts
COPY --from=publish /app/publish .

# Security: tạo non-root user
RUN adduser --disabled-password --gecos '' --shell /bin/false appuser \
    && chown -R appuser:appuser /app
USER appuser

# Environment
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:10000
ENV DOTNET_EnableDiagnostics=0

EXPOSE 10000

ENTRYPOINT ["dotnet", "BookInfoFinder.dll"]