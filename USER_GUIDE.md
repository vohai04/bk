# BookInfoFinder — Hướng dẫn sử dụng (User Guide)

Phiên bản: 1.0
Ngày: 18/11/2025
Tác giả: Võ Văn Hài (chỉnh sửa và mở rộng cho dự án)

Mục đích: Tài liệu này hướng dẫn người dùng cuối cách sử dụng ứng dụng web BookInfoFinder — tìm kiếm sách, xem chi tiết, yêu thích, bình luận, đánh giá, và quản lý cơ bản.

---

## Mục lục

- Giới thiệu
- Đăng ký & Đăng nhập
- Tìm kiếm sách
- Xem chi tiết sách
- Yêu thích (Favorites)
- Bình luận (Comments)
- Đánh giá (Ratings)
- Thông báo (Notifications)
- Lịch sử tìm kiếm
- Trang cá nhân (Profile)
- Quản trị (Admin)

---

## Giới thiệu

BookInfoFinder là một ứng dụng web dành cho người đọc để tìm kiếm và tương tác với thông tin sách (tác giả, nhà xuất bản, thể loại, tag). Người dùng có thể đăng ký, đăng nhập, tìm sách, thêm vào danh sách yêu thích, bình luận, đánh giá, và nhận thông báo.

Ứng dụng được xây bằng ASP.NET Razor Pages với các service xử lý (xem thư mục `Services/`) và entity model trong `Models/Entity`.

---

## Đăng ký & Đăng nhập

Trang liên quan: `Pages/Account/Register.cshtml`, `Pages/Account/Login.cshtml`.

1. Đăng ký (Register):

   - Mở trang `Register`.
   - Điền tên người dùng, `email`, `username`, `password` và xác nhận password.
   - Hệ thống kiểm tra và tạo tài khoản .
2. Đăng nhập (Login):

   - Mở trang `Login`.
   - Nhập `username` hoặc `email` và `password`, nhấn `Đăng nhập`.
   - (Tùy chọn) `Remember Me`: lưu token an toàn để đăng nhập tự động.
   - Sau đăng nhập thành công, người dùng được chuyển hướng tới trang chính Index cho người dùng hoặc  dashboard cho admin.

Lưu ý: Nếu quên mật khẩu, dùng trang `ForgotPassword` hoặc `ResetPassword`.

---

## Tìm kiếm sách

Trang chính (`Index`) có thanh tìm kiếm.

- Nhập từ khóa (tiêu đề, tác giả, thể loại) và nhấn `Tìm`.
- Kết quả hiển thị danh sách sách khớp với phân trang.
- Có thể dùng bộ lọc (theo sách được yêu thích nhiều nhất, xu hướng tìm kiếm, sách đánh giá cao và lọc theo tag).
- Click vào một mục trong danh sách để mở `BookDetail`.

Nếu bạn đã đăng nhập, từ khóa tìm kiếm có thể được lưu vào lịch sử tìm kiếm.

---

## Xem chi tiết sách

Trang: `BookDetail`.

- Hiển thị: tiêu đề, tác giả, mô tả, nhà xuất bản, thể loại, ảnh bìa, rating trung bình, danh sách bình luận...
- Hành động:
  - `Yêu thích` (Add to Favorites): lưu sách vào danh sách của bạn.
  - `Bình luận`: thêm bình luận (cần đăng nhập).
  - `Đánh giá`: chọn số sao (1-5) để đánh giá sách.

Nếu user chưa đăng nhập, thì sẽ có 1 thông báo là yêu cầu đăng nhập để thêm vào yêu thích, hoặc đăng nhập để đánh giá và bình luận.

---

## Yêu thích (Favorites)

Trang: `Favorites`.

- Thêm sách yêu thích từ trang chi tiết.
- Truy cập trang `Favorites` để xem, duyệt và xóa sách đã lưu.
- Favorites được liên kết với tài khoản người dùng.

---

## Bình luận (Comments)

- Truy cập trang sách, nhập nội dung bình luận kèm số sao và gửi.
- Bình luận hiển thị ngay (realtime SignalR).
- Hệ thống có chức năng thông báo khi ai đó phản hồi bình luận của mình.

---

## Đánh giá (Ratings)

- Chỉ user đã đăng nhập có thể đánh giá.
- Mỗi user có thể đánh giá 1 lần / sách (hệ thống có thể cho phép sửa rating).
- Sau khi rating, điểm trung bình sách được cập nhật ngay.

---

## Thông báo (Notifications)

- Ứng dụng có hỗ trợ thông báo realtime (SignalR hub nằm trong `Hubs/NotificationHub.cs`).
- Những sự kiện thông báo : Khi ai đó trả lời tin nhắn của mình.
- Người dùng có thể thấy thông báo trên trang `Notifications`.

---

## Lịch sử tìm kiếm

- Lưu lại các truy vấn tìm kiếm cho người dùng đã đăng nhập đã đăng nhập.
- Truy cập trang `SearchHistory` để xem hoặc xóa mục lịch sử.
- Có thể tìm lại lịch sử tìm kiếm, hiển thị ra có bao nhiêu kết quả khớp với cái người dùng đã tìm kiếm.
- xóa lịch sử tìm kiếm, có thể xóa tất cả hoặc xóa từng cái 1.

---

## Trang cá nhân (Profile)

- Trang `Profile` cho phép xem và chỉnh sửa thông tin cơ bản của người dùng (tên, email).
- Và có thể dổi mật khẩu, gửi OTP qua mail đã đăng ký trước đó.

---

## Chatbot hỗ trợ người dùng

- Trang Chatbot cho phép người dùng đặt câu hỏi về sách, tác giả, thể loại, hoặc các vấn đề liên quan đến sử dụng hệ thống.
- Chatbot có thể gợi ý sách, giải đáp thắc mắc về chức năng, hướng dẫn thao tác cơ bản.
- Người dùng chỉ cần nhập câu hỏi vào khung chat, hệ thống sẽ trả lời tự động nếu có dữ liệu trong database.
- Một số ví dụ:
  - Tôi muốn biết sách nào được đánh giá cao nhất.
  - Có sách nào tên ABC hay không.
  - Tổng số sách hiện tại là bao nhiêu.
- Chatbot hoạt động 24/7, giúp tiết kiệm thời gian hỗ trợ thủ công.

---

## Quản trị (Admin)

Trang admin nằm trong `Pages/Admin/*`.

Quyền admin cho phép:

- Thêm/sửa/xóa sách (`ManageBook`, `AddBook`).
- Quản lý tác giả, nhà xuất bản, thể loại (`ManageAuthor`, `ManageNXB`, `Categories`...).
- Xem báo cáo Report dùng QuestPDF.
- Xem các log thông báo ở pages Dashboard khi có người dùng đăng kí mới, có comment về sách mới,...có thể ấn vào log đó dể đưa dến nơi đã xảy ra thông báo đó, ví dụ : ai reply cái gì và sách nào,....
- Trang admin có thể chuyển đổi qua trang của người dùng

Lưu ý: Các trang admin yêu cầu quyền (role=Admin). Nếu cố truy cập khi chưa có quyền, hệ thống trả 403.
