using BookInfoFinder.Services.Interface;
using BookInfoFinder.Models.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookInfoFinder.Pages.Admin;

public sealed class NotificationsModel(IDashboardService dashboard) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "p")] public int CurrentPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public bool UnreadOnly { get; set; }
    public IReadOnlyList<NotificationDto> Items { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPages { get; private set; }
    public int UnreadCount { get; private set; }
    private int UserId => int.Parse(HttpContext.Session.GetString("UserId")!);

    public async Task OnGetAsync()
    {
        CurrentPage = Math.Clamp(CurrentPage, 1, 100000);
        UnreadCount = await dashboard.GetUnreadNotificationCountAsync(UserId);
        var result = await dashboard.GetNotificationsPagedAsync(UserId, CurrentPage, 15, UnreadOnly);
        Total = result.TotalCount;
        TotalPages = (int)Math.Ceiling(Total / 15d);
        if (CurrentPage > Math.Max(1, TotalPages))
        {
            CurrentPage = Math.Max(1, TotalPages);
            result = await dashboard.GetNotificationsPagedAsync(UserId, CurrentPage, 15, UnreadOnly);
        }
        Items = result.Notifications.ToList();
    }

    public async Task<IActionResult> OnPostReadAsync(int notificationId)
    {
        await dashboard.MarkNotificationAsReadAsync(notificationId, UserId);
        return RedirectToPage(new { p = CurrentPage, unreadOnly = UnreadOnly });
    }
    public async Task<IActionResult> OnPostReadAllAsync()
    {
        await dashboard.MarkAllNotificationsAsReadAsync(UserId);
        return RedirectToPage(new { unreadOnly = UnreadOnly });
    }
}
