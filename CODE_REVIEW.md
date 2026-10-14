# Thiết kế giao diện và rà soát code

Khu quản trị đã được thiết kế lại từ đầu. Màu xanh lá trầm, nền sáng, menu theo nhóm, bảng dữ liệu và biểu mẫu thống nhất giúp quản lý thư viện rõ ràng hơn. Khu người đọc sử dụng layout riêng với cùng bảng màu xanh rừng. Hồ sơ, modal, chi tiết sách, bình luận, tủ sách, lịch sử, thông báo và các trang tài khoản đã được chỉnh lại đồng bộ.

## Giao diện hiện tại

| Trang | Nội dung |
| --- | --- |
| `/Admin` hoặc `/Admin/Dashboard` | Tổng quan thư viện, sách được thêm gần nhất, thông tin cần bổ sung, phân bố thể loại |
| `/Admin/ManageBook` | Tìm kiếm, lọc, thêm/sửa/xóa sách và xuất CSV |
| `/Admin/AddBook`, `/Admin/UpdateBook/{id}` | Biểu mẫu sách chung, ảnh bìa xem trước, thẻ sách và kiểm tra dữ liệu |
| `/Admin/Categories`, `/Admin/Tags`, `/Admin/ManageAuthor`, `/Admin/ManageNXB` | Các danh mục dùng chung cách tìm kiếm, phân trang và biểu mẫu CRUD |
| `/Admin/Users` | Thêm/sửa/xóa, khóa và kích hoạt tài khoản |
| `/Admin/AuditLog` | Nhật ký thao tác đã lưu, lọc người thao tác/nội dung, đối tượng, thao tác, khoảng ngày, phân trang và chi tiết IP |
| `/Admin/Notifications` | Thông báo cá nhân, lọc chưa đọc, đánh dấu đã đọc |
| `/Admin/Report` | Phân bố dữ liệu thực tế và xuất PDF |

Dashboard không còn khối Hoạt động gần đây, Thông báo & Log hoạt động hoặc nhật ký nội tuyến. Đã loại bỏ các hàm dựng hoạt động giả từ ngày xuất bản sách/ngày tạo tài khoản. Audit log đọc bảng ActivityLogs và không có nút sửa/xóa lịch sử.

## Cấu trúc code

- `_AdminLayout.cshtml`: khung quản trị; `_Layout.cshtml`: khung người đọc.
- `_CatalogWorkspace.cshtml` + `AdminCatalogDefinition`: biểu mẫu và danh sách dùng chung cho bốn danh mục.
- `_BookEditor.cshtml` + `BookEditorViewModel`: biểu mẫu tạo/cập nhật sách dùng chung.
- `admin-core.js`: request có CSRF token, tạo DOM/text an toàn, ngày giờ Việt Nam, ảnh dự phòng và phân trang.
- Các module riêng: `admin-books.js`, `admin-catalog.js`, `admin-users.js`, `admin-book-editor.js`, `admin-report.js`. Đã loại bỏ các renderer nối chuỗi HTML trong các trang quản trị.
- `AdminAccessFilter`: bảo vệ mọi trang và handler trong thư mục admin, bao gồm các alias.
- `AdminAuditFilter`: ghi thay đổi danh mục và tài khoản thành công cùng transaction với thao tác. Sách và tạo tài khoản ghi sự kiện ở service; đã sửa người thao tác từ mã/chủ sách thành tên tài khoản đang đăng nhập và lưu IP.
- Sửa thêm thẻ sách trùng khi tạo sách, giữ chủ sở hữu khi cập nhật sách, kiểm tra dữ liệu sách phía server, sửa tổng số kết quả tìm kiếm thể loại/tác giả, sửa số sách theo nhà xuất bản trong báo cáo.
- Tài khoản đang đăng nhập không thể tự xóa/khóa/hạ quyền. Tài khoản sở hữu sách được hướng dẫn khóa để giữ nội dung.
- Thông báo trả lời bình luận được đánh dấu đã đọc đúng, giữ trạng thái bộ lọc sau thao tác. Badge được tải từ số thông báo chưa đọc thực tế.
- Mobile: menu hỗ trợ Escape/Tab; bảng quản lý cuộn trong vùng riêng; Audit log đổi thành các thẻ đọc được đầy đủ trên màn hình hẹp.

## Kiểm tra

