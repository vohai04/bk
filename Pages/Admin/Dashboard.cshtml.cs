using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Services.Interface;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models;

namespace BookInfoFinder.Pages.Admin
{
    public class DashboardModel : PageModel
    {
        private readonly IDashboardService _dashboardService;
        private readonly IUserService _userService;

        public DashboardModel(IDashboardService dashboardService, IUserService userService)
        {
            _dashboardService = dashboardService;
            _userService = userService;
        }

        public DashboardStatsDto Stats { get; set; } = new();
        public IEnumerable<RecentActivityDto> RecentActivities { get; set; } = new List<RecentActivityDto>();
        public IEnumerable<ActivityLogDto> ActivityLogs { get; set; } = new List<ActivityLogDto>();
        public IEnumerable<NotificationDto> Notifications { get; set; } = new List<NotificationDto>();
        public int UnreadNotificationCount { get; set; }

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
                // Get current user ID for notifications
                var userName = HttpContext.Session.GetString("UserName");
                var currentUser = await _userService.GetUserByEmailAsync(userName ?? "");

                // Load dashboard data
                Stats = await _dashboardService.GetDashboardStatsAsync();
                RecentActivities = await _dashboardService.GetRecentActivitiesForDisplayAsync(12);
                // Activity logs will be loaded via AJAX with paging (OnGetGetActivityLogsAsync)
                ActivityLogs = Enumerable.Empty<ActivityLogDto>();

