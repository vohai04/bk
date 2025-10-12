using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BookInfoFinder.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILogger<ResetPasswordModel> _logger;

        public ResetPasswordModel(IUserService userService, ILogger<ResetPasswordModel> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có từ 6-100 ký tự")]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public IActionResult OnGet(string? email = null)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToPage("/Account/ForgotPassword");
            }

            Email = email;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Validate password strength
                if (NewPassword.Length < 6)
                {
                    ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.";
                    return Page();
                }

                // Kiểm tra người dùng có tồn tại không
                var user = await _userService.GetUserByEmailAsync(Email);
                if (user == null)
                {
                    ErrorMessage = "Không tìm thấy tài khoản với email này.";
                    return Page();
                }

                // Kiểm tra tài khoản có bị vô hiệu hóa không
                if (user.Status == 0)
                {
                    ErrorMessage = "Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.";
                    return Page();
                }

                // Sử dụng ResetPasswordAsync thay vì ChangePasswordAsync
                bool result = await _userService.ResetPasswordAsync(user.UserId, NewPassword);
                if (!result)
                {
                    ErrorMessage = "Không thể đặt lại mật khẩu. Vui lòng thử lại sau.";
                    return Page();
                }

                _logger.LogInformation("Password reset successfully for email {Email}", Email);

                TempData["SuccessMessage"] = "Mật khẩu đã được đặt lại thành công! Vui lòng đăng nhập với mật khẩu mới.";
                return RedirectToPage("/Account/Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for email {Email}", Email);
                ErrorMessage = "Có lỗi hệ thống xảy ra. Vui lòng thử lại sau.";
                return Page();
            }
        }
    }
}