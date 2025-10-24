using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookInfoFinder.Pages
{
    public class NotificationsModel : PageModel
    {
        private readonly IDashboardService _dashboardService;

        public NotificationsModel(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public class NotificationItem
        {
            public NotificationDto Notification { get; set; } = default!;
            public string? TargetUrl { get; set; }
        }

        public List<NotificationItem> Items { get; set; } = new List<NotificationItem>();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }

        public async Task OnGetAsync(int page = 1, int pageSize = 20)
        {
            Page = Math.Max(1, page);
            PageSize = Math.Max(1, pageSize);

            var userIdStr = HttpContext.Session.GetString("UserId") ?? Request.Cookies["UserId"];
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                // Not logged in or no user id available
                Items = new List<NotificationItem>();
                TotalCount = 0;
                return;
            }

            var (notifications, total) = await _dashboardService.GetNotificationsPagedAsync(userId, Page, PageSize);
            TotalCount = total;

            foreach (var n in notifications)
            {
                string? url = null;
                if (n.Type == "comment_reply")
                {
                    // Ask service for reply details (includes replyId and book info)
                    var details = await _dashboardService.GetReplyDetailsAsync(n.NotificationId);
                    if (details != null)
                    {
                        try
                        {
                            var bookId = details.GetType().GetProperty("bookId")?.GetValue(details)?.ToString();
                            var replyId = details.GetType().GetProperty("replyId")?.GetValue(details)?.ToString();
                                if (!string.IsNullOrEmpty(bookId) && !string.IsNullOrEmpty(replyId))
                                {
                                    url = $"/BookDetail/{bookId}#comment-content-{replyId}";
                                }
                        }
                        catch { }
                    }
                }

                // Fallback: if RelatedEntityType == book and RelatedEntityId present, link to book
                if (url == null && n.RelatedEntityType?.ToLower() == "book" && n.RelatedEntityId.HasValue)
                {
                    url = $"/BookDetail/{n.RelatedEntityId.Value}";
                }

                Items.Add(new NotificationItem { Notification = n, TargetUrl = url });
            }
        }

        // AJAX handler to return current user id and unread count
        public async Task<IActionResult> OnGetUnreadCountAsync()
        {
            var userIdStr = HttpContext.Session.GetString("UserId") ?? Request.Cookies["UserId"];
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return new JsonResult(new { userId = (int?)null, unread = 0 });
            }

            var count = await _dashboardService.GetUnreadNotificationCountAsync(userId);
            return new JsonResult(new { userId = (int?)userId, unread = count });
        }

        // AJAX handler to return a page of notifications as JSON
        public async Task<IActionResult> OnGetPageAsync(int page = 1, int pageSize = 8)
        {
            var userIdStr = HttpContext.Session.GetString("UserId") ?? Request.Cookies["UserId"];
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return new JsonResult(new { items = new object[0], total = 0 });
            }

            var (notifications, total) = await _dashboardService.GetNotificationsPagedAsync(userId, page, pageSize);

            var resultList = new List<object>();
            foreach (var n in notifications)
            {
                string? url = null;
                if (n.Type == "comment_reply")
                {
                    var details = await _dashboardService.GetReplyDetailsAsync(n.NotificationId);
                    if (details != null)
                    {
                        try
                        {
                            var bookId = details.GetType().GetProperty("bookId")?.GetValue(details)?.ToString();
                            var replyId = details.GetType().GetProperty("replyId")?.GetValue(details)?.ToString();
                            if (!string.IsNullOrEmpty(bookId) && !string.IsNullOrEmpty(replyId))
                            {
                                url = $"/BookDetail/{bookId}#comment-content-{replyId}";
                            }
                        }
                        catch { }
                    }
                }

                if (url == null && n.RelatedEntityType?.ToLower() == "book" && n.RelatedEntityId.HasValue)
                {
                    url = $"/BookDetail/{n.RelatedEntityId.Value}";
                }

                resultList.Add(new
                {
                    notification = n,
                    targetUrl = url
                });
            }

            return new JsonResult(new { items = resultList, total });
        }
    }
}
