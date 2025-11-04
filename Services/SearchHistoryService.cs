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
                var searchHistories = await _context.SearchHistories
                    .Where(sh => sh.UserId == userId && !string.IsNullOrEmpty(sh.SearchQuery))
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
                // Get all search histories for user
                var allRecords = await _context.SearchHistories
                    .Where(sh => sh.UserId == userId)
                    .Include(sh => sh.User)
                    .OrderByDescending(sh => sh.SearchedAt)
                    .ToListAsync();

                // Group by SearchQuery to avoid duplicates
                var groupedRecords = allRecords
                    .Where(sh => !string.IsNullOrEmpty(sh.SearchQuery))
                    .GroupBy(sh => (sh.SearchQuery ?? "").ToLower())
                    .Select(g => g.First()) // Take most recent
                    .OrderByDescending(sh => sh.SearchedAt)
                    .ToList();

                var totalCount = groupedRecords.Count;
                var pagedRecords = groupedRecords
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var searchHistoryDtos = pagedRecords.Select(sh => sh.ToDto(sh.User?.UserName ?? "")).ToList();
                
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
                    CategoryName = searchHistoryCreateDto.CategoryName,
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

        public async Task<(List<BookListDto> Books, int TotalCount)> GetMostSearchedBooksPagedAsync(int page, int pageSize)
        {
            try
            {
                // Get search counts for all books that have been searched
                var searchStats = await _context.SearchHistories
                    .Where(sh => sh.BookId.HasValue)
                    .GroupBy(sh => sh.BookId!.Value)
                    .Select(g => new { BookId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();

                var totalCount = searchStats.Count;
                var pagedStats = searchStats
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var bookIds = pagedStats.Select(x => x.BookId).ToList();

                var books = await _context.Books
                    .Where(b => bookIds.Contains(b.BookId))
                    .Include(b => b.Author)
                    .Include(b => b.Category)
                    .Include(b => b.BookTags)
                        .ThenInclude(bt => bt.Tag)
                    .ToListAsync();

                // Get ratings
                var ratings = await _context.Ratings
                    .Where(r => bookIds.Contains(r.BookId))
                    .GroupBy(r => r.BookId)
                    .Select(g => new { BookId = g.Key, Avg = g.Average(r => r.Star), Count = g.Count() })
                    .ToListAsync();

                var ratingsMap = ratings.ToDictionary(x => x.BookId, x => new { x.Avg, x.Count });
                var searchMap = pagedStats.ToDictionary(x => x.BookId, x => x.Count);

                var bookListDtos = books.Select(book =>
                {
                    var ratingInfo = ratingsMap.TryGetValue(book.BookId, out var info) ? info : null;
                    return new BookListDto
                    {
                        BookId = book.BookId,
                        Title = book.Title,
                        ImageBase64 = book.ImageBase64 ?? "",
                        PublicationDate = book.PublicationDate,
                        AuthorName = book.Author?.Name ?? "Không rõ",
                        CategoryName = book.Category?.Name ?? "Không rõ",
                        Tags = book.BookTags.Select(bt => bt.Tag.Name).ToList(),
                        AverageRating = Math.Round(ratingInfo?.Avg ?? 0, 2),
                        RatingCount = ratingInfo?.Count ?? 0
                    };
                }).OrderByDescending(b => searchMap.TryGetValue(b.BookId, out var count) ? count : 0).ToList();

                return (bookListDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting most searched books paged");
                return (new List<BookListDto>(), 0);
            }
        }
    }
}