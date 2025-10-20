using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface ISearchHistoryService
    {
        Task<List<SearchHistoryDto>> GetAllHistoriesAsync();
        Task<List<SearchHistoryDto>> GetHistoriesByUserAsync(int userId);
        Task<(List<SearchHistoryDto> SearchHistories, int TotalCount)> GetSearchHistoriesByUserPagedAsync(int userId, int page, int pageSize);
        Task<SearchHistoryDto?> GetHistoryByIdAsync(int id);
        Task<SearchHistoryDto> AddHistoryAsync(SearchHistoryCreateDto searchHistoryCreateDto);
        Task<bool> DeleteHistoryAsync(int id);
        Task<bool> DeleteAllHistoriesOfUserAsync(int userId);
        Task<IEnumerable<SearchHistoryDto>> GetRecentSearchHistoriesAsync(int userId, int count = 10);
        Task<IEnumerable<string>> GetPopularSearchQueriesAsync(int count = 10);
        Task<int> GetSearchHistoryCountByUserAsync(int userId);
        Task<(List<BookListDto> Books, int TotalCount)> GetMostSearchedBooksPagedAsync(int page, int pageSize);
    }
}