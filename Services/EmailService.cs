using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;

namespace BookInfoFinder.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<EmailService> _logger;
        private readonly Random _random = new Random();

        public EmailService(IOptions<EmailSettings> settings, IMemoryCache memoryCache, ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public string GenerateOTP(int length = 6)
        {
            var min = (int)Math.Pow(10, length - 1);
            var max = (int)Math.Pow(10, length) - 1;
            return _random.Next(min, max).ToString();
        }

        public async Task<bool> SendOTPEmailAsync(OTPRequestDto otpRequest)
        {
            try
            {
                _logger.LogInformation("=== EMAIL SERVICE DEBUG ===");
                _logger.LogInformation("Email Settings - Email: {Email}, Password configured: {HasPassword}", 
                    _settings.Email, !string.IsNullOrEmpty(_settings.Password));
                _logger.LogInformation("Environment: {Environment}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));

                if (!IsValidEmail(otpRequest.Email))
                {
                    _logger.LogWarning("Invalid email format: {Email}", otpRequest.Email);
                    return false;
                }

                var otp = GenerateOTP();
                var subject = otpRequest.Purpose switch
                {
                    "password_reset" => "Mã xác thực đặt lại mật khẩu",
                    "verification" => "Mã xác thực tài khoản",
                    _ => "Mã xác thực OTP"
                };

                var body = $@"
Xin chào,

Mã xác thực OTP của bạn là: {otp}

Mã này có hiệu lực trong 5 phút.
Vui lòng không chia sẻ mã này với bất kỳ ai.

Trân trọng,
BookInfoFinder Team
";

                var success = await SendEmailAsync(otpRequest.Email, subject, body);
                
                if (success)
                {
                    // Store OTP for verification
                    await StoreOTPAsync(otpRequest.Email, otp, TimeSpan.FromMinutes(5));
                    _logger.LogInformation("OTP sent successfully to: {Email}", otpRequest.Email);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP email to: {Email}", otpRequest.Email);
                return false;
            }
        }

        public async Task<bool> VerifyOTPAsync(OTPVerifyDto otpVerify)
        {
            try
            {
                var isValid = await ValidateStoredOTPAsync(otpVerify.Email, otpVerify.OTP);
                
                if (isValid)
                {
                    // Clear OTP after successful verification
                    await ClearOTPAsync(otpVerify.Email);
                    _logger.LogInformation("OTP verified successfully for: {Email}", otpVerify.Email);
                }
                else
                {
                    _logger.LogWarning("Invalid OTP verification attempt for: {Email}", otpVerify.Email);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for: {Email}", otpVerify.Email);
                return false;
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(PasswordResetRequestDto resetRequest)
        {
            try
            {
                if (!IsValidEmail(resetRequest.Email))
                {
                    _logger.LogWarning("Invalid email format: {Email}", resetRequest.Email);
                    return false;
                }

                var otp = GenerateOTP();
                var subject = "Đặt lại mật khẩu BookInfoFinder";
                var body = $@"
Xin chào,

Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản BookInfoFinder.

Mã xác thực của bạn là: {otp}

Mã này có hiệu lực trong 5 phút.
Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.

Trân trọng,
BookInfoFinder Team
";

                var success = await SendEmailAsync(resetRequest.Email, subject, body);
                
                if (success)
                {
                    // Store OTP for password reset verification
                    await StoreOTPAsync(resetRequest.Email, otp, TimeSpan.FromMinutes(5));
                    _logger.LogInformation("Password reset email sent to: {Email}", resetRequest.Email);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email to: {Email}", resetRequest.Email);
                return false;
            }
        }

    


        public bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Use built-in email validation
                var emailAttribute = new EmailAddressAttribute();
                return emailAttribute.IsValid(email);
            }
            catch
            {
                return false;
            }
        }

        public async Task StoreOTPAsync(string email, string otp, TimeSpan expiry)
        {
            await Task.CompletedTask; // Async signature for consistency
            
            var otpKey = GetOTPKey(email);
            var otpInfo = new OTPInfo
            {
                OTP = otp,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(expiry)
            };

            _memoryCache.Set(otpKey, otpInfo, expiry);
            _logger.LogDebug("OTP stored for email: {Email}", email);
        }

        public async Task<bool> ValidateStoredOTPAsync(string email, string otp)
        {
            await Task.CompletedTask; // Async signature for consistency
            
            var otpKey = GetOTPKey(email);
            
            if (_memoryCache.TryGetValue(otpKey, out OTPInfo? otpInfo) && otpInfo != null)
            {
                if (DateTime.UtcNow <= otpInfo.ExpiresAt && otpInfo.OTP == otp)
                {
                    return true;
                }
            }

            return false;
        }

        public async Task ClearOTPAsync(string email)
        {
            await Task.CompletedTask; // Async signature for consistency
            
            var otpKey = GetOTPKey(email);
            _memoryCache.Remove(otpKey);
            
            _logger.LogDebug("OTP cleared for email: {Email}", email);
        }

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                _logger.LogInformation("=== SENDING EMAIL ===");
                _logger.LogInformation("To: {ToEmail}, Subject: {Subject}", toEmail, subject);
                _logger.LogInformation("SMTP Config - Email: {Email}, Password configured: {HasPassword}", 
                    _settings.Email, !string.IsNullOrEmpty(_settings.Password));

                if (string.IsNullOrEmpty(_settings.Email) || string.IsNullOrEmpty(_settings.Password))
                {
                    _logger.LogError("Email settings not configured properly");
                    return false;
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("BookInfoFinder", _settings.Email));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;
                message.Body = new TextPart("plain") { Text = body };

                using var client = new SmtpClient();
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                
                // Set timeout ngắn hơn cho production
                client.Timeout = 15000; // 15 seconds

                _logger.LogInformation("Connecting to SMTP server...");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                
                // Thử ports theo thứ tự ưu tiên cho hosting providers
                // Port 2525: Alternative submission port, ít bị hosting chặn nhất
                // Port 587: Standard SMTP port với STARTTLS 
                // Port 1025: Alternative port khi 587 bị chặn
                // Port 465: Legacy SSL port
                var smtpConfigs = new[]
                {
                    new { Host = "smtp.gmail.com", Port = 2525, UseStartTls = true },  // Ưu tiên cao nhất
                    new { Host = "smtp.gmail.com", Port = 587, UseStartTls = true },   // Standard port
                    new { Host = "smtp.gmail.com", Port = 1025, UseStartTls = true },  // Alternative
                    new { Host = "smtp.gmail.com", Port = 465, UseStartTls = false },  // Legacy SSL
                    new { Host = "smtp-mail.outlook.com", Port = 2525, UseStartTls = true }, // Backup
                    new { Host = "smtp-mail.outlook.com", Port = 587, UseStartTls = true }
                };

                Exception? lastException = null;
                
                foreach (var config in smtpConfigs)
                {
                    try
                    {
                        _logger.LogInformation("=== TESTING PORT {Port} ===", config.Port);
                        _logger.LogInformation("Trying SMTP: {Host}:{Port} (StartTLS: {StartTls})", 
                            config.Host, config.Port, config.UseStartTls);
                        
                        var secureOptions = config.UseStartTls 
                            ? MailKit.Security.SecureSocketOptions.StartTls 
                            : MailKit.Security.SecureSocketOptions.SslOnConnect;
                            
                        await client.ConnectAsync(config.Host, config.Port, secureOptions, cts.Token);
                        
                        _logger.LogInformation("✅ Connected! Authenticating...");
                        await client.AuthenticateAsync(_settings.Email, _settings.Password, cts.Token);
                        
                        _logger.LogInformation("✅ Authenticated! Sending email...");
                        await client.SendAsync(message, cts.Token);
                        await client.DisconnectAsync(true, cts.Token);

                        _logger.LogInformation("🎉 EMAIL SENT SUCCESSFULLY via {Host}:{Port}!", config.Host, config.Port);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        var errorType = ex switch
                        {
                            TaskCanceledException => "TIMEOUT - Port có thể bị hosting chặn",
                            System.Net.Sockets.SocketException => "SOCKET ERROR - Hosting chặn port này",
                            MailKit.Security.AuthenticationException => "AUTH FAILED - Kiểm tra email/password",
                            _ => ex.GetType().Name
                        };
                        
                        _logger.LogWarning("❌ Failed {Host}:{Port} - {ErrorType}: {Error}", 
                            config.Host, config.Port, errorType, ex.Message);
                        
                        if (client.IsConnected)
                        {
                            try { await client.DisconnectAsync(true); } catch { }
                        }
                        continue;
                    }
                }

                throw lastException ?? new Exception("All SMTP servers failed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Email sending timed out after 15 seconds for: {Email}", toEmail);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to: {Email}. Error: {ErrorMessage}", toEmail, ex.Message);
                return false;
            }
        }

        private static string GetOTPKey(string email) => $"otp_{email}";

        private class OTPInfo
        {
            public string OTP { get; set; } = "";
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}