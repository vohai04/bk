using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookInfoFinder.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(ILogger<LogoutModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            // Nếu chưa đăng nhập thì redirect về trang chủ
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
            {
                return RedirectToPage("/Index");
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            try
            {
                var userName = HttpContext.Session.GetString("UserName");
                
                // Xóa toàn bộ session
                HttpContext.Session.Clear();
                
                _logger.LogInformation("User {Username} logged out successfully", userName);
                
                return RedirectToPage("/Index", new { message = "Bạn đã đăng xuất thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return RedirectToPage("/Index");
            }
        }
    }
}