                if (currentUser != null)
                {
                    Notifications = await _dashboardService.GetNotificationsAsync(currentUser.UserId, false, 10);
                    UnreadNotificationCount = await _dashboardService.GetUnreadNotificationCountAsync(currentUser.UserId);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi khi tải dữ liệu dashboard: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostMarkNotificationReadAsync(int notificationId)
        {
            try
            {
                var userName = HttpContext.Session.GetString("UserName");
                var currentUser = await _userService.GetUserByEmailAsync(userName ?? "");

                if (currentUser != null)
                {
                    await _dashboardService.MarkNotificationAsReadAsync(notificationId, currentUser.UserId);
                }

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostMarkAllNotificationsReadAsync()
        {
            try
            {
                var userName = HttpContext.Session.GetString("UserName");
                var currentUser = await _userService.GetUserByEmailAsync(userName ?? "");

                if (currentUser != null)
                {
                    await _dashboardService.MarkAllNotificationsAsReadAsync(currentUser.UserId);
                }

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostGetStatsAsync()
        {
            try
            {
                var stats = await _dashboardService.GetDashboardStatsAsync();
                return new JsonResult(new
                {
                    totalBooks = stats.TotalBooks,
                    totalUsers = stats.TotalUsers,
                    totalCategories = stats.TotalCategories,
                    totalComments = stats.TotalComments,
                    newBooksToday = stats.NewBooksToday,
                    newUsersToday = stats.NewUsersToday,
                    newCommentsToday = stats.NewCommentsToday,
                    activeUsersToday = stats.ActiveUsersToday,
                    unreadNotificationCount = await GetUnreadNotificationCountAsync()
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostGetActivitiesAsync(int page = 1, int pageSize = 10)
        {
            try
            {
                // Use the paged service to get correct total count and items
                var (activities, total) = await _dashboardService.GetRecentActivitiesPagedAsync(page, pageSize);

                return new JsonResult(new
                {
                    activities = activities.Select(a => new
                    {
                        type = a.Type,
                        title = a.Title,
                        description = a.Description,
                        userName = a.UserName,
                        createdAt = a.CreatedAt.ToString("O"),
                        entityId = a.EntityId,
                        entityType = a.EntityType,
                        actionUrl = a.ActionUrl
                    }),
                    sampleTitles = activities.Select(a => a.Title).Take(10).ToArray(),
                    currentPage = page,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // Keep POST version for compatibility, but prefer GET for client-side refresh/pagination
        public async Task<IActionResult> OnPostGetNotificationsAsync()
        {
            return await OnGetGetNotificationsAsync();
        }

        // Mirror POST handlers with GET endpoints so client can call via GET (like Index page)
        public async Task<IActionResult> OnGetGetActivitiesAsync(int page = 1, int pageSize = 10)
        {
            try
            {
                // Prefer explicit query string if provided (diagnostic for pagination issues)
                var qsPage = 1;
                if (Request.Query.ContainsKey("page") && int.TryParse(Request.Query["page"].FirstOrDefault(), out var parsed))
                {
                    qsPage = parsed;
                }

                var usePage = qsPage > 0 ? qsPage : page;

                var (activities, total) = await _dashboardService.GetRecentActivitiesPagedAsync(usePage, pageSize);

                return new JsonResult(new
                {
                    activities = activities.Select(a => new
                    {
                        type = a.Type,
                        title = a.Title,
                        description = a.Description,
                        userName = a.UserName,
                        createdAt = a.CreatedAt.ToString("O"),
                        entityId = a.EntityId,
                        entityType = a.EntityType,
                        actionUrl = a.ActionUrl
                    }),
                    sampleTitles = activities.Select(a => a.Title).Take(10).ToArray(),
                    currentPage = usePage,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize),
                    debugQuery = Request.QueryString.Value
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnGetGetNotificationsAsync(int page = 1, int pageSize = 20, bool unreadOnly = false)
        {
            try
            {
                var userName = HttpContext.Session.GetString("UserName");
                var currentUser = await _userService.GetUserByEmailAsync(userName ?? "");

                if (currentUser == null)
                    return new JsonResult(new { notifications = new List<object>(), unreadCount = 0, totalPages = 0 });

                var (items, total) = await _dashboardService.GetNotificationsPagedAsync(currentUser.UserId, page, pageSize, unreadOnly);
                var unreadCount = await _dashboardService.GetUnreadNotificationCountAsync(currentUser.UserId);

                return new JsonResult(new
                {
                    notifications = items.Select(n => new
                    {
                        notificationId = n.NotificationId,
                        title = n.Title,
                        message = n.Message,
                        type = n.Type,
                        isRead = n.IsRead,
                        createdAt = n.CreatedAt.ToString("O"),
                        relatedEntityId = n.RelatedEntityId,
                        relatedEntityType = n.RelatedEntityType
                    }),
                    unreadCount = unreadCount,
                    currentPage = page,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnGetGetActivityLogsAsync(int page = 1, int pageSize = 10, DateTime? startDate = null, DateTime? endDate = null, string? entityType = null)
        {
            try
            {
                // Prefer explicit query string 'page' if provided (same approach as activities handler)
                var qsPage = 1;
                if (Request.Query.ContainsKey("page") && int.TryParse(Request.Query["page"].FirstOrDefault(), out var parsed))
                {
                    qsPage = parsed;
                }

                var usePage = qsPage > 0 ? qsPage : page;

                // Pass filter parameters to service so total count reflects filters
                var (logs, total) = await _dashboardService.GetActivityLogsPagedAsync(usePage, pageSize, startDate, endDate, entityType);

                var list = logs.ToList();
                return new JsonResult(new
                {
                    logs = list.Select(l => new
                    {
                        activityId = l.ActivityId,
                        userName = l.UserName,
                        action = l.Action,
                        description = l.Description,
                        entityType = l.EntityType,
                        entityId = l.EntityId,
                        createdAt = l.CreatedAt.ToString("O"),
                        ipAddress = l.IpAddress
                    }),
                    currentPage = usePage,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize),
                    sampleNames = list.Select(l => l.Action).Take(10).ToArray()
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // Provide GET stats handler (used by client-side refresh) to avoid POST token issues
        public async Task<IActionResult> OnGetGetStatsAsync()
        {
            try
            {
                var stats = await _dashboardService.GetDashboardStatsAsync();
                return new JsonResult(new
                {
                    totalBooks = stats.TotalBooks,
                    totalUsers = stats.TotalUsers,
                    totalCategories = stats.TotalCategories,
                    totalComments = stats.TotalComments,
                    newBooksToday = stats.NewBooksToday,
                    newUsersToday = stats.NewUsersToday,
                    newCommentsToday = stats.NewCommentsToday,
                    newCategoriesToday = stats.NewCategoriesToday,
                    activeUsersToday = stats.ActiveUsersToday,
                    unreadNotificationCount = await GetUnreadNotificationCountAsync()
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        private async Task<int> GetUnreadNotificationCountAsync()
        {
            try
            {
                var userName = HttpContext.Session.GetString("UserName");
                var currentUser = await _userService.GetUserByEmailAsync(userName ?? "");
                return currentUser != null ? await _dashboardService.GetUnreadNotificationCountAsync(currentUser.UserId) : 0;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<IActionResult> OnPostGetReplyDetailsAsync(int notificationId)
        {
            try
            {
                var userName = HttpContext.Session.GetString("UserName");
                var currentUser = await _userService.GetUserByEmailAsync(userName ?? "");

                if (currentUser == null)
                    return new JsonResult(new { success = false, message = "User not found" });

                // Get notification details
                var notifications = await _dashboardService.GetNotificationsAsync(currentUser.UserId, false, 50);
                var notification = notifications.FirstOrDefault(n => n.NotificationId == notificationId);

                if (notification == null || notification.Type != "comment_reply")
                    return new JsonResult(new { success = false, message = "Notification not found or not a reply" });

                // Get reply details from dashboard service
                var replyDetails = await _dashboardService.GetReplyDetailsAsync(notificationId);

                return new JsonResult(new
                {
                    success = true,
                    reply = replyDetails
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}