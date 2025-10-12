using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Services.Interface;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models;
 
namespace BookInfoFinder.Pages.Admin
{
    public class DashboardModel : PageModel
    {
        private readonly IReportService _reportService;
 
        public DashboardModel(IReportService reportService)
        {
            _reportService = reportService;
        }
        public int TotalBooks { get; set; }
        public int TotalUsers { get; set; }
        public int TotalCategories { get; set; }
        public int TotalTags { get; set; }
        public int ToTalUsers { get; set; }
        public int TotalAuthors { get; set; }
        public int TotalPublishers { get; set; }
 
        public async Task<IActionResult> OnGetAsync()
        {
            // Check authentication using session
            var userRole = HttpContext.Session.GetString("Role");
            if (string.IsNullOrEmpty(userRole) || !userRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền truy cập trang này.";
                return RedirectToPage("/Account/Login");
            }

            try
            {
                var report = await _reportService.GetDashboardReportAsync();
                TotalBooks = report.TotalBooks;
                TotalUsers = report.TotalUsers;
                TotalAuthors = report.TotalAuthors;
                TotalPublishers = report.TotalPublishers;
                TotalCategories = report.TotalCategories;
                TotalTags = report.TotalTags;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi khi tải dữ liệu: {ex.Message}";
            }

            return Page();
        }
    }
}