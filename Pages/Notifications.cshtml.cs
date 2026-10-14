using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Pages;

public sealed class NotificationsModel(IDashboardService dashboard, BookContext context) : PageModel
{
    private int? UserId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;
    public sealed record Item(NotificationDto Notification, string? TargetUrl);
    public List<Item> Items { get; private set; } = [];
    public int CurrentPage { get; private set; }
    public int TotalPages { get; private set; }
    public int UnreadCount { get; private set; }
    public async Task OnGetAsync(int page = 1)
    {
        if (!UserId.HasValue) return;
        CurrentPage = Math.Clamp(page, 1, 100000);
        var result = await dashboard.GetNotificationsPagedAsync(UserId.Value, CurrentPage, 10);
        TotalPages = (int)Math.Ceiling(result.TotalCount / 10d);
        UnreadCount = await dashboard.GetUnreadNotificationCountAsync(UserId.Value);
        var notifications = result.Notifications.ToList();
        var replyIds = notifications.Where(n => n.Type == "comment_reply" && n.RelatedEntityId.HasValue).Select(n => n.RelatedEntityId!.Value).ToList();
        var replies = await context.BookComments.AsNoTracking().Where(c => replyIds.Contains(c.BookCommentId)).ToDictionaryAsync(c => c.BookCommentId, c => c.BookId);
        Items = notifications.Select(n => new Item(n,
            n.Type == "comment_reply" && n.RelatedEntityId.HasValue && replies.TryGetValue(n.RelatedEntityId.Value, out var bookId)
            ? $"/BookDetail/{bookId}#comment-content-{n.RelatedEntityId}"
            : n.RelatedEntityType?.ToLowerInvariant() == "book" && n.RelatedEntityId.HasValue ? $"/BookDetail/{n.RelatedEntityId}" : null)).ToList();
    }
    public async Task<IActionResult> OnGetUnreadCountAsync() => new JsonResult(new { userId = UserId, unread = UserId.HasValue ? await dashboard.GetUnreadNotificationCountAsync(UserId.Value) : 0 });
    public async Task<IActionResult> OnGetPageAsync(int page = 1)
    {
        await OnGetAsync(page);
        return new JsonResult(new { items = Items, total = TotalPages * 10 });
    }
    public async Task<IActionResult> OnPostReadAsync(int notificationId)
    {
        if (!UserId.HasValue) return Unauthorized();
        await dashboard.MarkNotificationAsReadAsync(notificationId, UserId.Value);
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostReadAllAsync()
    {
        if (!UserId.HasValue) return Unauthorized();
        await dashboard.MarkAllNotificationsAsReadAsync(UserId.Value);
        return RedirectToPage();
    }
}
