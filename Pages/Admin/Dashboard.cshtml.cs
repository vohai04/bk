using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Pages.Admin;

public sealed class DashboardModel(BookContext db, IDashboardService dashboard, ILogger<DashboardModel> logger) : PageModel
{
    public DashboardStatsDto Stats { get; private set; } = new();
    public List<LibraryBook> Books { get; private set; } = [];
    public List<CategorySummary> Categories { get; private set; } = [];
    public int MissingCovers { get; private set; }
    public int MissingSummaries { get; private set; }
    public string? LoadError { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            Stats = await dashboard.GetDashboardStatsAsync();
            Books = await db.Books.AsNoTracking().OrderByDescending(b => b.BookId).Take(5)
                .Select(b => new LibraryBook(b.BookId, b.Title, b.Author != null ? b.Author.Name : "Chưa rõ tác giả", b.Category != null ? b.Category.Name : "Chưa phân loại", b.ImageBase64, b.ISBN)).ToListAsync();
            Categories = await db.Categories.AsNoTracking().OrderByDescending(c => c.Books.Count)
                .ThenBy(c => c.Name).Take(5).Select(c => new CategorySummary(c.Name, c.Books.Count)).ToListAsync();
            MissingCovers = await db.Books.CountAsync(b => b.ImageBase64 == null || b.ImageBase64 == "");
            MissingSummaries = await db.Books.CountAsync(b => b.Abstract == "");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load admin overview");
            LoadError = "Chưa tải được tổng quan. Kiểm tra kết nối cơ sở dữ liệu rồi tải lại trang.";
        }
    }

    public async Task<IActionResult> OnGetGetStatsAsync() => new JsonResult(await dashboard.GetDashboardStatsAsync());
    public Task<IActionResult> OnPostGetStatsAsync() => OnGetGetStatsAsync();

    public sealed record LibraryBook(int Id, string Title, string Author, string Category, string? Cover, string Isbn);
    public sealed record CategorySummary(string Name, int Count);
}
