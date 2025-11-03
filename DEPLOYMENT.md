# 🚀 HƯỚNG DẪN DEPLOY BOOKINFOFINDER VỚI DOCKER

## 📋 Yêu cầu hệ thống
- Docker Desktop đã cài đặt
- Docker Compose
- Git (để clone project)

## 🔧 Chuẩn bị deployment

### 1. Tạo file environment variables
```bash
# Copy file mẫu
copy .env.example .env

# Chỉnh sửa file .env với thông tin thực tế
notepad .env
```

### 2. Cấu hình dữ liệu nhạy cảm trong .env
```env
# Database
DB_PASSWORD=your_strong_password_123!

# Email (sử dụng App Password)
EMAIL_ADDRESS=your-email@gmail.com
EMAIL_PASSWORD=your_gmail_app_password

# Gemini AI
GEMINI_API_KEY=your_gemini_api_key
```

## 🚀 Deployment Options

### Option 1: Quick Start (Development)
```bash
# Build và chạy tất cả services
docker-compose up --build

# Chạy ở background
docker-compose up -d --build
```

### Option 2: Production với Nginx
```bash
# Chỉ app + database
docker-compose up -d postgres bookfinder-app

# Hoặc full stack với nginx
docker-compose up -d
```

### Option 3: Chỉ app (sử dụng external database)
```bash
# Nếu đã có PostgreSQL sẵn
docker build -t bookfinder .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=your-db;Database=BookInfoDB;Username=postgres;Password=yourpass" \
  -e EmailSettings__Email="your-email@gmail.com" \
  -e EmailSettings__Password="your-app-password" \
  -e GEMINI__ApiKey="your-api-key" \
  bookfinder
```

## 🌐 Truy cập ứng dụng

- **Với Nginx**: http://localhost (port 80)
- **Direct app**: http://localhost:8080
- **Database**: localhost:5432

## 📊 Monitoring & Logs

### Xem logs
```bash
# Logs tất cả services
docker-compose logs -f

# Logs của app
docker-compose logs -f bookfinder-app

# Logs của database
docker-compose logs -f postgres
```

### Kiểm tra health
```bash
# Health check
curl http://localhost:8080/health

# Database status
docker-compose exec postgres pg_isready -U postgres
```

## 🔒 Bảo mật Production

### 1. Environment Variables
- ✅ Sử dụng file `.env` cho sensitive data
- ✅ KHÔNG commit `.env` vào Git
- ✅ Backup `.env` file ở nơi an toàn

### 2. Database Security
```bash
# Thay đổi default password
DB_PASSWORD=SuperStrongPassword123!@#

# Giới hạn network access
# Database chỉ accessible từ app container
```

### 3. SSL/HTTPS (Production)
```bash
# Tạo SSL certificates
mkdir nginx/ssl

# Copy certificates
copy your-cert.pem nginx/ssl/cert.pem
copy your-key.pem nginx/ssl/key.pem

# Uncomment SSL config trong nginx.conf
```

## 🛠️ Maintenance Commands

### Database Migration
```bash
# Chạy migration trong container
docker-compose exec bookfinder-app dotnet ef database update
```

### Backup Database
```bash
# Backup
docker-compose exec postgres pg_dump -U postgres BookInfoDB > backup.sql

# Restore
docker-compose exec -T postgres psql -U postgres BookInfoDB < backup.sql
```

### Update Application
```bash
# Pull latest code
git pull origin main

# Rebuild và restart
docker-compose down
docker-compose up --build -d
```

### Cleanup
```bash
# Dọn dẹp containers cũ
docker-compose down --volumes

# Xóa images cũ
docker image prune -a
```

## 🚨 Troubleshooting

### App không start
1. Kiểm tra logs: `docker-compose logs bookfinder-app`
2. Verify environment variables trong `.env`
3. Đảm bảo database đã sẵn sàng

### Database connection error
1. Kiểm tra postgres container: `docker-compose ps`
2. Test connection: `docker-compose exec postgres pg_isready`
3. Verify connection string trong `.env`

### Port conflicts
```bash
# Thay đổi ports trong docker-compose.yml
ports:
  - "8081:8080"  # Thay vì 8080:8080
```

## 📁 File Structure sau deployment
```
BookInfoFinder/
├── Dockerfile
├── docker-compose.yml
├── .env (KHÔNG commit)
├── .env.example
├── .dockerignore
├── nginx/
│   └── nginx.conf
├── appsettings.Production.json
└── [existing project files]
```

## 🎯 Next Steps
1. ✅ Setup monitoring (Prometheus + Grafana)
2. ✅ Configure auto-backup
3. ✅ Setup CI/CD pipeline
4. ✅ Domain & SSL setup
5. ✅ Load balancing cho production