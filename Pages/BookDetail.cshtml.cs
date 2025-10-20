using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
namespace BookInfoFinder.Pages;

public class BookDetailModel : PageModel
{
    private readonly IBookService _bookService;
    private readonly IBookCommentService _bookCommentService;
    private readonly IFavoriteService _favoriteService;
    private readonly IAuthorService _authorService;
    private readonly IPublisherService _publisherService;
    private readonly IRatingService _ratingService;
    private readonly IUserService _userService;

    public BookDetailDto? Book { get; set; }

    public BookDetailModel(
        IUserService userService,
        IRatingService ratingService,
        IBookService bookService,
        IBookCommentService bookCommentService,
        IFavoriteService favoriteService,
        IAuthorService authorService,
        IPublisherService publisherService)
    {
        _bookService = bookService;
        _bookCommentService = bookCommentService;
        _favoriteService = favoriteService;
        _authorService = authorService;
        _publisherService = publisherService;
        _ratingService = ratingService;
        _userService = userService;
    }

    // Generic handler to fetch details for author/category/publisher by id
    public async Task<JsonResult> OnGetEntityDetailAsync(string type, int id)
    {
        if (string.IsNullOrEmpty(type)) return new JsonResult(new { success = false, message = "Missing type" });

        try
        {
            switch (type.ToLower())
            {
                case "author":
                    var author = await _authorService.GetAuthorByIdAsync(id);
                    if (author == null) return new JsonResult(new { success = false, message = "Không tìm thấy tác giả" });
                    return new JsonResult(new { success = true, data = author });
                case "category":
                    var category = await (_publisherService is null ? Task.FromResult<CategoryDto?>(null) : Task.FromResult<CategoryDto?>(null));
                    // Use CategoryService if available via DI through PageModel - try to resolve via HttpContext.RequestServices
                    var categoryService = HttpContext.RequestServices.GetService(typeof(BookInfoFinder.Services.Interface.ICategoryService)) as BookInfoFinder.Services.Interface.ICategoryService;
                    if (categoryService != null)
                    {
                        category = await categoryService.GetCategoryByIdAsync(id);
                        if (category == null) return new JsonResult(new { success = false, message = "Không tìm thấy thể loại" });
                        return new JsonResult(new { success = true, data = category });
                    }
                    return new JsonResult(new { success = false, message = "Service category không khả dụng" });
                case "publisher":
                    var publisher = await _publisherService.GetPublisherByIdAsync(id);
                    if (publisher == null) return new JsonResult(new { success = false, message = "Không tìm thấy nhà xuất bản" });
                    return new JsonResult(new { success = true, data = publisher });
                default:
                    return new JsonResult(new { success = false, message = "Loại không hợp lệ" });
            }
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    // Handler to fetch Tag details by name (TagDto contains nice fields)
    public async Task<JsonResult> OnGetTagDetailAsync(string name)
    {
        if (string.IsNullOrEmpty(name)) return new JsonResult(new { success = false, message = "Missing name" });
        try
        {
            var tagService = HttpContext.RequestServices.GetService(typeof(BookInfoFinder.Services.Interface.ITagService)) as BookInfoFinder.Services.Interface.ITagService;
            if (tagService == null) return new JsonResult(new { success = false, message = "Service tag không khả dụng" });
            var all = await tagService.SearchTagsAsync(name);
            var found = all.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (found == null) return new JsonResult(new { success = false, message = "Không tìm thấy tag" });
            return new JsonResult(new { success = true, data = found });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    // Lấy danh sách bình luận gốc (phân trang) - Facebook style
    public async Task<JsonResult> OnGetGetBookDetailAsync(int id)
    {
        // Lấy page và pageSize từ query string
        var query = Request.Query;
        int page = 1;
        int pageSize = 10; // Facebook style - ít comments hơn mỗi trang

        if (int.TryParse(query["page"], out var p)) page = p;
        if (int.TryParse(query["pageSize"], out var ps)) pageSize = ps;
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : pageSize;

        var detail = await _bookService.GetBookDetailWithStatsAndCommentsAsync(id, page, pageSize);
        if (detail == null)
            return new JsonResult(new { success = false, message = "Không tìm thấy sách!" });

        // Enhance comments with nested structure - Facebook style
        var enhancedComments = new List<object>();
        foreach (var comment in detail.Comments)
        {
            // Get replies for each comment (limit 3 latest by default - like Facebook)
            var replies = await _bookCommentService.GetRepliesByCommentAsync(comment.BookCommentId);
            var latestReplies = replies.Take(3).ToList(); // Show only 3 latest replies initially
            
            enhancedComments.Add(new
            {
                bookCommentId = comment.BookCommentId,
                userId = comment.UserId,
                comment = comment.Comment,
                star = comment.Star,
                userName = comment.UserName,
                roleName = comment.RoleName,
                // Adjusted to parse `CreatedAt` and `UpdatedAt` as DateTime before formatting
                createdAt = comment.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("vi-VN")),
                timeAgo = GetTimeAgo(comment.CreatedAt),
                repliesCount = replies.Count,
                replies = latestReplies.Select(r => new
                {
                    bookCommentId = r.BookCommentId,
                    userId = r.UserId,
                    comment = r.Comment,
                    userName = r.UserName,
                    roleName = r.RoleName,
                    // Ensure proper parsing of `CreatedAt` as DateTime before applying methods
                    createdAt = GetTimeAgo(r.CreatedAt),
                    timeAgo = GetTimeAgo(r.CreatedAt)
                }),
                hasMoreReplies = replies.Count > 3
            });
        }

        return new JsonResult(new
        {
            success = true,
            book = detail,
            comments = enhancedComments,
            totalComments = detail.TotalComments,
            currentPage = page,
            totalPages = (int)Math.Ceiling(detail.TotalComments / (double)pageSize),
            currentUserId = int.TryParse(HttpContext.Session.GetString("UserId"), out int sessionUserId) ? sessionUserId : (int?)null
        });
    }

    // Load more replies for a specific comment - Facebook style
    public async Task<JsonResult> OnGetLoadMoreRepliesAsync(int parentCommentId, int skip = 0, int take = 5)
    {
        var allReplies = await _bookCommentService.GetRepliesByCommentAsync(parentCommentId);
        var replies = allReplies.Skip(skip).Take(take).Select(r => new
        {
            bookCommentId = r.BookCommentId,
            comment = r.Comment,
            userName = r.UserName,
            roleName = r.RoleName,
            createdAt = GetTimeAgo(r.CreatedAt)
        }).ToList();

        var hasMore = allReplies.Count > skip + take;

        return new JsonResult(new 
        { 
            success = true, 
            replies = replies,
            hasMore = hasMore,
            totalReplies = allReplies.Count
        });
    }

    // Helper method to convert DateTime to "time ago" format like Facebook
    private string GetTimeAgo(DateTime dateTime)
    {
        var localDateTime = dateTime.ToLocalTime();
        var timeSpan = DateTime.Now - localDateTime;

        if (timeSpan.TotalMinutes < 1)
            return "Vừa xong";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes} phút trước";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours} giờ trước";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays} ngày trước";
        if (timeSpan.TotalDays < 30)
            return $"{(int)(timeSpan.TotalDays / 7)} tuần trước";
        if (timeSpan.TotalDays < 365)
            return $"{(int)(timeSpan.TotalDays / 30)} tháng trước";
        
        return $"{(int)(timeSpan.TotalDays / 365)} năm trước";
    }

    public async Task<JsonResult> OnGetGetRepliesAsync(int parentCommentId)
    {
        var replies = await _bookCommentService.GetRepliesByCommentAsync(parentCommentId);

        return new JsonResult(new { success = true, replies = replies });
    }
    public async Task<JsonResult> OnGetCheckFavoriteAsync(int bookId)
    {
        var userIdStr = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return new JsonResult(new { isFavorite = false });
        bool exists = await _favoriteService.IsFavoriteAsync(userId, bookId);
        return new JsonResult(new { isFavorite = exists });
    }

    public async Task<IActionResult> OnGetAsync(int id, int page = 1, int pageSize = 15)
    {
        Book = await _bookService.GetBookDetailWithStatsAndCommentsAsync(id, page, pageSize);
        if (Book == null)
            return NotFound();
        return Page();
    }

    public async Task<JsonResult> OnGetCheckLoginAsync()
    {
        await HttpContext.Session.LoadAsync();
        var userIdStr = HttpContext.Session.GetString("UserId");
        bool isLoggedIn = !string.IsNullOrEmpty(userIdStr);
        return new JsonResult(new { isLoggedIn });
    }

    public async Task<JsonResult> OnPostAddFavoriteAsync(int bookId)
    {
        await HttpContext.Session.LoadAsync();
        var userIdStr = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
        {
            return new JsonResult(new { success = false, message = "Bạn cần đăng nhập để thêm vào yêu thích!" });
        }
        
        var isFavorite = await _favoriteService.IsFavoriteAsync(userId, bookId);
        if (isFavorite)
        {
            return new JsonResult(new { success = false, message = "Sách đã có trong yêu thích." });
        }
        
        var favoriteCreateDto = new FavoriteCreateDto
        {
            UserId = userId,
            BookId = bookId
        };
        
        await _favoriteService.AddToFavoritesAsync(favoriteCreateDto);
        return new JsonResult(new { success = true, message = "Đã thêm vào danh sách yêu thích!" });
    }

    // Gửi bình luận/đánh giá (comment gốc)
    public async Task<JsonResult> OnPostAddCommentAsync(int BookId, int Star, string Comment)
    {
        await HttpContext.Session.LoadAsync();
        var userIdStr = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return new JsonResult(new { success = false, message = "Bạn cần đăng nhập để bình luận!" });

        var commentCreateDto = new BookCommentCreateDto
        {
            BookId = BookId,
            UserId = userId,
            Comment = Comment,
            Star = Star
        };

        try
        {
            // Lưu comment
            var saved = await _bookCommentService.CreateRootCommentAsync(commentCreateDto);

            // Lưu hoặc cập nhật rating
            var existingRating = await _ratingService.GetRatingByUserAndBookAsync(userId, BookId);
            if (existingRating != null)
            {
                var ratingUpdateDto = new RatingUpdateDto
                {
                    RatingId = existingRating.RatingId,
                    Star = Star
                };
                await _ratingService.UpdateRatingAsync(ratingUpdateDto);
            }
            else
            {
                var ratingCreateDto = new RatingCreateDto
                {
                    BookId = BookId,
                    UserId = userId,
                    Star = Star
                };
                await _ratingService.CreateRatingAsync(ratingCreateDto);
            }

            // Sau khi lưu, tính lại số sao trung bình và lượt đánh giá
            var avgStar = await _ratingService.GetAverageRatingAsync(BookId);
            var ratingCount = await _ratingService.GetRatingCountAsync(BookId);

            // Lấy thông tin user
            var user = await _userService.GetUserByIdAsync(userId);

            return new JsonResult(new
            {
                success = true,
                message = "Bình luận đã được gửi!",
                comment = new
                {
                    bookCommentId = saved.BookCommentId,
                    userName = user?.FullName ?? "Bạn",
                    star = Star,
                    comment = Comment,
                    roleName = user?.Role ?? "user",
                    // Adjusted to parse `CreatedAt` as DateTime before formatting
                    createdAt = saved.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("vi-VN")),
                    timeAgo = GetTimeAgo(saved.CreatedAt),
                },
                averageRating = avgStar,
                ratingCount = ratingCount
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    // Gửi reply cho comment - Facebook style với mentions
    public async Task<JsonResult> OnPostAddReplyAsync(int BookId, int ParentCommentId, string Comment, string? MentionedUser = null)
    {
        await HttpContext.Session.LoadAsync();
        var userIdStr = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return new JsonResult(new { success = false, message = "Bạn cần đăng nhập để trả lời!" });

        // Process mentions (Facebook style @username)
        if (!string.IsNullOrEmpty(MentionedUser))
        {
            Comment = $"@{MentionedUser} {Comment}";
        }

        var replyCreateDto = new BookCommentCreateDto
        {
            BookId = BookId,
            UserId = userId,
            ParentCommentId = ParentCommentId,
            Comment = Comment
        };

        try
        {
            var saved = await _bookCommentService.CreateReplyAsync(replyCreateDto);
            var user = await _userService.GetUserByIdAsync(userId);

            return new JsonResult(new
            {
                success = true,
                message = "Trả lời đã được gửi!",
                reply = new
                {
                    bookCommentId = saved.BookCommentId,
                    userName = user?.FullName ?? "Bạn",
                    comment = Comment,
                    roleName = user?.Role ?? "user",
                    createdAt = GetTimeAgo(saved.CreatedAt),
                    timeAgo = GetTimeAgo(saved.CreatedAt),
                    parentCommentId = ParentCommentId
                }
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    // Edit comment - Facebook style
    public async Task<JsonResult> OnPostEditCommentAsync(int commentId, string newComment)
    {
        await HttpContext.Session.LoadAsync();
        var userIdStr = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return new JsonResult(new { success = false, message = "Bạn cần đăng nhập!" });

        try
        {
            // Check if user can edit this comment
            var canEdit = await _bookCommentService.CanUserEditCommentAsync(commentId, userId);
            if (!canEdit)
                return new JsonResult(new { success = false, message = "Bạn không có quyền chỉnh sửa bình luận này!" });

            var updateDto = new BookCommentUpdateDto
            {
                BookCommentId = commentId,
                Comment = newComment
            };

            var updated = await _bookCommentService.UpdateCommentAsync(updateDto);
            
            return new JsonResult(new
            {
                success = true,
                message = "Đã cập nhật bình luận!",
                comment = new
                {
                    bookCommentId = updated.BookCommentId,
                    comment = updated.Comment,
                    // Adjusted to parse `UpdatedAt` as DateTime before formatting
                    updatedAt = updated.UpdatedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("vi-VN")) ?? DateTime.Now.ToLocalTime().ToString("dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("vi-VN")),
                    isEdited = true
                }
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    // Delete comment - Facebook style
    public async Task<JsonResult> OnPostDeleteCommentAsync(int commentId)
    {
        await HttpContext.Session.LoadAsync();
        var userIdStr = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return new JsonResult(new { success = false, message = "Bạn cần đăng nhập!" });

        try
        {
            // Check if user can delete this comment
            var canDelete = await _bookCommentService.CanUserDeleteCommentAsync(commentId, userId);
            if (!canDelete)
                return new JsonResult(new { success = false, message = "Bạn không có quyền xóa bình luận này!" });

            var deleted = await _bookCommentService.DeleteCommentAsync(commentId);
            if (!deleted)
                return new JsonResult(new { success = false, message = "Không thể xóa bình luận!" });

            return new JsonResult(new
            {
                success = true,
                message = "Đã xóa bình luận!",
                commentId = commentId
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }
}