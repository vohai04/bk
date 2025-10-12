using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BookInfoFinder.Pages.Account
{
    public class VerifyOTPModel : PageModel
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<VerifyOTPModel> _logger;

        // Dictionary để lưu OTP tạm thời - bỏ vì đã dùng EmailService
        // private static readonly Dictionary<string, (string Otp, DateTime ExpireTime, string Purpose)> _otpStorage = new();

        public VerifyOTPModel(IEmailService emailService, ILogger<VerifyOTPModel> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Purpose { get; set; } = string.Empty; // "forgot-password", "email-verification", etc.

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có đúng 6 số")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP chỉ được chứa số")]
        public string OtpCode { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public IActionResult OnGet(string? email = null, string? purpose = null)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(purpose))
            {
                return RedirectToPage("/Account/Login");
            }

            Email = email;
            Purpose = purpose;
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
                // Kiểm tra OTP sử dụng EmailService
                if (!await _emailService.ValidateStoredOTPAsync(Email, OtpCode))
                {
                    ErrorMessage = "Mã OTP không đúng hoặc đã hết hạn.";
                    return Page();
                }

                // OTP đúng, xóa khỏi bộ nhớ
                await _emailService.ClearOTPAsync(Email);

                _logger.LogInformation("OTP verified successfully for email {Email}, purpose {Purpose}", Email, Purpose);

                // Chuyển hướng dựa trên mục đích
                switch (Purpose.ToLower())
                {
                    case "forgot-password":
                        return RedirectToPage("/Account/ResetPassword", new { email = Email });
                    case "email-verification":
                        return RedirectToPage("/Account/Login", new { message = "Email đã được xác thực thành công." });
                    default:
                        return RedirectToPage("/Account/Login", new { message = "Xác thực OTP thành công." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for email {Email}", Email);
                ErrorMessage = "Có lỗi xảy ra. Vui lòng thử lại.";
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

            try
            {
                // Tạo mã OTP mới
                var otpCode = _emailService.GenerateOTP();

                // Lưu OTP
                await _emailService.StoreOTPAsync(Email, otpCode, TimeSpan.FromMinutes(5));

                // Gửi email
                var otpRequest = new OTPRequestDto
                {
                    Email = Email,
                    Purpose = Purpose
                };
                await _emailService.SendOTPEmailAsync(otpRequest);

                _logger.LogInformation("OTP resent to email {Email}", Email);

                SuccessMessage = "Mã OTP mới đã được gửi đến email của bạn.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending OTP to email {Email}", Email);
                ErrorMessage = "Có lỗi xảy ra khi gửi email. Vui lòng thử lại.";
                return Page();
            }
        }

        // Phương thức static để các page khác có thể tạo và lưu OTP
        public static async Task SetOTPAsync(IEmailService emailService, string email, string otp, string purpose, int expireMinutes = 5)
        {
            await emailService.StoreOTPAsync(email, otp, TimeSpan.FromMinutes(expireMinutes));
        }

        // Phương thức static để kiểm tra OTP từ bên ngoài
        public static async Task<bool> VerifyOTPAsync(IEmailService emailService, string email, string otp, string purpose)
        {
            bool isValid = await emailService.ValidateStoredOTPAsync(email, otp);
            if (isValid)
            {
                await emailService.ClearOTPAsync(email);
            }
            return isValid;
        }
    }
}