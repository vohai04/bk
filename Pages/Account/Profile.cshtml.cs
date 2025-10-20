using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Services.Interface;
using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Pages.Account
{
    public class ProfilePageModel : PageModel
    {
        private readonly IUserService _userService;

        public ProfilePageModel(IUserService userService)
        {
            _userService = userService;
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

                if (dto == null || dto.UserId != sessionUserId)
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
                    return new JsonResult(new { success = true, user = new {
                        userId = updated.UserId,
                        email = updated.Email,
                        fullName = updated.FullName,
                        updatedAt = updated.UpdatedAt?.ToString("dd/MM/yyyy HH:mm")
                    }});
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

            if (req == null || req.UserId != sessionUserId)
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

        public class ChangePasswordRequest
        {
            public int UserId { get; set; }
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
    }
}
