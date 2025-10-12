using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BookInfoFinder.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(IUserService userService, ILogger<RegisterModel> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [BindProperty]
        public RegisterRequestModel RegisterRequest { get; set; } = new();
        
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Kiểm tra tên đăng nhập và email đã tồn tại chưa
                bool userNameExists = await _userService.IsUserNameExistsAsync(RegisterRequest.Username);
                bool emailExists = await _userService.IsEmailExistsAsync(RegisterRequest.Email);

                if (userNameExists || emailExists)
                {
                    ErrorMessage = "Tên đăng nhập hoặc email đã được sử dụng.";
                    return Page();
                }

                // Tạo user mới
                var userCreateDto = new UserCreateDto
                {
                    FullName = RegisterRequest.FullName,
                    UserName = RegisterRequest.Username,
                    Email = RegisterRequest.Email,
                    Password = RegisterRequest.Password,
                    Role = "User"
                };

                _logger.LogInformation("Attempting to create user: {Username}, {Email}", RegisterRequest.Username, RegisterRequest.Email);
                await _userService.CreateUserAsync(userCreateDto);
                _logger.LogInformation("User created successfully: {Username}", RegisterRequest.Username);

                _logger.LogInformation("New user registered: {Username}, Email: {Email}", RegisterRequest.Username, RegisterRequest.Email);

                return RedirectToPage("/Account/Login", new { message = "Đăng ký thành công! Vui lòng đăng nhập." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for user {Username}. Exception: {Exception}", RegisterRequest.Username, ex.ToString());
                ErrorMessage = $"Đã xảy ra lỗi trong quá trình đăng ký: {ex.Message}";
                return Page();
            }
        }
    }

    public class RegisterRequestModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3-50 ký tự")]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}