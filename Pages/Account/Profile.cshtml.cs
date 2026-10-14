using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Services.Interface;
using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Pages.Account
{
    [RequestSizeLimit(3 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3 * 1024 * 1024)]
    public class ProfilePageModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly IAvatarService _avatars;

        public ProfilePageModel(IUserService userService, IAvatarService avatars)
        {
            _userService = userService;
            _avatars = avatars;
        }

        // Make non-nullable to avoid CS8602 in Razor views; initialized and replaced with real data in OnGetAsync
        public UserDto Profile { get; set; } = new UserDto();

        public async Task<IActionResult> OnGetAsync()
        {
            await HttpContext.Session.LoadAsync();
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                // Not logged in -> redirect to login
                return RedirectToPage("/Account/Login");
            }

            var profileFromService = await _userService.GetUserByIdAsync(userId);
            if (profileFromService == null) return NotFound();
            Profile = profileFromService;
            return Page();
        }

        // AJAX handler: Update profile
        public async Task<JsonResult> OnPostUpdateAsync([FromBody] UserUpdateDto dto)
        {
            await HttpContext.Session.LoadAsync();
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int sessionUserId))
            {
                return new JsonResult(new { success = false, message = "Bạn cần đăng nhập" });
            }

            if (dto == null || dto.UserId != sessionUserId || string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Length > 100 || string.IsNullOrWhiteSpace(dto.Email) || dto.Email.Length > 256 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(dto.Email))
                return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ" });

            try
            {
                // Only update FullName and Email
                var user = await _userService.GetUserByIdAsync(dto.UserId);
                if (user == null)
                    return new JsonResult(new { success = false, message = "Không tìm thấy người dùng" });

                user.FullName = dto.FullName;
                user.Email = dto.Email;

                // Update entity
                var updateDto = new UserUpdateDto
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email,
                    UserName = user.UserName,
                    Role = user.Role,
                    Status = user.Status
                };
                var updated = await _userService.UpdateUserAsync(updateDto);
                return new JsonResult(new
                {
                    success = true,
                    user = new
                    {
                        userId = updated.UserId,
                        email = updated.Email,
                        fullName = updated.FullName,
                        updatedAt = updated.UpdatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // AJAX handler: change password (simple implementation using ResetPasswordAsync)
        public async Task<JsonResult> OnPostChangePasswordAsync([FromBody] ChangePasswordRequest req)
        {
            await HttpContext.Session.LoadAsync();
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int sessionUserId))
            {
                return new JsonResult(new { success = false, message = "Bạn cần đăng nhập" });
            }

            if (req == null || req.UserId != sessionUserId || string.IsNullOrEmpty(req.NewPassword) || string.IsNullOrEmpty(req.CurrentPassword) || req.NewPassword.Length is < 6 or > 100)
                return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ" });

            try
            {
                // Delegate change-password logic to the service layer
                var changed = await _userService.ChangePasswordAsync(req.UserId, req.CurrentPassword, req.NewPassword);
                if (!changed)
                {
                    // Could be wrong current password or user not found
                    return new JsonResult(new { success = false, message = "Mật khẩu hiện tại không đúng hoặc không thể đổi" });
                }
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostUploadAvatarAsync(IFormFile? avatar)
        {
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
                return new JsonResult(new { success = false, message = "Đăng nhập để đổi ảnh đại diện." }) { StatusCode = 401 };
            if (avatar is null)
                return new JsonResult(new { success = false, message = "Chọn ảnh đại diện trước khi lưu." }) { StatusCode = 400 };
            try
            {
                var result = await _avatars.UploadAsync(userId, avatar, HttpContext.RequestAborted);
                return new JsonResult(new { success = true, user = result });
            }
            catch (ArgumentException error)
            {
                return new JsonResult(new { success = false, message = error.Message }) { StatusCode = 400 };
            }
        }

        public async Task<IActionResult> OnPostDeleteAvatarAsync()
        {
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
                return new JsonResult(new { success = false, message = "Đăng nhập để xóa ảnh đại diện." }) { StatusCode = 401 };
            var result = await _avatars.DeleteAsync(userId, HttpContext.RequestAborted);
            return new JsonResult(new { success = true, user = result });
        }

        public class ChangePasswordRequest
        {
            public int UserId { get; set; }
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
    }
}
