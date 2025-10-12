namespace BookInfoFinder.Models.Dto
{
    public class OTPRequestDto
    {
        public string Email { get; set; } = "";
        public string Purpose { get; set; } = ""; // "password_reset" or "verification"
    }

    public class OTPVerifyDto
    {
        public string Email { get; set; } = "";
        public string OTP { get; set; } = "";
        public string Purpose { get; set; } = "";
    }

    public class PasswordResetRequestDto
    {
        public string Email { get; set; } = "";
    }
}