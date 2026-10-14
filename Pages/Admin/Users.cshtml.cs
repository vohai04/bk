using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using BookInfoFinder.Models;
using BookInfoFinder.Data;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Pages.Admin
{
    public class UsersModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly BookContext _context;
        public UsersModel(IUserService userService, BookContext context)
        {
            _userService = userService;
            _context = context;
        }

        public void OnGet() { }

        // Handler AJAX cho disable user (khóa tài khoản)
        public async Task<JsonResult> OnPostAjaxDisableUserAsync([FromForm] int userId)
        {
            return await OnPostAjaxSetUserStatusAsync(userId, 0);
        }

        // Handler AJAX cho enable user (kích hoạt lại tài khoản)
        public async Task<JsonResult> OnPostAjaxEnableUserAsync([FromForm] int userId)
        {
            return await OnPostAjaxSetUserStatusAsync(userId, 1);
        }

        public async Task<JsonResult> OnGetAjaxSearchAsync()
        {
            try
            {
                var query = Request.Query;
                string? search = query["search"].ToString();
                string? role = query["role"].ToString();
                string? status = query["status"].ToString();
                int.TryParse(query["page"], out int page);
                int.TryParse(query["pageSize"], out int pageSize);

                page = Math.Clamp(page, 1, 100000);
                pageSize = pageSize <= 0 ? 10 : Math.Clamp(pageSize, 1, 50);

                var allUsers = await _userService.GetAllUsersAsync();
                var filteredUsers = allUsers.AsEnumerable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    filteredUsers = filteredUsers.Where(u =>
                        u.UserName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        u.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase));
                }

                // Apply role filter
                if (!string.IsNullOrWhiteSpace(role) && role != "all")
                {
                    filteredUsers = filteredUsers.Where(u => u.Role.Equals(role, StringComparison.OrdinalIgnoreCase));
                }

                // Apply status filter
                if (!string.IsNullOrWhiteSpace(status) && int.TryParse(status, out int statusInt))
                {
                    filteredUsers = filteredUsers.Where(u => u.Status == statusInt);
                }

                var totalCount = filteredUsers.Count();
                var pagedUsers = filteredUsers
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = pagedUsers.Select(u => new
                {
                    u.UserId,
                    u.UserName,
                    u.Email,
                    u.FullName,
                    u.Role,
                    u.Status, // Make sure Status is included
                    StatusText = u.Status == 1 ? "Hoạt động" : "Tạm khóa"
                });

                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                return new JsonResult(new { users = result, totalPages, totalCount });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxAddAsync(
            [FromForm] string userName,
            [FromForm] string fullName,
            [FromForm] string email,
            [FromForm] string password,
            [FromForm] string roleStr)
        {
            try
            {
                if (roleStr is not ("Admin" or "User"))
                    return new JsonResult(new { success = false, message = "Chọn vai trò hợp lệ." });
                if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
                {
                    return new JsonResult(new { success = false, message = "Vui lòng điền đầy đủ thông tin." });
                }

                if (await _userService.IsUserNameExistsAsync(userName.Trim()))
                {
                    return new JsonResult(new { success = false, message = "Tên đăng nhập đã tồn tại." });
                }

                if (await _userService.IsEmailExistsAsync(email.Trim()))
                {
                    return new JsonResult(new { success = false, message = "Email đã tồn tại." });
                }

                var userCreateDto = new UserCreateDto
                {
                    UserName = userName.Trim(),
                    FullName = fullName.Trim(),
                    Email = email.Trim(),
                    Password = password,
                    Role = roleStr.Trim()
                };

                var createdUser = await _userService.CreateUserAsync(userCreateDto);
                return new JsonResult(new
                {
                    success = true,
                    user = new { createdUser.UserId, createdUser.UserName, createdUser.Email, createdUser.FullName, createdUser.Role }
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxEditAsync(
            [FromForm] int userId,
            [FromForm] string userName,
            [FromForm] string fullName,
            [FromForm] string email,
            [FromForm] string roleStr)
        {
            try
            {
                var existingUser = await _userService.GetUserByIdAsync(userId);
                if (existingUser == null)
                    return new JsonResult(new { success = false, message = "Không tìm thấy người dùng." });
                if (roleStr is not ("Admin" or "User"))
                    return new JsonResult(new { success = false, message = "Chọn vai trò hợp lệ." });
                if (existingUser.Role == "Admin" && roleStr != "Admin")
                {
                    var reason = await GuardAccountAsync(userId);
                    if (reason is not null) return new JsonResult(new { success = false, message = reason });
                }

                if (string.IsNullOrWhiteSpace(email) || email.Length > 100)
                    return new JsonResult(new { success = false, message = "Email không hợp lệ." });

                // Check if username exists for other users
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    var usernameExists = await _userService.IsUserNameExistsAsync(userName.Trim());
                    if (usernameExists && !existingUser.UserName.Equals(userName.Trim(), StringComparison.OrdinalIgnoreCase))
                        return new JsonResult(new { success = false, message = "Tên đăng nhập đã tồn tại." });
                }

                // Check if email exists for other users
                var emailExists = await _userService.IsEmailExistsAsync(email.Trim());
                if (emailExists && !existingUser.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase))
                    return new JsonResult(new { success = false, message = "Email đã tồn tại." });

                var userUpdateDto = new UserUpdateDto
                {
                    UserId = userId,
                    UserName = userName?.Trim() ?? existingUser.UserName,
                    Email = email.Trim(),
                    FullName = fullName?.Trim() ?? existingUser.FullName,
                    Role = roleStr?.Trim() ?? existingUser.Role,
                    Status = existingUser.Status // Keep current status
                };

                var updatedUser = await _userService.UpdateUserAsync(userUpdateDto);
                return new JsonResult(new
                {
                    success = true,
                    user = new { updatedUser.UserId, updatedUser.UserName, updatedUser.Email, updatedUser.FullName, updatedUser.Role }
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxSetUserStatusAsync([FromForm] int userId, [FromForm] int status)
        {
            try
            {
                if (status is not (0 or 1)) return new JsonResult(new { success = false, message = "Trạng thái không hợp lệ." });
                if (status == 0)
                {
                    var reason = await GuardAccountAsync(userId);
                    if (reason is not null) return new JsonResult(new { success = false, message = reason });
                }
                bool success;
                if (status == 1)
                {
                    success = await _userService.ActivateUserAsync(userId);
                }
                else
                {
                    success = await _userService.DeactivateUserAsync(userId);
                }
                return new JsonResult(new { success });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxDeleteAsync([FromForm] int userId)
        {
            var reason = await GuardAccountAsync(userId);
            if (reason is not null) return new JsonResult(new { success = false, message = reason });
            if (await _context.Books.AnyAsync(b => b.UserId == userId))
                return new JsonResult(new { success = false, message = "Tài khoản đang sở hữu sách. Hãy khóa tài khoản để ngừng truy cập và giữ nội dung thư viện." });
            try { return new JsonResult(new { success = await _userService.DeleteUserAsync(userId) }); }
            catch (DbUpdateException) { return new JsonResult(new { success = false, message = "Tài khoản còn dữ liệu liên quan. Hãy dùng Khóa tài khoản." }); }
        }

        private async Task<string?> GuardAccountAsync(int userId)
        {
            if (HttpContext.Session.GetString("UserId") == userId.ToString())
                return "Bạn không thể xóa, khóa hoặc hạ quyền tài khoản đang đăng nhập.";
            var user = await _userService.GetUserByIdAsync(userId);
            if (user is null) return "Không tìm thấy người dùng.";
            if (user.Role == "Admin" && user.Status == 1 && await _context.Users.CountAsync(u => u.Role == Role.Admin && u.Status == 1) <= 1)
                return "Thư viện cần ít nhất một quản trị viên đang hoạt động.";
            return null;
        }
    }
}