`dotnet build --no-restore` đạt 0 lỗi và 0 cảnh báo. `python tests/run_ui_checks.py` chạy 34 nhóm kiểm tra trên PostgreSQL riêng trong thư mục tạm:

- Các luồng đọc, tìm kiếm, trạng thái rỗng/lỗi, phân trang và Back.
- Khách/user không vào được admin, token CSRF bắt buộc, khóa admin mất quyền ngay, SignalR từ chối nhóm thông báo của người khác.
- CRUD **thực tế** cho sách, thể loại, thẻ, tác giả, nhà xuất bản và tài khoản; xác nhận trước xóa; dữ liệu trong bảng được cập nhật sau thao tác.
- Mỗi thao tác mới lưu đúng loại sự kiện, tài khoản thực hiện, mã đối tượng; ghi nhật ký cả sau khi đối tượng bị xóa.
- Audit log: bộ lọc, phân trang, chi tiết, tải lại, khoảng ngày không hợp lệ, ranh giới ngày UTC+7.
- Thông báo: trang riêng và đánh dấu trả lời đã đọc.
- Báo cáo: số sách theo nhà xuất bản đúng với cơ sở dữ liệu; export trả về PDF hợp lệ.
- Desktop 1440px, mobile 390px và không có lỗi JavaScript trong các luồng kiểm tra.

Không tạo dữ liệu thử nghiệm trong cơ sở dữ liệu cấu hình của ứng dụng. Kết quả và ảnh chụp lưu trong `artifacts/`, được Git bỏ qua. Xem `tests/README.md` để chạy lại.

## Rà soát bổ sung phía người đọc và backend

- `reader-core.js`: DOM an toàn, ISO UTC qua API, một formatter `Intl.DateTimeFormat` với `Asia/Ho_Chi_Minh`. Bình luận, phản hồi và thời điểm sửa đều có ngày/giờ cụ thể. Không dùng múi giờ của máy chủ hoặc chuỗi ISO làm nhãn.
- `BookCommentService`: bỏ truy vấn đệ quy theo từng phản hồi, dựng cây từ một truy vấn; lưu bình luận và đánh giá trong cùng transaction; giới hạn dữ liệu 500 ký tự, phân trang tối đa 50, kiểm tra sách/tài khoản/parent cùng sách; audit khi tạo/sửa/xóa. Xóa bình luận gốc xóa phản hồi nhiều cấp qua khóa ngoại cascade.
- `BookDetailModel`: bỏ service locator và các response có timestamp mâu thuẫn; kiểm tra quyền chủ bình luận trước sửa/xóa. Thông báo trả lời trỏ vào ID phản hồi mới. Trang thông báo dùng model có kiểu rõ ràng và truy vấn gom thay vì reflection.
- `PasswordStorage`: ASP.NET Core PasswordHasher với salt cho tạo/đổi/reset. Các mật khẩu dạng thường được chuyển thành hash khi khởi động; đăng nhập tương thích dữ liệu cũ và nâng cấp hash. Hash hiện tại vừa cột 100 ký tự nên không cần thay đổi schema. Giới hạn annotation đã được đồng bộ với cột.
- `PasswordResetGrant`: reset yêu cầu OTP đã xác thực trong cùng session, đúng email, hết hạn sau 10 phút và xóa quyền reset sau thành công. Chặn truy cập trực tiếp qua email hoặc sửa email trong POST. OTP dùng bộ sinh số mật mã, tối đa 5 lần thử, gửi tối đa một lần/phút; không tạo hai mã khác nhau ở page và service.
- `ReaderAccessFilter`: kiểm tra lại tài khoản trong DB trước handler; khóa tài khoản làm mất quyền sử dụng session cũ. SignalR kiểm tra tài khoản đang hoạt động khi đăng ký nhóm.
- Xóa một lượt tìm kiếm kiểm tra đúng chủ sở hữu; profile kiểm tra tên/email, giữ role/status/username phía server, kiểm tra mật khẩu hiện tại và độ dài mật khẩu mới.
- `NewBooksToday` dựa vào sự kiện thêm sách thực tế, các thống kê trong ngày dùng ranh giới ngày Việt Nam với khoảng UTC nửa mở.
- Bỏ tám module CSS cũ không còn được tham chiếu, tránh gradient và selector cũ xung đột. Tách JavaScript hồ sơ/chi tiết/bộ sưu tập khỏi Razor.

