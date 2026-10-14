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
        public void OnGet(int page = 1)
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
                pageSize = Math.Clamp(pageSize <= 0 ? 10 : pageSize, 1, 50);

                // Identity comes from the server session only.
                var userIdStr = HttpContext.Session.GetString("UserId");

                if (!int.TryParse(userIdStr, out int userId))
                {
                    return new JsonResult(new
                    {
                        histories = new List<object>(),
                        totalCount = 0,
                        totalPages = 0,
                        showToast = true,
                        toastMessage = "Bạn cần đăng nhập để xem lịch sử tìm kiếm"
                    });
                }

                var (histories, totalCount) = await _historyService.GetSearchHistoriesByUserPagedAsync(userId, page, pageSize);
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var historiesVm = histories.Select(h => new
                {
                    searchHistoryId = h.SearchHistoryId,
                    searchQuery = h.SearchQuery,
                    resultCount = h.ResultCount,
                    searchedAt = h.SearchedAt,
                    date = h.SearchedAt
                }).ToList();

                return new JsonResult(new
                {
                    histories = historiesVm,
                    totalCount = totalCount,
                    totalPages = totalPages
                });
            }
            catch (Exception)
            {
                return new JsonResult(new
                {
                    histories = new List<object>(),
                    totalCount = 0,
                    totalPages = 0,
                    error = "Không tải được lịch sử tìm kiếm. Vui lòng thử lại."
                });
            }
        }

        // AJAX Delete one
        public async Task<JsonResult> OnPostAjaxDeleteAsync([FromForm] int id)
        {
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
                return new JsonResult(new { success = false }) { StatusCode = 401 };
            var item = await _historyService.GetHistoryByIdAsync(id);
            if (item is null || item.UserId != userId)
                return new JsonResult(new { success = false }) { StatusCode = 403 };
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
