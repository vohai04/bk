# Sử dụng .NET 9.0 SDK để build app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj và restore dependencies
COPY ["BookInfoFinder.csproj", "."]
RUN dotnet restore "BookInfoFinder.csproj"

# Copy toàn bộ source code và build
COPY . .
WORKDIR "/src"
RUN dotnet build "BookInfoFinder.csproj" -c Release -o /app/build

# Publish app
FROM build AS publish
RUN dotnet publish "BookInfoFinder.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Sử dụng runtime image để chạy app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Cài đặt packages cần thiết cho PostgreSQL và các dependencies
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Expose port 10000 (Render's default port)
EXPOSE 10000

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:10000

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "BookInfoFinder.dll"]