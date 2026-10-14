using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services;

public sealed class BookCommentService(BookContext context, IDashboardService dashboard, IHttpContextAccessor http) : IBookCommentService
{
    private IQueryable<BookComment> Query => context.BookComments.AsNoTracking().Include(c => c.User);
    private static int Size(int size) => Math.Clamp(size, 1, 50);
    private static string Text(string? text)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 500)
            throw new ArgumentException("Bình luận cần từ 1 đến 500 ký tự.");
        return text;
    }
    private async Task ValidateAsync(BookCommentCreateDto dto)
    {
        dto.Comment = Text(dto.Comment);
        if (!await context.Users.AnyAsync(u => u.UserId == dto.UserId && u.Status == 1))
            throw new ArgumentException("Tài khoản không còn hoạt động.");
        if (!await context.Books.AnyAsync(b => b.BookId == dto.BookId))
            throw new ArgumentException("Không tìm thấy sách.");
    }
    private static BookCommentDto Map(BookComment c) => new()
    {
        BookCommentId = c.BookCommentId, BookId = c.BookId, UserId = c.UserId,
        AvatarUrl = AvatarUrls.For(c.UserId, c.User?.AvatarVersion),
        UserName = c.User?.UserName ?? "Người đọc", Role = (int)(c.User?.Role ?? Models.Role.User),
        RoleName = (c.User?.Role ?? Models.Role.User).ToString(), Comment = c.Comment, Star = c.Star,
        ParentCommentId = c.ParentCommentId, CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
    };
    // Build reply trees from one query rather than querying each node recursively.
    private async Task<ILookup<int, BookCommentDto>> ReplyLookup(int bookId)
    {
        var items = await Query.Where(c => c.BookId == bookId && c.ParentCommentId != null)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.BookCommentId).ToListAsync();
        return items.Select(Map).ToLookup(c => c.ParentCommentId!.Value);
    }
    private static List<BookCommentDto> Tree(int parent, ILookup<int, BookCommentDto> lookup, int depth = 0)
    {
        if (depth > 50) return [];
        return lookup[parent].Select(item =>
        {
            item.Replies = Tree(item.BookCommentId, lookup, depth + 1);
            item.ReplyCount = item.Replies.Count;
            item.TotalRepliesCount = item.Replies.Sum(r => 1 + r.TotalRepliesCount);
            return item;
        }).ToList();
    }
    public async Task<(List<BookCommentDto> Comments, int TotalCount)> GetRootCommentsPagedAsync(int bookId, int page, int pageSize)
    {
        pageSize = Size(pageSize); page = Math.Clamp(page, 1, 100000);
        var query = Query.Where(c => c.BookId == bookId && c.ParentCommentId == null);
        var count = await query.CountAsync();
        var entities = await query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.BookCommentId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var lookup = await ReplyLookup(bookId);
        var comments = entities.Select(Map).ToList();
        foreach (var comment in comments)
        {
            comment.Replies = Tree(comment.BookCommentId, lookup);
            comment.ReplyCount = comment.Replies.Count;
            comment.TotalRepliesCount = comment.Replies.Sum(r => 1 + r.TotalRepliesCount);
        }
        return (comments, count);
    }
    public async Task<List<BookCommentDto>> GetRootCommentsByBookAsync(int bookId)
    {
        var items = await Query.Where(c => c.BookId == bookId && c.ParentCommentId == null)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.BookCommentId).ToListAsync();
        return items.Select(Map).ToList();
    }
    public async Task<List<BookCommentDto>> GetRepliesByCommentAsync(int parentCommentId)
    {
        var bookId = await context.BookComments.Where(c => c.BookCommentId == parentCommentId).Select(c => (int?)c.BookId).FirstOrDefaultAsync();
        return bookId.HasValue ? Tree(parentCommentId, await ReplyLookup(bookId.Value)) : [];
    }
    public async Task<(List<BookCommentDto> Replies, int TotalCount)> GetRepliesPagedAsync(int parentCommentId, int page, int pageSize)
    {
        var replies = await GetRepliesByCommentAsync(parentCommentId);
        return (replies.Skip((Math.Clamp(page, 1, 100000) - 1) * Size(pageSize)).Take(Size(pageSize)).ToList(), replies.Count);
    }
    public async Task<BookCommentDto?> GetCommentByIdAsync(int commentId)
    {
        var item = await Query.FirstOrDefaultAsync(c => c.BookCommentId == commentId);
        return item is null ? null : Map(item);
    }
    public async Task<BookCommentDto> CreateRootCommentAsync(BookCommentCreateDto dto)
    {
        await ValidateAsync(dto);
        if (dto.ParentCommentId != null || dto.Star is null or < 1 or > 5)
            throw new ArgumentException("Chọn đánh giá từ 1 đến 5 sao.");
        await using var transaction = await context.Database.BeginTransactionAsync();
        var comment = dto.ToEntity(); context.BookComments.Add(comment);
        var rating = await context.Ratings.SingleOrDefaultAsync(r => r.UserId == dto.UserId && r.BookId == dto.BookId);
        if (rating is null) context.Ratings.Add(new Rating { UserId = dto.UserId, BookId = dto.BookId, Star = dto.Star.Value, CreatedAt = DateTime.UtcNow });
        else { rating.Star = dto.Star.Value; rating.UpdatedAt = DateTime.UtcNow; }
        await context.SaveChangesAsync(); await Audit(comment, "Comment Created");
        await transaction.CommitAsync();
        return (await GetCommentByIdAsync(comment.BookCommentId))!;
    }
    public async Task<BookCommentDto> CreateReplyAsync(BookCommentCreateDto dto)
    {
        await ValidateAsync(dto);
        if (dto.ParentCommentId is null || dto.Star != null ||
            !await context.BookComments.AnyAsync(c => c.BookCommentId == dto.ParentCommentId && c.BookId == dto.BookId))
            throw new ArgumentException("Phản hồi phải thuộc bình luận trong cùng sách.");
        await using var transaction = await context.Database.BeginTransactionAsync();
        var comment = dto.ToEntity(); context.BookComments.Add(comment);
        await context.SaveChangesAsync(); await Audit(comment, "Reply Created");
        await transaction.CommitAsync();
        await dashboard.CreateCommentReplyNotificationAsync(comment.BookCommentId, comment.UserId);
        return (await GetCommentByIdAsync(comment.BookCommentId))!;
    }
    private async Task Audit(BookComment comment, string action)
    {
        var name = await context.Users.Where(u => u.UserId == comment.UserId).Select(u => u.UserName).SingleAsync();
        await dashboard.LogActivityAsync(name, action, $"Bình luận #{comment.BookCommentId} trong sách #{comment.BookId}", "BookComment", comment.BookCommentId, http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "");
    }
    public async Task<BookCommentDto> UpdateCommentAsync(BookCommentUpdateDto dto)
    {
        var comment = await context.BookComments.FindAsync(dto.BookCommentId) ?? throw new ArgumentException("Không tìm thấy bình luận.");
        comment.Comment = Text(dto.Comment); comment.UpdatedAt = DateTime.UtcNow;
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SaveChangesAsync(); await Audit(comment, "Comment Updated"); await transaction.CommitAsync();
        return (await GetCommentByIdAsync(comment.BookCommentId))!;
    }
    public async Task<bool> DeleteCommentAsync(int commentId)
    {
        var comment = await context.BookComments.FindAsync(commentId);
        if (comment is null) return false;
        await using var transaction = await context.Database.BeginTransactionAsync();
        await Audit(comment, "Comment Deleted"); context.BookComments.Remove(comment);
        await context.SaveChangesAsync(); await transaction.CommitAsync(); return true;
    }
    public Task<int> GetTotalCommentsCountByBookAsync(int bookId) => context.BookComments.CountAsync(c => c.BookId == bookId);
    public Task<int> GetRootCommentsCountByBookAsync(int bookId) => context.BookComments.CountAsync(c => c.BookId == bookId && c.ParentCommentId == null);
    public async Task<int> GetRepliesCountByCommentAsync(int parentCommentId) => (await GetRepliesByCommentAsync(parentCommentId)).Sum(r => 1 + r.TotalRepliesCount);
    public Task<bool> CanUserDeleteCommentAsync(int commentId, int userId) => context.BookComments.AnyAsync(c => c.BookCommentId == commentId && c.UserId == userId);
    public Task<bool> CanUserEditCommentAsync(int commentId, int userId) => CanUserDeleteCommentAsync(commentId, userId);
    public async Task<BookCommentDto?> GetCommentWithRepliesAsync(int commentId, int replyPage = 1, int replyPageSize = 5)
    {
        var comment = await GetCommentByIdAsync(commentId);
        if (comment != null) comment.Replies = (await GetRepliesPagedAsync(commentId, replyPage, replyPageSize)).Replies;
        return comment;
    }
    public async Task<List<BookCommentDto>> GetCommentsTreeByBookAsync(int bookId, int maxDepth = 2) => (await GetRootCommentsPagedAsync(bookId, 1, 50)).Comments;
}
