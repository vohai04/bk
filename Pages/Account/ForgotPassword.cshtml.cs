using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BookInfoFinder.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ForgotPasswordModel> _logger;

        // Dictionary để lưu OTP tạm thời - bỏ vì đã dùng EmailService
        // private static readonly Dictionary<string, (string Otp, DateTime ExpireTime)> _otpStorage = new();

        public ForgotPasswordModel(IUserService userService, IEmailService emailService, ILogger<ForgotPasswordModel> logger)
        {
            _userService = userService;
            _emailService = emailService;
            _logger = logger;
        }

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có đúng 6 số")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP chỉ được chứa số")]
        public string OtpCode { get; set; } = string.Empty;

        public bool IsOtpSent { get; set; } = false;
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public void OnGet()
        {
            // Reset form khi load trang
            IsOtpSent = false;
        }

        public async Task<IActionResult> OnPostSendOtpAsync()
        {
            // Chỉ validate Email field
            ModelState.Clear();
            if (string.IsNullOrEmpty(Email) || !new EmailAddressAttribute().IsValid(Email))
            {
                ErrorMessage = "Vui lòng nhập email hợp lệ.";
                return Page();
            }

            try
            {
                // Kiểm tra email có tồn tại trong hệ thống không
                var user = await _userService.GetUserByEmailAsync(Email);
                if (user == null)
                {
                    ErrorMessage = "Email này không tồn tại trong hệ thống.";
                    return Page();
                }

                // Gửi email
                var otpRequest = new OTPRequestDto
                {
                    Email = Email,
                    Purpose = "PasswordReset"
                };
                if (!await _emailService.SendOTPEmailAsync(otpRequest))
                {
                    ErrorMessage = "Không gửi được mã xác thực. Vui lòng thử lại sau.";
                    return Page();
                }

                _logger.LogInformation("OTP sent to email {Email}", Email);

                HttpContext.Session.SetString("OtpEmail", Email.Trim().ToLowerInvariant());
                HttpContext.Session.SetString("OtpPurpose", "forgot-password");
                IsOtpSent = true;
                SuccessMessage = "Mã xác thực đã được gửi đến email của bạn. Vui lòng kiểm tra hộp thư.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP to email {Email}", Email);
                ErrorMessage = "Có lỗi xảy ra khi gửi email. Vui lòng thử lại.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostResendOtpAsync()
        {
            if (string.IsNullOrEmpty(Email))
            {
                ErrorMessage = "Email không hợp lệ.";
                return Page();
            }

            return await OnPostSendOtpAsync();
        }

        public async Task<IActionResult> OnPostVerifyOtpAsync()
        {
            if (!ModelState.IsValid)
            {
                IsOtpSent = true;
                return Page();
            }

            try
            {
                if (HttpContext.Session.GetString("OtpEmail") != Email.Trim().ToLowerInvariant() || HttpContext.Session.GetString("OtpPurpose") != "forgot-password")
                    return RedirectToPage("/Account/ForgotPassword");
                // Kiểm tra OTP có hợp lệ không
                if (!await _emailService.ValidateStoredOTPAsync(Email, OtpCode))
                {
                    ErrorMessage = "Mã OTP không đúng hoặc đã hết hạn.";
                    IsOtpSent = true;
                    return Page();
                }

                // OTP đúng, xóa khỏi bộ nhớ
                await _emailService.ClearOTPAsync(Email);

                _logger.LogInformation("OTP verified successfully for email {Email}", Email);

                // Chuyển hướng đến trang đặt lại mật khẩu
                BookInfoFinder.Services.PasswordResetGrant.Issue(HttpContext.Session, Email);
                return RedirectToPage("/Account/ResetPassword", new { email = Email });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for email {Email}", Email);
                ErrorMessage = "Có lỗi xảy ra. Vui lòng thử lại.";
                IsOtpSent = true;
                return Page();
            }
        }
    }
}