@echo off
echo =======================================
echo  BOOKINFOFINDER DOCKER DEPLOYMENT
echo =======================================
echo.

REM Kiểm tra Docker
docker --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ Docker không được cài đặt hoặc không chạy!
    echo Vui lòng cài đặt Docker Desktop và khởi động nó.
    pause
    exit /b 1
)

echo ✅ Docker đã sẵn sàng

REM Kiểm tra file .env
if not exist ".env" (
    echo.
    echo ⚠️  File .env không tồn tại!
    echo Đang tạo file .env từ template...
    copy ".env.example" ".env"
    echo.
    echo 🔧 VUI LÒNG CHỈNH SỬA FILE .env VỚI THÔNG TIN THỰC TẾ:
    echo    - DB_PASSWORD: Mật khẩu database mạnh
    echo    - EMAIL_ADDRESS: Email của bạn  
    echo    - EMAIL_PASSWORD: App password của email
    echo    - GEMINI_API_KEY: API key của Gemini
    echo.
    echo Nhấn Enter để mở file .env...
    pause >nul
    notepad .env
    echo.
    echo Đã chỉnh sửa file .env xong? (y/n):
    set /p confirm=
    if /i not "%confirm%"=="y" (
        echo Deployment bị hủy.
        pause
        exit /b 1
    )
)

echo ✅ File .env đã sẵn sàng

REM Lựa chọn deployment mode
echo.
echo Chọn chế độ deployment:
echo 1. Development (App + Database)
echo 2. Production (App + Database + Nginx)
echo 3. App Only (sử dụng database có sẵn)
echo.
set /p mode="Nhập lựa chọn (1-3): "

echo.
echo 🚀 Bắt đầu deployment...

if "%mode%"=="1" (
    echo Chế độ: Development
    docker-compose up -d postgres bookfinder-app
) else if "%mode%"=="2" (
    echo Chế độ: Production với Nginx
    docker-compose up -d --build
) else if "%mode%"=="3" (
    echo Chế độ: App Only
    docker build -t bookfinder .
    echo ⚠️  Bạn cần chạy container với database connection riêng
    echo Ví dụ: docker run -p 8080:8080 [environment variables] bookfinder
) else (
    echo Lựa chọn không hợp lệ!
    pause
    exit /b 1
)

if "%mode%" neq "3" (
    echo.
    echo ⏳ Đang khởi động services...
    timeout /t 10 /nobreak >nul
    
    echo.
    echo 📊 Trạng thái services:
    docker-compose ps
    
    echo.
    echo 🌐 Ứng dụng có thể truy cập tại:
    if "%mode%"=="2" (
        echo    - http://localhost (Nginx)
        echo    - http://localhost:8080 (Direct)
    ) else (
        echo    - http://localhost:8080
    )
    
    echo.
    echo 📋 Lệnh hữu ích:
    echo    - Xem logs: docker-compose logs -f
    echo    - Dừng: docker-compose down
    echo    - Restart: docker-compose restart
    
    echo.
    echo ✅ Deployment hoàn tất!
)

echo.
pause