Các kiểm tra bổ sung bao gồm cập nhật hồ sơ, hash mật khẩu, reset không có OTP, đổi mật khẩu sai/đúng, xóa lịch sử của người khác, thông báo phản hồi, comment/reply CRUD nhiều cấp, parent thuộc sách khác, XSS, thời điểm tạo/sửa UTC+7, liên kết tới bình luận lồng nhau, cascade, khóa user và giao diện mobile.

Giới hạn: các nhóm kiểm tra chạy trên PostgreSQL cục bộ biệt lập. Chưa gửi email SMTP thật hoặc kiểm tra tải lớn; chưa coi toàn bộ các service báo cáo/email/chatbot là đã được kiểm chứng hoàn toàn. Không ghi dữ liệu kiểm thử vào DB cấu hình của ứng dụng.

Quy tắc UI tham khảo: https://raw.githubusercontent.com/vercel-labs/web-interface-guidelines/main/command.md

## Ảnh đại diện

- Hồ sơ có chọn ảnh, xem trước, lưu thay thế và xác nhận xóa. Chưa có ảnh hoặc đã xóa thì hiển thị chữ cái tên tài khoản.
- Ảnh hiện ở menu tài khoản của cả hai layout, hồ sơ, bình luận và phản hồi. Hồ sơ/menu cập nhật ngay sau lưu; API bình luận dùng URL phiên bản hiện tại khi tải lại.
- `AvatarService`: nhận JPG/PNG/WebP tối đa 2 MiB, kiểm tra nội dung bằng decoder; giới hạn kích thước ảnh đầu vào trước khi giải mã. Tự xoay theo EXIF, cắt giữa thành 256×256 WebP, bỏ metadata.
- `Users.AvatarVersion` lưu mã phiên bản; dữ liệu ảnh ở bảng `UserAvatars` riêng. Migration chỉ thêm cột nullable và bảng ảnh, giữ nguyên dữ liệu/tài khoản hiện có. `BookContextFactory` giúp tạo migration mà không chạy startup hoặc kết nối DB cấu hình.
- Upload/xóa dùng ID trong session, CSRF và kiểm tra trạng thái hoạt động. Các thao tác ảnh của cùng tài khoản được tuần tự hóa bằng transaction/row lock; client không chọn được tài khoản khác để sửa ảnh.
- URL ảnh đổi sau mỗi upload, có ETag và cache theo phiên bản; dữ liệu trả về cố định `image/webp` và `nosniff`. Không lưu file tùy ý trong thư mục public.
- `python tests/run_ui_checks.py avatar_checks.py`: 6 nhóm kiểm tra gồm preview, PNG/JPEG/WebP, thu nhỏ ảnh, reload/cache, file giả/quá dung lượng/quá chiều rộng, cập nhật profile giữ avatar, session ownership, CSRF, bình luận/phản hồi, lỗi mạng, mobile, xóa ảnh và tài khoản bị khóa.
- Kiểm tra hồi quy còn phát hiện và sửa lỗi hộp xác nhận không đóng khi bấm trong lúc mở: chỉ bật nút xác nhận sau sự kiện `shown.bs.modal`.

## Chatbot Gemini

- Key mới xác thực thành công, nhưng Google trả HTTP 404 khi gọi `gemini-2.5-flash` với project này. `gemini-3.8-flash` đã trả lời thành công trong kiểm tra trực tiếp; đây là model mặc định mới. Có thể ghi đè bằng `GEMINI_MODEL` hoặc `GEMINI__Model` trên Render.
- `GeminiClient` dùng HttpClient do DI quản lý, timeout 45 giây, truyền key bằng header thay cho URL. Không log key, prompt hoặc nội dung lỗi từ nhà cung cấp; thông báo phân biệt xác thực, model không khả dụng, hạn mức, timeout và kết nối.
- Không gửi chi tiết exception database vào prompt. Tránh tải danh sách sách hai lần khi tạo context. Nút gửi khóa trong lúc chờ để tránh gửi trùng.
- `python tests/gemini_checks.py`: 11 kiểm tra độc lập dùng HTTP giả lập, gồm phản hồi nhiều phần, loại bỏ phần suy nghĩ, JSON lỗi/rỗng, HTTP 401/403/404/429/500, mất kết nối, timeout và thiếu cấu hình. Key chỉ nằm ở môi trường, `appsettings.json` không chứa key.
- Docker bỏ qua `artifacts/`, `.git/` và `tests/` để không đóng gói dữ liệu local hoặc lịch sử Git.
