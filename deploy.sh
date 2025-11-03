#!/bin/bash

echo "======================================="
echo "  BOOKINFOFINDER DOCKER DEPLOYMENT"
echo "======================================="
echo ""

# Kiểm tra Docker
if ! command -v docker &> /dev/null; then
    echo "❌ Docker không được cài đặt!"
    echo "Vui lòng cài đặt Docker và thử lại."
    exit 1
fi

echo "✅ Docker đã sẵn sàng"

# Kiểm tra file .env
if [ ! -f ".env" ]; then
    echo ""
    echo "⚠️  File .env không tồn tại!"
    echo "Đang tạo file .env từ template..."
    cp ".env.example" ".env"
    echo ""
    echo "🔧 VUI LÒNG CHỈNH SỬA FILE .env VỚI THÔNG TIN THỰC TẾ:"
    echo "   - DB_PASSWORD: Mật khẩu database mạnh"
    echo "   - EMAIL_ADDRESS: Email của bạn"
    echo "   - EMAIL_PASSWORD: App password của email"
    echo "   - GEMINI_API_KEY: API key của Gemini"
    echo ""
    read -p "Nhấn Enter để mở file .env..." 
    ${EDITOR:-nano} .env
    echo ""
    read -p "Đã chỉnh sửa file .env xong? (y/n): " confirm
    if [[ $confirm != [yY] ]]; then
        echo "Deployment bị hủy."
        exit 1
    fi
fi

echo "✅ File .env đã sẵn sàng"

# Lựa chọn deployment mode
echo ""
echo "Chọn chế độ deployment:"
echo "1. Development (App + Database)"
echo "2. Production (App + Database + Nginx)"
echo "3. App Only (sử dụng database có sẵn)"
echo ""
read -p "Nhập lựa chọn (1-3): " mode

echo ""
echo "🚀 Bắt đầu deployment..."

case $mode in
    1)
        echo "Chế độ: Development"
        docker-compose up -d postgres bookfinder-app
        ;;
    2)
        echo "Chế độ: Production với Nginx"
        docker-compose up -d --build
        ;;
    3)
        echo "Chế độ: App Only"
        docker build -t bookfinder .
        echo "⚠️  Bạn cần chạy container với database connection riêng"
        echo "Ví dụ: docker run -p 8080:8080 [environment variables] bookfinder"
        ;;
    *)
        echo "Lựa chọn không hợp lệ!"
        exit 1
        ;;
esac

if [ "$mode" != "3" ]; then
    echo ""
    echo "⏳ Đang khởi động services..."
    sleep 10
    
    echo ""
    echo "📊 Trạng thái services:"
    docker-compose ps
    
    echo ""
    echo "🌐 Ứng dụng có thể truy cập tại:"
    if [ "$mode" == "2" ]; then
        echo "   - http://localhost (Nginx)"
        echo "   - http://localhost:8080 (Direct)"
    else
        echo "   - http://localhost:8080"
    fi
    
    echo ""
    echo "📋 Lệnh hữu ích:"
    echo "   - Xem logs: docker-compose logs -f"
    echo "   - Dừng: docker-compose down"
    echo "   - Restart: docker-compose restart"
    
    echo ""
    echo "✅ Deployment hoàn tất!"
fi

echo ""
read -p "Nhấn Enter để tiếp tục..."