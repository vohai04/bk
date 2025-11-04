using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using BookInfoFinder.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
 
namespace BookInfoFinder.Pages
{
    public class SearchHistoryModel : PageModel
    {
        private readonly ISearchHistoryService _historyService;
        private readonly IUserService _userService;
 
        public SearchHistoryModel(ISearchHistoryService historyService, IUserService userService)
        {
            _historyService = historyService;
            _userService = userService;
        }

        // Handle page load with URL parameters
        public async Task OnGetAsync(int page = 1)
        {
            // Read page parameter from URL like Users page
            CurrentPage = page < 1 ? 1 : page;
            
            // Pass to ViewData for JavaScript
            ViewData["InitialPage"] = CurrentPage;
        }

        // Properties for pagination like Users page
        [BindProperty(SupportsGet = true)] 
        public int CurrentPage { get; set; } = 1;
 
        // AJAX Get history with pagination
        public async Task<JsonResult> OnGetAjaxGetAsync()
        {
            try
            {
                var query = Request.Query;
                int.TryParse(query["page"], out int page);
                int.TryParse(query["pageSize"], out int pageSize);

                page = page <= 0 ? 1 : page;
                pageSize = pageSize <= 0 ? 10 : pageSize;

                // Get user ID from session with cookie fallback
                var userIdStr = HttpContext.Session.GetString("UserId");
                var cookieUserId = HttpContext.Request.Cookies["UserId"];
                
                if (string.IsNullOrEmpty(userIdStr) && !string.IsNullOrEmpty(cookieUserId))
                {
                    userIdStr = cookieUserId;
                }
                
                if (!int.TryParse(userIdStr, out int userId))
                {
                    return new JsonResult(new { 
                        histories = new List<object>(), 
                        totalCount = 0,
                        totalPages = 0,
                        error = "User not logged in"
                    });
                }

                var (histories, totalCount) = await _historyService.GetSearchHistoriesByUserPagedAsync(userId, page, pageSize);
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                
                var historiesVm = histories.Select(h => new {
                    searchHistoryId = h.SearchHistoryId,
                    searchQuery = h.SearchQuery,
                    resultCount = h.ResultCount,
                    searchedAt = h.SearchedAt,
                    timeAgo = GetTimeAgo(h.SearchedAt),
                    date = h.SearchedAt.ToString("dd/MM/yyyy HH:mm")
                }).ToList();

                return new JsonResult(new { 
                    histories = historiesVm,
                    totalCount = totalCount,
                    totalPages = totalPages
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { 
                    histories = new List<object>(), 
                    totalCount = 0,
                    totalPages = 0,
                    error = $"Có lỗi khi tải lịch sử tìm kiếm: {ex.Message}" 
                });
            }
        }

        // Helper method for time ago like Facebook
        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;
            
            if (timeSpan.TotalMinutes < 1)
                return "Vừa xong";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} phút trước";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} giờ trước";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} ngày trước";
            if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)} tuần trước";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} tháng trước";
            
            return $"{(int)(timeSpan.TotalDays / 365)} năm trước";
        }
 
        // AJAX Delete one
        public async Task<JsonResult> OnPostAjaxDeleteAsync([FromForm] int id)
        {
            await _historyService.DeleteHistoryAsync(id);
            return new JsonResult(new { success = true });
        }
 
        // AJAX Delete all
        public async Task<JsonResult> OnPostAjaxDeleteAllAsync()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return new JsonResult(new { success = false });
 
            await _historyService.DeleteAllHistoriesOfUserAsync(userId);
            return new JsonResult(new { success = true });
        }
    }
}