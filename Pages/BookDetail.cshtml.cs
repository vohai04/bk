using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookInfoFinder.Pages;

public class BookDetailModel(IBookService books, IBookCommentService comments, IFavoriteService favorites,
    IAuthorService authors, ICategoryService categories, IPublisherService publishers, ITagService tags,
    IRatingService ratings) : PageModel
{
    public BookDetailDto Book { get; private set; } = new();
    private int? UserId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;
    private static JsonResult Failure(string message, int code = 400) => new(new { success = false, message }) { StatusCode = code };
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var book = await books.GetBookByIdAsync(id);
        if (book is null) return NotFound();
        Book = book; return Page();
    }
    public async Task<JsonResult> OnGetGetBookDetailAsync(int id, int page = 1, int pageSize = 10)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 50);
        var book = await books.GetBookByIdAsync(id);
        if (book is null) return Failure("Không tìm thấy sách.", 404);
        var (items, count) = await comments.GetRootCommentsPagedAsync(id, page, pageSize);
        return new(new { success = true, book, comments = items, totalComments = count, currentPage = page,
            totalPages = (int)Math.Ceiling(count / (double)pageSize), currentUserId = UserId });
    }
    public async Task<JsonResult> OnGetGetRepliesAsync(int parentCommentId) => new(new { success = true, replies = await comments.GetRepliesByCommentAsync(parentCommentId) });
    public async Task<JsonResult> OnGetLoadMoreRepliesAsync(int parentCommentId, int skip = 0, int take = 5)
    {
        skip = Math.Max(0, skip); take = Math.Clamp(take, 1, 50);
        var replies = await comments.GetRepliesByCommentAsync(parentCommentId);
        return new(new { success = true, replies = replies.Skip(skip).Take(take), hasMore = replies.Count > skip + take, totalReplies = replies.Count });
    }
    public async Task<JsonResult> OnGetGetCommentPageAsync(int commentId, int pageSize = 10)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var target = await comments.GetCommentByIdAsync(commentId);
        if (target is null) return Failure("Không tìm thấy bình luận.", 404);
        for (var depth = 0; target.ParentCommentId.HasValue && depth < 50; depth++)
        {
            var parent = await comments.GetCommentByIdAsync(target.ParentCommentId.Value);
            if (parent is null) return Failure("Không tìm thấy bình luận gốc.", 404);
            target = parent;
        }
        var roots = await comments.GetRootCommentsByBookAsync(target.BookId);
        var index = roots.FindIndex(c => c.BookCommentId == target.BookCommentId);
        return new(new { success = true, page = index / pageSize + 1, bookId = target.BookId, rootId = target.BookCommentId });
    }
    public async Task<JsonResult> OnGetEntityDetailAsync(string type, int id)
    {
        object? data = type?.ToLowerInvariant() switch
        {
            "author" => await authors.GetAuthorByIdAsync(id),
            "category" => await categories.GetCategoryByIdAsync(id),
            "publisher" => await publishers.GetPublisherByIdAsync(id), _ => null
        };
        return data is null ? Failure("Không tìm thấy thông tin.", 404) : new(new { success = true, data });
    }
    public async Task<JsonResult> OnGetTagDetailAsync(string name)
    {
        var tag = (await tags.GetAllTagsAsync()).FirstOrDefault(t => t.Name == name);
        return tag is null ? Failure("Không tìm thấy chủ đề.", 404) : new(new { success = true, data = tag });
    }
    public JsonResult OnGetCheckLogin() => new(new { isLoggedIn = UserId.HasValue });
    public async Task<JsonResult> OnGetCheckFavoriteAsync(int bookId) => new(new { isFavorite = UserId.HasValue && await favorites.IsFavoriteAsync(UserId.Value, bookId) });
    public async Task<JsonResult> OnPostAddFavoriteAsync(int bookId)
    {
        if (!UserId.HasValue) return Failure("Đăng nhập để lưu sách.", 401);
        if (await books.GetBookByIdAsync(bookId) is null) return Failure("Không tìm thấy sách.", 404);
        if (!await favorites.IsFavoriteAsync(UserId.Value, bookId))
            await favorites.AddToFavoritesAsync(new FavoriteCreateDto { UserId = UserId.Value, BookId = bookId });
        return new(new { success = true, message = "Đã lưu vào tủ sách." });
    }
    public async Task<JsonResult> OnPostAddCommentAsync(int BookId, int Star, string Comment)
    {
        if (!UserId.HasValue) return Failure("Đăng nhập để bình luận.", 401);
        try
        {
            var saved = await comments.CreateRootCommentAsync(new() { BookId = BookId, UserId = UserId.Value, Star = Star, Comment = Comment });
            return new(new { success = true, comment = saved, averageRating = await ratings.GetAverageRatingAsync(BookId), ratingCount = await ratings.GetRatingCountAsync(BookId) });
        }
        catch (ArgumentException error) { return Failure(error.Message); }
    }
    public async Task<JsonResult> OnPostAddReplyAsync(int BookId, int ParentCommentId, string Comment)
    {
        if (!UserId.HasValue) return Failure("Đăng nhập để trả lời.", 401);
        try
        {
            var saved = await comments.CreateReplyAsync(new() { BookId = BookId, UserId = UserId.Value, ParentCommentId = ParentCommentId, Comment = Comment });
            return new(new { success = true, reply = saved });
        }
        catch (ArgumentException error) { return Failure(error.Message); }
    }
    public async Task<JsonResult> OnPostEditCommentAsync(int commentId, string newComment)
    {
        if (!UserId.HasValue) return Failure("Vui lòng đăng nhập.", 401);
        if (!await comments.CanUserEditCommentAsync(commentId, UserId.Value)) return Failure("Bạn không có quyền sửa bình luận này.", 403);
        try { return new(new { success = true, comment = await comments.UpdateCommentAsync(new() { BookCommentId = commentId, Comment = newComment }) }); }
        catch (ArgumentException error) { return Failure(error.Message); }
    }
    public async Task<JsonResult> OnPostDeleteCommentAsync(int commentId)
    {
        if (!UserId.HasValue) return Failure("Vui lòng đăng nhập.", 401);
        if (!await comments.CanUserDeleteCommentAsync(commentId, UserId.Value)) return Failure("Bạn không có quyền xóa bình luận này.", 403);
        return new(new { success = await comments.DeleteCommentAsync(commentId) });
    }
}
