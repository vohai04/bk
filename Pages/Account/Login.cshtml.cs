using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BookInfoFinder.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(IUserService userService, ILogger<LoginModel> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [BindProperty]
        public LoginRequestModel LoginRequest { get; set; } = new();
        
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public IActionResult OnGet(string? message = null)
        {
            // Nếu đã đăng nhập rồi thì redirect
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
            {
                var role = HttpContext.Session.GetString("Role");
                if (role == "Admin")
                {
                    return RedirectToPage("/Admin/Dashboard");
                }
                else
                {
                    return RedirectToPage("/Index");
                }
            }

            if (!string.IsNullOrEmpty(message))
            {
                SuccessMessage = message;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var loginRequest = new LoginRequestDto
                {
                    UserName = LoginRequest.Username,
                    Password = LoginRequest.Password
                };

                var user = await _userService.ValidateUserAsync(loginRequest);
                
                if (user == null)
                {
                    ErrorMessage = "Tên đăng nhập hoặc mật khẩu không đúng.";
                    return Page();
                }

                // Kiểm tra status (1 = active, 0 = inactive)
                if (user.Status == 0)
                {
                    ErrorMessage = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.";
                    return Page();
                }

                // Lưu thông tin user vào session
                HttpContext.Session.SetString("UserId", user.UserId.ToString());
                HttpContext.Session.SetString("UserName", user.UserName);
                HttpContext.Session.SetString("FullName", user.FullName);
                HttpContext.Session.SetString("Email", user.Email);
                HttpContext.Session.SetString("Role", user.Role);
                HttpContext.Session.SetString("Status", user.Status.ToString());

                _logger.LogInformation("User {Username} logged in successfully", LoginRequest.Username);

                // Chuyển hướng dựa trên role
                if (user.Role == "Admin")
                {
                    return RedirectToPage("/Admin/Dashboard");
                }
                else
                {
                    return LocalRedirect(returnUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {Username}", LoginRequest.Username);
                ErrorMessage = "Đã xảy ra lỗi trong quá trình đăng nhập. Vui lòng thử lại.";
                return Page();
            }
        }
    }

    public class LoginRequestModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; } = false;
    }
}