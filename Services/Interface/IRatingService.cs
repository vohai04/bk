using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IRatingService
    {
        // Rating CRUD operations
        Task<RatingDto?> GetRatingByIdAsync(int ratingId);
        Task<RatingDto?> GetRatingByUserAndBookAsync(int userId, int bookId);
        Task<RatingDto> CreateRatingAsync(RatingCreateDto ratingCreateDto);
        Task<RatingDto> UpdateRatingAsync(RatingUpdateDto ratingUpdateDto);
        Task<bool> DeleteRatingAsync(int ratingId);
        
        // Rating queries
        Task<List<RatingDto>> GetRatingsByBookAsync(int bookId);
        Task<List<RatingDto>> GetRatingsByUserAsync(int userId);
        Task<(List<RatingDto> Ratings, int TotalCount)> GetRatingsPagedAsync(int bookId, int page, int pageSize);
        
        // Rating statistics
        Task<double> GetAverageRatingAsync(int bookId);
        Task<int> GetRatingCountAsync(int bookId);
        Task<Dictionary<int, int>> GetStarStatisticsAsync(int bookId); // Star -> Count
        Task<bool> HasUserRatedBookAsync(int userId, int bookId);
        
        // Top rated books
        Task<(List<BookListDto> Books, int TotalCount)> GetTopRatedBooksPagedAsync(int page, int pageSize, double minRating = 0);
        Task<List<BookListDto>> GetRecentlyRatedBooksAsync(int count = 10);
        
        // User rating history
        Task<(List<RatingDto> Ratings, int TotalCount)> GetUserRatingsPagedAsync(int userId, int page, int pageSize);
        Task<int> GetUserRatingCountAsync(int userId);
    }
}