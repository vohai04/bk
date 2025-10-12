using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IEmailService
    {
        // OTP operations (rate limiting removed)
        string GenerateOTP(int length = 6);
        Task<bool> SendOTPEmailAsync(OTPRequestDto otpRequest);
        Task<bool> VerifyOTPAsync(OTPVerifyDto otpVerify);
        
        // Password reset (rate limiting removed)
        Task<bool> SendPasswordResetEmailAsync(PasswordResetRequestDto resetRequest);
        
        // Email validation
        bool IsValidEmail(string email);
        
        // OTP storage and validation (in-memory cache)
        Task StoreOTPAsync(string email, string otp, TimeSpan expiry);
        Task<bool> ValidateStoredOTPAsync(string email, string otp);
        Task ClearOTPAsync(string email);
    }
}