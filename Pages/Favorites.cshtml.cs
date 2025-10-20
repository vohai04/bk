using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Services.Interface;
 
namespace BookInfoFinder.Pages
{
   public class FavoritesModel : PageModel
   {
       private readonly IFavoriteService _favoriteService;
       public string? UserName { get; set; }
       public const int PageSize = 6;
 
       public FavoritesModel(IFavoriteService favoriteService)
       {
           _favoriteService = favoriteService;
       }
 
       public void OnGet()
       {
           UserName = HttpContext.Session.GetString("UserName");
       }
 
       // AJAX: lấy danh sách yêu thích phân trang
    public async Task<JsonResult> OnGetAjaxFavoritesAsync()
{
    var query = Request.Query;
 
    int page = 1;
    int pageSize = 6;
 
    if (int.TryParse(query["page"], out var p)) page = p;
    if (int.TryParse(query["pageSize"], out var ps)) pageSize = ps;
 
    page = page < 1 ? 1 : page;
    pageSize = pageSize < 1 ? 6 : pageSize;
 
    var userIdStr = HttpContext.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
    {
        return new JsonResult(new
        {
            success = false,
            books = new List<object>(),
            totalPages = 1,
            message = "User not logged in"
        });
    }
 
    var totalBooks = await _favoriteService.GetFavoritesCountByUserAsync(userId);
    var totalPages = totalBooks > 0 ? (int)Math.Ceiling(totalBooks / (double)pageSize) : 1;
    var currentPage = page > totalPages ? totalPages : page;

    var (favorites, totalCount) = await _favoriteService.GetFavoritesByUserPagedAsync(userId, currentPage, pageSize);

    var result = favorites.Select(f => new
    {
        bookId = f.BookId,
        title = f.BookTitle ?? "Không có tiêu đề",
        imageBase64 = f.BookImage,
        author = f.AuthorName ?? "Không rõ",
        category = f.CategoryName ?? "Không rõ",
        tags = f.Tags ?? new List<string>(),
        createdAt = f.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("vi-VN"))
    }).ToList();    return new JsonResult(new
    {
        success = true,
        books = result,
        totalPages = totalPages,
        currentPage = currentPage,
        totalBooks = totalCount
    });
}
 
       // AJAX handler xóa yêu thích
       public async Task<JsonResult> OnPostRemoveFavoriteAsync([FromBody] FavoriteRequest req)
       {
           try
           {
               var userIdStr = HttpContext.Session.GetString("UserId");
               if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                   return new JsonResult(new { success = false, message = "Bạn cần đăng nhập!" });
 
               await _favoriteService.RemoveFromFavoritesAsync(userId, req.BookId);
               return new JsonResult(new { success = true, message = "Đã xóa khỏi yêu thích!" });
           }
           catch (Exception ex)
           {
               Console.WriteLine($"Error in OnPostRemoveFavoriteAsync: {ex.Message}");
               return new JsonResult(new { success = false, message = "Có lỗi khi xóa khỏi yêu thích!" });
           }
       }
   }
 
   public class FavoriteRequest
   {
       public int BookId { get; set; }
   }
}