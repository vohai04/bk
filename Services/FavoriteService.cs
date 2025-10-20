using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services
{
    public class FavoriteService : IFavoriteService
    {
        private readonly BookContext _context;
        private readonly ILogger<FavoriteService> _logger;

        public FavoriteService(BookContext context, ILogger<FavoriteService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FavoriteDto> AddToFavoritesAsync(FavoriteCreateDto favoriteCreateDto)
        {
            try
            {
                // Check if already exists
                var existing = await _context.Favorites
                    .AnyAsync(f => f.UserId == favoriteCreateDto.UserId && f.BookId == favoriteCreateDto.BookId);

                if (existing)
                    throw new ArgumentException("Book is already in favorites");

                // Verify book exists
                var book = await _context.Books
                    .Include(b => b.Author)
                    .FirstOrDefaultAsync(b => b.BookId == favoriteCreateDto.BookId);

                if (book == null)
                    throw new ArgumentException("Book not found");

                // Verify user exists
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserId == favoriteCreateDto.UserId);

                if (user == null)
                    throw new ArgumentException("User not found");

                var favorite = favoriteCreateDto.ToEntity();
                favorite.CreatedAt = DateTime.UtcNow;

                _context.Favorites.Add(favorite);
                await _context.SaveChangesAsync();

                return favorite.ToDto(book.Title, book.ImageBase64 ?? "", book.Author?.Name ?? "", user.UserName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding to favorites");
                throw;
            }
        }

        public async Task<bool> RemoveFromFavoritesAsync(int userId, int bookId)
        {
            try
            {
                var favorite = await _context.Favorites
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.BookId == bookId);

                if (favorite == null) return false;

                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing from favorites: {UserId}, {BookId}", userId, bookId);
                return false;
            }
        }

        public async Task<bool> IsFavoriteAsync(int userId, int bookId)
        {
            try
            {
                return await _context.Favorites
                    .AnyAsync(f => f.UserId == userId && f.BookId == bookId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if favorite: {UserId}, {BookId}", userId, bookId);
                return false;
            }
        }

        public async Task<List<FavoriteDto>> GetFavoritesByUserAsync(int userId)
        {
            try
            {
                var favorites = await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Include(f => f.Book)
                        .ThenInclude(b => b.Author)
                    .Include(f => f.Book)
                        .ThenInclude(b => b.Category)
                    .Include(f => f.Book)
                        .ThenInclude(b => b.BookTags)
                            .ThenInclude(bt => bt.Tag)
                    .Include(f => f.User)
                    .OrderByDescending(f => f.CreatedAt)
                    .ToListAsync();

                return favorites.Select(f => f.ToDto(
                    f.Book?.Title ?? "",
                    f.Book?.ImageBase64 ?? "",
                    f.Book?.Author?.Name ?? "",
                    f.User?.UserName ?? "",
                    f.Book?.Category?.Name ?? "Không rõ",
                    f.Book?.BookTags?.Select(bt => bt.Tag.Name).ToList() ?? new List<string>()
                )).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting favorites by user: {UserId}", userId);
                return new List<FavoriteDto>();
            }
        }

        public async Task<(List<FavoriteDto> Favorites, int TotalCount)> GetFavoritesByUserPagedAsync(int userId, int page, int pageSize)
        {
            try
            {
                var totalCount = await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .CountAsync();

                var favorites = await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Include(f => f.Book)
                        .ThenInclude(b => b.Author)
                    .Include(f => f.Book)
                        .ThenInclude(b => b.Category)
                    .Include(f => f.Book)
                        .ThenInclude(b => b.BookTags)
                            .ThenInclude(bt => bt.Tag)
                    .Include(f => f.User)
                    .OrderByDescending(f => f.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var favoriteDtos = favorites.Select(f => f.ToDto(
                    f.Book?.Title ?? "",
                    f.Book?.ImageBase64 ?? "",
                    f.Book?.Author?.Name ?? "",
                    f.User?.UserName ?? "",
                    f.Book?.Category?.Name ?? "Không rõ",
                    f.Book?.BookTags?.Select(bt => bt.Tag.Name).ToList() ?? new List<string>()
                )).ToList();

                return (favoriteDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting favorites by user paged: {UserId}", userId);
                return (new List<FavoriteDto>(), 0);
            }
        }

        public async Task<int> GetFavoritesCountByUserAsync(int userId)
        {
            try
            {
                return await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting favorites count by user: {UserId}", userId);
                return 0;
            }
        }

        public async Task<List<FavoriteDto>> GetFavoritesByBookAsync(int bookId)
        {
            try
            {
                var favorites = await _context.Favorites
                    .Where(f => f.BookId == bookId)
                    .Include(f => f.Book)
                        .ThenInclude(b => b.Author)
                    .Include(f => f.User)
                    .OrderByDescending(f => f.CreatedAt)
                    .ToListAsync();

                return favorites.Select(f => f.ToDto(
                    f.Book?.Title ?? "",
                    f.Book?.ImageBase64 ?? "",
                    f.Book?.Author?.Name ?? "",
                    f.User?.UserName ?? ""
                )).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting favorites by book: {BookId}", bookId);
                return new List<FavoriteDto>();
            }
        }

        public async Task<int> GetFavoriteCountByBookAsync(int bookId)
        {
            try
            {
                return await _context.Favorites
                    .Where(f => f.BookId == bookId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting favorite count by book: {BookId}", bookId);
                return 0;
            }
        }

        public async Task<(List<BookListDto> Books, int TotalCount)> GetMostFavoritedBooksPagedAsync(int page, int pageSize)
        {
            try
            {
                // Get favorite counts for all books
                var favoriteStats = await _context.Favorites
                    .GroupBy(f => f.BookId)
                    .Select(g => new { BookId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();

                var totalCount = favoriteStats.Count;
                var pagedStats = favoriteStats
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
                var favoriteMap = pagedStats.ToDictionary(x => x.BookId, x => x.Count);

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
                }).OrderByDescending(b => favoriteMap.TryGetValue(b.BookId, out var count) ? count : 0).ToList();

                return (bookListDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting most favorited books paged");
                return (new List<BookListDto>(), 0);
            }
        }

        public async Task<List<FavoriteDto>> GetRecentFavoritesAsync(int count = 10)
        {
            try
            {
                var favorites = await _context.Favorites
                    .Include(f => f.Book)
                        .ThenInclude(b => b.Author)
                    .Include(f => f.User)
                    .OrderByDescending(f => f.CreatedAt)
                    .Take(count)
                    .ToListAsync();

                return favorites.Select(f => f.ToDto(
                    f.Book?.Title ?? "",
                    f.Book?.ImageBase64 ?? "",
                    f.Book?.Author?.Name ?? "",
                    f.User?.UserName ?? ""
                )).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent favorites");
                return new List<FavoriteDto>();
            }
        }

        public async Task<List<FavoriteDto>> GetRecentFavoritesByUserAsync(int userId, int count = 10)
        {
            try
            {
                var favorites = await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Include(f => f.Book)
                        .ThenInclude(b => b.Author)
                    .Include(f => f.User)
                    .OrderByDescending(f => f.CreatedAt)
                    .Take(count)
                    .ToListAsync();

                return favorites.Select(f => f.ToDto(
                    f.Book?.Title ?? "",
                    f.Book?.ImageBase64 ?? "",
                    f.Book?.Author?.Name ?? "",
                    f.User?.UserName ?? ""
                )).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent favorites by user: {UserId}", userId);
                return new List<FavoriteDto>();
            }
        }

        public async Task<int> GetTotalFavoritesCountAsync()
        {
            try
            {
                return await _context.Favorites.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total favorites count");
                return 0;
            }
        }

        public async Task<List<BookListDto>> GetTopFavoritedBooksAsync(int count = 10)
        {
            try
            {
                var topBooks = await _context.Favorites
                    .GroupBy(f => f.BookId)
                    .Select(g => new { BookId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(count)
                    .ToListAsync();

                var bookIds = topBooks.Select(x => x.BookId).ToList();

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
                var favoriteMap = topBooks.ToDictionary(x => x.BookId, x => x.Count);

                return books.Select(book =>
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
                }).OrderByDescending(b => favoriteMap.TryGetValue(b.BookId, out var count) ? count : 0).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top favorited books");
                return new List<BookListDto>();
            }
        }
    }
}