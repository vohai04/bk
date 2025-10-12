using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services
{
    public class SearchHistoryService : ISearchHistoryService
    {
        private readonly BookContext _context;
        private readonly ILogger<SearchHistoryService> _logger;

        public SearchHistoryService(BookContext context, ILogger<SearchHistoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<SearchHistoryDto>> GetAllHistoriesAsync()
        {
            try
            {
                var searchHistories = await _context.SearchHistories
                    .Include(sh => sh.User)
                    .OrderByDescending(sh => sh.SearchedAt)
                    .ToListAsync();

                return searchHistories.Select(sh => sh.ToDto(sh.User?.UserName ?? "")).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all search histories");
                return new List<SearchHistoryDto>();
            }
        }

        public async Task<List<SearchHistoryDto>> GetHistoriesByUserAsync(int userId)
        {
            try
            {
                // Debug: tạm thời bỏ filter để xem tất cả data
                var searchHistories = await _context.SearchHistories
                    .Where(sh => sh.UserId == userId)
                    //.Where(sh => sh.UserId == userId && !string.IsNullOrEmpty(sh.SearchQuery))
                    .GroupBy(sh => (sh.SearchQuery ?? "").ToLower())
                    .Select(g => g.OrderByDescending(sh => sh.SearchedAt).First())
                    .Include(sh => sh.User)
                    .OrderByDescending(sh => sh.SearchedAt)
                    .ToListAsync();

                return searchHistories.Select(sh => sh.ToDto(sh.User?.UserName ?? "")).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting search history for user {userId}");
                return new List<SearchHistoryDto>();
            }
        }

        // Method mới cho pagination
        public async Task<(List<SearchHistoryDto> SearchHistories, int TotalCount)> GetSearchHistoriesByUserPagedAsync(int userId, int page, int pageSize)
        {
            try
            {
                // Debug: log userId being searched
                _logger.LogInformation($"Searching for search histories with UserId: {userId}");
                
                // Đơn giản hóa query - lấy tất cả records trước, rồi group sau
                var allRecords = await _context.SearchHistories
                    .Where(sh => sh.UserId == userId)
                    .Include(sh => sh.User)
                    .OrderByDescending(sh => sh.SearchedAt)
                    .ToListAsync();
                
                _logger.LogInformation($"Found {allRecords.Count} total records for UserId: {userId}");

                // Group by SearchQuery in memory để tránh duplicate
                var groupedRecords = allRecords
                    .GroupBy(sh => (sh.SearchQuery ?? "").ToLower())
                    .Select(g => g.First()) // Lấy record đầu tiên (mới nhất do đã order)
                    .OrderByDescending(sh => sh.SearchedAt)
                    .ToList();

                _logger.LogInformation($"After grouping: {groupedRecords.Count} unique search queries");

                var totalCount = groupedRecords.Count;
                var pagedRecords = groupedRecords
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var searchHistoryDtos = pagedRecords.Select(sh => sh.ToDto(sh.User?.UserName ?? "")).ToList();
                
                _logger.LogInformation($"Returning {searchHistoryDtos.Count} DTOs for page {page}");
                
                return (searchHistoryDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting search histories for user: {UserId}", userId);
                return (new List<SearchHistoryDto>(), 0);
            }
        }

        public async Task<SearchHistoryDto?> GetHistoryByIdAsync(int id)
        {
            try
            {
                var searchHistory = await _context.SearchHistories
                    .Include(sh => sh.User)
                    .FirstOrDefaultAsync(sh => sh.SearchHistoryId == id);

                if (searchHistory == null)
                {
                    _logger.LogWarning($"Search history with ID {id} not found");
                    return null;
                }

                return searchHistory.ToDto(searchHistory.User?.UserName ?? "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting search history with ID {id}");
                return null;
            }
        }

        public async Task<SearchHistoryDto> AddHistoryAsync(SearchHistoryCreateDto searchHistoryCreateDto)
        {
            try
            {
                // Check if user exists
                var userExists = await _context.Users.AnyAsync(u => u.UserId == searchHistoryCreateDto.UserId);
                if (!userExists)
                {
                    _logger.LogWarning($"User with ID {searchHistoryCreateDto.UserId} not found");
                    throw new ArgumentException("User not found");
                }

                var searchHistory = new SearchHistory
                {
                    UserId = searchHistoryCreateDto.UserId,
                    SearchQuery = searchHistoryCreateDto.SearchQuery,
                    Title = searchHistoryCreateDto.Title,
                    Author = searchHistoryCreateDto.Author,
                    CategoryId = searchHistoryCreateDto.CategoryId,
                    // Date = searchHistoryCreateDto.Date, // Removed because SearchHistoryCreateDto does not contain 'Date'
                    SearchedAt = DateTime.UtcNow,
                    ResultCount = searchHistoryCreateDto.ResultCount,
                    BookId = searchHistoryCreateDto.BookId
                };

                _context.SearchHistories.Add(searchHistory);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Search history added for user {searchHistoryCreateDto.UserId}");

                // Get the user name for the DTO
                var user = await _context.Users.FindAsync(searchHistoryCreateDto.UserId);
                return searchHistory.ToDto(user?.UserName ?? "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding search history");
                throw;
            }
        }

        public async Task<bool> DeleteHistoryAsync(int id)
        {
            try
            {
                var searchHistory = await _context.SearchHistories.FindAsync(id);
                if (searchHistory == null)
                {
                    _logger.LogWarning($"Search history with ID {id} not found");
                    return false;
                }

                _context.SearchHistories.Remove(searchHistory);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Search history with ID {id} deleted");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting search history with ID {id}");
                return false;
            }
        }

        public async Task<bool> DeleteAllHistoriesOfUserAsync(int userId)
        {
            try
            {
                var searchHistories = await _context.SearchHistories
                    .Where(sh => sh.UserId == userId)
                    .ToListAsync();

                if (!searchHistories.Any())
                {
                    _logger.LogInformation($"No search history found for user {userId}");
                    return true;
                }

                _context.SearchHistories.RemoveRange(searchHistories);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Deleted {searchHistories.Count} search histories for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting search history for user {userId}");
                return false;
            }
        }

        public async Task<IEnumerable<SearchHistoryDto>> GetRecentSearchHistoriesAsync(int userId, int count = 10)
        {
            try
            {
                var searchHistories = await _context.SearchHistories
                    .Where(sh => sh.UserId == userId)
                    .Include(sh => sh.User)
                    .OrderByDescending(sh => sh.SearchedAt)
                    .Take(count)
                    .ToListAsync();

                return searchHistories.Select(sh => sh.ToDto(sh.User?.UserName ?? ""));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting recent search histories for user {userId}");
                return new List<SearchHistoryDto>();
            }
        }

        public async Task<IEnumerable<string>> GetPopularSearchQueriesAsync(int count = 10)
        {
            try
            {
                var popularQueries = await _context.SearchHistories
                    .GroupBy(sh => sh.SearchQuery.ToLower())
                    .Select(g => new { Query = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(count)
                    .Select(x => x.Query)
                    .ToListAsync();

                return popularQueries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular search queries");
                return new List<string>();
            }
        }

        public async Task<int> GetSearchHistoryCountByUserAsync(int userId)
        {
            try
            {
                var count = await _context.SearchHistories
                    .Where(sh => sh.UserId == userId)
                    .CountAsync();

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting search history count for user {userId}");
                return 0;
            }
        }
    }
}