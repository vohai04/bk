using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services
{
    public class BookCommentService : IBookCommentService
    {
        private readonly BookContext _context;
        private readonly ILogger<BookCommentService> _logger;
        private readonly IDashboardService _dashboardService;

        public BookCommentService(BookContext context, ILogger<BookCommentService> logger, IDashboardService dashboardService)
        {
            _context = context;
            _logger = logger;
            _dashboardService = dashboardService;
        }

        public async Task<List<BookCommentDto>> GetRootCommentsByBookAsync(int bookId)
        {
            try
            {
                var comments = await _context.BookComments
                    .Where(c => c.BookId == bookId && c.ParentCommentId == null)
                    .Include(c => c.User)
                    .Include(c => c.Replies)
                        .ThenInclude(r => r.User)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                return comments.Select(c => new BookCommentDto
                {
                    BookCommentId = c.BookCommentId,
                    BookId = c.BookId,
                    UserId = c.UserId,
                    UserName = c.User?.UserName ?? "",
                    Role = (int)(c.User?.Role ?? Models.Role.User),
                    RoleName = (c.User?.Role ?? Models.Role.User).ToString(),
                    ParentCommentId = c.ParentCommentId,
                    Comment = c.Comment,
                    Star = c.Star,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    ReplyCount = c.Replies?.Count ?? 0,
                    TotalRepliesCount = c.Replies?.Count ?? 0,
                    Replies = c.Replies?.Select(r => r.ToDto()).ToList() ?? new List<BookCommentDto>()
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting root comments for book: {BookId}", bookId);
                return new List<BookCommentDto>();
            }
        }

        public async Task<(List<BookCommentDto> Comments, int TotalCount)> GetRootCommentsPagedAsync(int bookId, int page, int pageSize)
        {
            try
            {
                var totalCount = await _context.BookComments
                    .Where(c => c.BookId == bookId && c.ParentCommentId == null)
                    .CountAsync();

                var comments = await _context.BookComments
                    .Where(c => c.BookId == bookId && c.ParentCommentId == null)
                    .Include(c => c.User)
                    .Include(c => c.Replies)
                        .ThenInclude(r => r.User)
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var commentDtos = new List<BookCommentDto>();
                
                foreach (var comment in comments)
                {
                    var commentDto = comment.ToDto();
                    // Tính tổng số replies (Facebook style - đếm tất cả nested)
                    commentDto.TotalRepliesCount = await CountAllRepliesAsync(comment.BookCommentId);
                    commentDtos.Add(commentDto);
                }
                
                return (commentDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting root comments paged for book: {BookId}", bookId);
                return (new List<BookCommentDto>(), 0);
            }
        }

        public async Task<List<BookCommentDto>> GetRepliesByCommentAsync(int parentCommentId)
        {
            try
            {
                // Lấy tất cả replies trong thread này (Facebook style)
                var allReplies = await GetRepliesTreeAsync(parentCommentId);
                return allReplies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting replies for comment: {CommentId}", parentCommentId);
                return new List<BookCommentDto>();
            }
        }

        // Method mới để lấy toàn bộ replies tree (Facebook style)
        private async Task<List<BookCommentDto>> GetRepliesTreeAsync(int parentCommentId)
        {
            var result = new List<BookCommentDto>();

            // Lấy direct replies (không include Replies vì sẽ set manual)
            var directReplies = await _context.BookComments
                .Where(c => c.ParentCommentId == parentCommentId)
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt) // Sắp xếp mới nhất trước
                .ToListAsync();

            foreach (var reply in directReplies)
            {
                // Tạo DTO thủ công thay vì dùng ToDto() để tránh circular reference
                var replyDto = new BookCommentDto
                {
                    BookCommentId = reply.BookCommentId,
                    BookId = reply.BookId,
                    UserId = reply.UserId,
                    UserName = reply.User?.UserName ?? "",
                    Role = (int)(reply.User?.Role ?? Models.Role.User),
                    RoleName = (reply.User?.Role ?? Models.Role.User).ToString(),
                    ParentCommentId = reply.ParentCommentId,
                    Comment = reply.Comment,
                    Star = reply.Star,
                    CreatedAt = reply.CreatedAt,
                    UpdatedAt = reply.UpdatedAt,
                    ReplyCount = 0, // Will be calculated
                    TotalRepliesCount = 0, // Will be calculated
                    Replies = new List<BookCommentDto>() // Initialize empty
                };
                
                // Recursively lấy replies của reply này
                var nestedReplies = await GetRepliesTreeAsync(reply.BookCommentId);
                    _logger.LogInformation($"Reply {reply.BookCommentId} has {nestedReplies.Count} nested replies");
                replyDto.Replies = nestedReplies; // Set nested replies manually
                replyDto.ReplyCount = nestedReplies.Count;
                replyDto.TotalRepliesCount = nestedReplies.Count;
                
                result.Add(replyDto);
            }

            return result;
        }

        public async Task<(List<BookCommentDto> Replies, int TotalCount)> GetRepliesPagedAsync(int parentCommentId, int page, int pageSize)
        {
            try
            {
                var totalCount = await _context.BookComments
                    .Where(c => c.ParentCommentId == parentCommentId)
                    .CountAsync();

                var replies = await _context.BookComments
                    .Where(c => c.ParentCommentId == parentCommentId)
                    .Include(c => c.User)
                    .OrderByDescending(c => c.CreatedAt) // Sắp xếp mới nhất trước
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var replyDtos = replies.Select(r => r.ToDto()).ToList();
                return (replyDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting replies paged for comment: {CommentId}", parentCommentId);
                return (new List<BookCommentDto>(), 0);
            }
        }

        public async Task<BookCommentDto?> GetCommentByIdAsync(int commentId)
        {
            try
            {
                var comment = await _context.BookComments
                    .Include(c => c.User)
                    .Include(c => c.Replies)
                        .ThenInclude(r => r.User)
                    .FirstOrDefaultAsync(c => c.BookCommentId == commentId);

                return comment?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comment by id: {CommentId}", commentId);
                return null;
            }
        }

        public async Task<BookCommentDto> CreateRootCommentAsync(BookCommentCreateDto commentCreateDto)
        {
            try
            {
                if (commentCreateDto.ParentCommentId != null)
                    throw new ArgumentException("Root comment cannot have parent");
                
                if (commentCreateDto.Star == null || commentCreateDto.Star < 1 || commentCreateDto.Star > 5)
                    throw new ArgumentException("Root comment must have rating between 1 and 5");

                var comment = commentCreateDto.ToEntity();
                comment.CreatedAt = DateTime.UtcNow;

                _context.BookComments.Add(comment);
                await _context.SaveChangesAsync();

                // Reload with user info
                var savedComment = await _context.BookComments
                    .Include(c => c.User)
                    .FirstAsync(c => c.BookCommentId == comment.BookCommentId);

                // Log activity
                await _dashboardService.LogActivityAsync(
                    commentCreateDto.UserId.ToString(),
                    "Comment Created",
                    $"Posted a comment with {commentCreateDto.Star} stars",
                    "BookComment",
                    savedComment.BookCommentId,
                    ""
                );

                return savedComment.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating root comment");
                throw;
            }
        }

        public async Task<BookCommentDto> CreateReplyAsync(BookCommentCreateDto commentCreateDto)
        {
            try
            {
                if (commentCreateDto.ParentCommentId == null)
                    throw new ArgumentException("Reply must have parent comment");
                
                if (commentCreateDto.Star != null)
                    throw new ArgumentException("Reply cannot have rating");

                // Verify parent comment exists
                var parentExists = await _context.BookComments
                    .AnyAsync(c => c.BookCommentId == commentCreateDto.ParentCommentId);
                if (!parentExists)
                    throw new ArgumentException("Parent comment does not exist");

                var comment = commentCreateDto.ToEntity();
                comment.CreatedAt = DateTime.UtcNow;
                comment.Star = null; // Ensure no rating for replies

                _context.BookComments.Add(comment);
                await _context.SaveChangesAsync();

                // Reload with user info
                var savedComment = await _context.BookComments
                    .Include(c => c.User)
                    .FirstAsync(c => c.BookCommentId == comment.BookCommentId);

                // Log activity
                await _dashboardService.LogActivityAsync(
                    commentCreateDto.UserId.ToString(),
                    "Reply Created",
                    "Replied to a comment",
                    "BookComment",
                    savedComment.BookCommentId,
                    ""
                );

                    // Create notification for comment reply
                    // NOTE: pass the parent comment id (the comment being replied to), not the new reply id
                    if (commentCreateDto.ParentCommentId.HasValue)
                    {
                        await _dashboardService.CreateCommentReplyNotificationAsync(commentCreateDto.ParentCommentId.Value, commentCreateDto.UserId);
                    }

                return savedComment.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating reply");
                throw;
            }
        }

        public async Task<BookCommentDto> UpdateCommentAsync(BookCommentUpdateDto commentUpdateDto)
        {
            try
            {
                var comment = await _context.BookComments
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.BookCommentId == commentUpdateDto.BookCommentId);

                if (comment == null)
                    throw new ArgumentException("Comment not found");

                commentUpdateDto.UpdateEntity(comment);
                await _context.SaveChangesAsync();
                
                return comment.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating comment: {CommentId}", commentUpdateDto.BookCommentId);
                throw;
            }
        }

        public async Task<bool> DeleteCommentAsync(int commentId)
        {
            try
            {
                var comment = await _context.BookComments
                    .Include(c => c.Replies)
                    .FirstOrDefaultAsync(c => c.BookCommentId == commentId);

                if (comment == null) return false;

                // Cascade delete will handle replies
                _context.BookComments.Remove(comment);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment: {CommentId}", commentId);
                return false;
            }
        }

        public async Task<int> GetTotalCommentsCountByBookAsync(int bookId)
        {
            try
            {
                return await _context.BookComments
                    .Where(c => c.BookId == bookId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total comments count for book: {BookId}", bookId);
                return 0;
            }
        }

        public async Task<int> GetRootCommentsCountByBookAsync(int bookId)
        {
            try
            {
                return await _context.BookComments
                    .Where(c => c.BookId == bookId && c.ParentCommentId == null)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting root comments count for book: {BookId}", bookId);
                return 0;
            }
        }

        public async Task<int> GetRepliesCountByCommentAsync(int parentCommentId)
        {
            try
            {
                // Đếm tất cả replies trong thread (Facebook style)
                return await CountAllRepliesAsync(parentCommentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting replies count for comment: {CommentId}", parentCommentId);
                return 0;
            }
        }

        // Method mới để đếm tất cả nested replies
        private async Task<int> CountAllRepliesAsync(int parentCommentId)
        {
            var directReplies = await _context.BookComments
                .Where(c => c.ParentCommentId == parentCommentId)
                .Select(c => c.BookCommentId)
                .ToListAsync();

            int totalCount = directReplies.Count;

            // Recursively đếm replies của từng reply
            foreach (var replyId in directReplies)
            {
                totalCount += await CountAllRepliesAsync(replyId);
            }

            return totalCount;
        }

        public async Task<bool> CanUserDeleteCommentAsync(int commentId, int userId)
        {
            try
            {
                var comment = await _context.BookComments
                    .FirstOrDefaultAsync(c => c.BookCommentId == commentId);

                return comment != null && comment.UserId == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking delete permission for comment: {CommentId}, user: {UserId}", commentId, userId);
                return false;
            }
        }

        public async Task<bool> CanUserEditCommentAsync(int commentId, int userId)
        {
            try
            {
                var comment = await _context.BookComments
                    .FirstOrDefaultAsync(c => c.BookCommentId == commentId);

                return comment != null && comment.UserId == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking edit permission for comment: {CommentId}, user: {UserId}", commentId, userId);
                return false;
            }
        }

        public async Task<BookCommentDto?> GetCommentWithRepliesAsync(int commentId, int replyPage = 1, int replyPageSize = 5)
        {
            try
            {
                var comment = await _context.BookComments
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.BookCommentId == commentId);

                if (comment == null) return null;

                var replies = await _context.BookComments
                    .Where(r => r.ParentCommentId == commentId)
                    .Include(r => r.User)
                    .OrderByDescending(r => r.CreatedAt) // Sắp xếp mới nhất trước
                    .Skip((replyPage - 1) * replyPageSize)
                    .Take(replyPageSize)
                    .ToListAsync();

                var commentDto = comment.ToDto();
                commentDto.Replies = replies.Select(r => r.ToDto()).ToList();

                return commentDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comment with replies: {CommentId}", commentId);
                return null;
            }
        }

        public async Task<List<BookCommentDto>> GetCommentsTreeByBookAsync(int bookId, int maxDepth = 2)
        {
            try
            {
                // Get root comments
                var rootComments = await _context.BookComments
                    .Where(c => c.BookId == bookId && c.ParentCommentId == null)
                    .Include(c => c.User)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                var commentDtos = new List<BookCommentDto>();

                foreach (var rootComment in rootComments)
                {
                    var commentDto = rootComment.ToDto();
                    
                    if (maxDepth > 1)
                    {
                        // Get first level replies
                        var replies = await _context.BookComments
                            .Where(r => r.ParentCommentId == rootComment.BookCommentId)
                            .Include(r => r.User)
                            .OrderByDescending(r => r.CreatedAt) // Sắp xếp mới nhất trước
                            .ToListAsync();

                        commentDto.Replies = replies.Select(r => r.ToDto()).ToList();
                    }

                    commentDtos.Add(commentDto);
                }

                return commentDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comments tree for book: {BookId}", bookId);
                return new List<BookCommentDto>();
            }
        }
    }
}