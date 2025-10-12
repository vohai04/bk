using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IFavoriteService
    {
        // Favorite CRUD operations
        Task<FavoriteDto> AddToFavoritesAsync(FavoriteCreateDto favoriteCreateDto);
        Task<bool> RemoveFromFavoritesAsync(int userId, int bookId);
        Task<bool> IsFavoriteAsync(int userId, int bookId);
        
        // User favorites
        Task<List<FavoriteDto>> GetFavoritesByUserAsync(int userId);
        Task<(List<FavoriteDto> Favorites, int TotalCount)> GetFavoritesByUserPagedAsync(int userId, int page, int pageSize);
        Task<int> GetFavoritesCountByUserAsync(int userId);
        
        // Book favorites
        Task<List<FavoriteDto>> GetFavoritesByBookAsync(int bookId);
        Task<int> GetFavoriteCountByBookAsync(int bookId);
        Task<(List<BookListDto> Books, int TotalCount)> GetMostFavoritedBooksPagedAsync(int page, int pageSize);
        
        // Recent activities
        Task<List<FavoriteDto>> GetRecentFavoritesAsync(int count = 10);
        Task<List<FavoriteDto>> GetRecentFavoritesByUserAsync(int userId, int count = 10);
        
        // Statistics
        Task<int> GetTotalFavoritesCountAsync();
        Task<List<BookListDto>> GetTopFavoritedBooksAsync(int count = 10);
    }
}