using BookInfoFinder.Data;
using BookInfoFinder.Models.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Pages.Admin;

public sealed class AuditLogModel(BookContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? EntityType { get; set; }
    [BindProperty(SupportsGet = true)] public string? ActionType { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int CurrentPage { get; set; } = 1;
    public IReadOnlyList<ActivityLog> Entries { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPages { get; private set; }
    public const int PageSize = 20;

    public async Task OnGetAsync()
    {
        CurrentPage = Math.Clamp(CurrentPage, 1, 100000);
        if (From > To) ModelState.AddModelError("From", "Ngày bắt đầu cần trước hoặc bằng ngày kết thúc.");
        if (From?.Year is < 1900 or > 2100 || To?.Year is < 1900 or > 2100)
            ModelState.AddModelError("From", "Chọn ngày trong khoảng 1900–2100.");
        if (!ModelState.IsValid) return;
        var query = db.ActivityLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim().ToLower();
            query = query.Where(a => a.UserName.ToLower().Contains(term) || a.Description.ToLower().Contains(term) || a.Action.ToLower().Contains(term));
        }
        if (!string.IsNullOrEmpty(EntityType)) query = query.Where(a => a.EntityType == EntityType);
        if (!string.IsNullOrEmpty(ActionType))
        {
            query = ActionType switch
            {
                "Created" => query.Where(a => a.Action.Contains("Created") || a.Action.Contains("Registered")),
                "Updated" => query.Where(a => a.Action.Contains("Updated")),
                "Deleted" => query.Where(a => a.Action.Contains("Deleted")),
                "StatusChanged" => query.Where(a => a.Action == "StatusChanged"),
                _ => query.Where(a => false)
            };
        }
        // Filters use the reader's local day in Vietnam; records remain stored in UTC.
        if (From is { } from)
        {
            var utcStart = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddHours(-7);
            query = query.Where(a => a.CreatedAt >= utcStart);
        }
        if (To is { } to)
        {
            var utcEnd = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddHours(-7);
            query = query.Where(a => a.CreatedAt < utcEnd);
        }
        Total = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(Total / (double)PageSize);
        CurrentPage = Math.Min(CurrentPage, Math.Max(1, TotalPages));
        Entries = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.ActivityId)
            .Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToListAsync();
    }

    public static string ActionLabel(string action) => action.Contains("Created") || action.Contains("Registered") ? "Tạo mới" : action.Contains("Updated") ? "Cập nhật" : action.Contains("Deleted") ? "Xóa" : action == "StatusChanged" ? "Đổi trạng thái" : action;
    public static string EntityLabel(string type) => type switch { "Book" => "Sách", "User" => "Người dùng", "Author" => "Tác giả", "Category" => "Thể loại", "Tag" => "Thẻ sách", "Publisher" => "Nhà xuất bản", "Comment" => "Bình luận", _ => type };
    public static string ActionClass(string action) => action.Contains("Deleted") ? "danger" : action.Contains("Updated") ? "amber" : action == "StatusChanged" ? "neutral" : "green";
}
