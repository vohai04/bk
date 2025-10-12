using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IBookCommentService
    {
        // Root comments (bình luận gốc)
        Task<List<BookCommentDto>> GetRootCommentsByBookAsync(int bookId);
        Task<(List<BookCommentDto> Comments, int TotalCount)> GetRootCommentsPagedAsync(int bookId, int page, int pageSize);
        
        // Replies (trả lời bình luận)
        Task<List<BookCommentDto>> GetRepliesByCommentAsync(int parentCommentId);
        Task<(List<BookCommentDto> Replies, int TotalCount)> GetRepliesPagedAsync(int parentCommentId, int page, int pageSize);
        
        // CRUD operations
        Task<BookCommentDto?> GetCommentByIdAsync(int commentId);
        Task<BookCommentDto> CreateRootCommentAsync(BookCommentCreateDto commentCreateDto); // Với Star rating
        Task<BookCommentDto> CreateReplyAsync(BookCommentCreateDto commentCreateDto); // Không có Star
        Task<BookCommentDto> UpdateCommentAsync(BookCommentUpdateDto commentUpdateDto);
        Task<bool> DeleteCommentAsync(int commentId);
        
        // Statistics và utilities
        Task<int> GetTotalCommentsCountByBookAsync(int bookId); // Tất cả comments (root + replies)
        Task<int> GetRootCommentsCountByBookAsync(int bookId); // Chỉ comments gốc
        Task<int> GetRepliesCountByCommentAsync(int parentCommentId); // Replies của 1 comment
        
        // Permission checks
        Task<bool> CanUserDeleteCommentAsync(int commentId, int userId);
        Task<bool> CanUserEditCommentAsync(int commentId, int userId);
        
        // Nested structure (lấy comment với replies)
        Task<BookCommentDto?> GetCommentWithRepliesAsync(int commentId, int replyPage = 1, int replyPageSize = 5);
        Task<List<BookCommentDto>> GetCommentsTreeByBookAsync(int bookId, int maxDepth = 2);
    }
}