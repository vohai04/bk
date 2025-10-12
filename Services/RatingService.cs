using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services
{
    public class RatingService : IRatingService
    {
        private readonly BookContext _context;
        private readonly ILogger<RatingService> _logger;

        public RatingService(BookContext context, ILogger<RatingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<RatingDto?> GetRatingByIdAsync(int ratingId)
        {
            try
            {
                var rating = await _context.Ratings
                    .Include(r => r.User)
                    .Include(r => r.Book)
                        .ThenInclude(b => b!.Author)
                    .FirstOrDefaultAsync(r => r.RatingId == ratingId);

                return rating?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rating by id: {RatingId}", ratingId);
                return null;
            }
        }

        public async Task<RatingDto?> GetRatingByUserAndBookAsync(int userId, int bookId)
        {
            try
            {
                var rating = await _context.Ratings
                    .Include(r => r.User)
                    .Include(r => r.Book)
                        .ThenInclude(b => b!.Author)
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.BookId == bookId);

                return rating?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rating by user and book: {UserId}, {BookId}", userId, bookId);
                return null;
            }
        }

        public async Task<RatingDto> CreateRatingAsync(RatingCreateDto ratingCreateDto)
        {
            try
            {
                // Validate star rating
                if (ratingCreateDto.Star < 1 || ratingCreateDto.Star > 5)
                    throw new ArgumentException("Star rating must be between 1 and 5");

                // Check if user already rated this book
                var existingRating = await _context.Ratings
                    .AnyAsync(r => r.UserId == ratingCreateDto.UserId && r.BookId == ratingCreateDto.BookId);

                if (existingRating)
                    throw new ArgumentException("User has already rated this book");

                // Verify book and user exist
                var bookExists = await _context.Books.AnyAsync(b => b.BookId == ratingCreateDto.BookId);
                var userExists = await _context.Users.AnyAsync(u => u.UserId == ratingCreateDto.UserId);

                if (!bookExists) throw new ArgumentException("Book not found");
                if (!userExists) throw new ArgumentException("User not found");

                var rating = ratingCreateDto.ToEntity();
                rating.CreatedAt = DateTime.UtcNow;

                _context.Ratings.Add(rating);
                await _context.SaveChangesAsync();

                // Reload with related data
                var savedRating = await _context.Ratings
                    .Include(r => r.User)
                    .Include(r => r.Book)
                        .ThenInclude(b => b!.Author)
                    .FirstAsync(r => r.RatingId == rating.RatingId);

                return savedRating.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating rating");
                throw;
            }
        }

        public async Task<RatingDto> UpdateRatingAsync(RatingUpdateDto ratingUpdateDto)
        {
            try
            {
                var rating = await _context.Ratings
                    .Include(r => r.User)
                    .Include(r => r.Book)
                        .ThenInclude(b => b!.Author)
                    .FirstOrDefaultAsync(r => r.RatingId == ratingUpdateDto.RatingId);

                if (rating == null)
                    throw new ArgumentException("Rating not found");

                if (ratingUpdateDto.Star < 1 || ratingUpdateDto.Star > 5)
                    throw new ArgumentException("Star rating must be between 1 and 5");

                ratingUpdateDto.UpdateEntity(rating);
                await _context.SaveChangesAsync();

                return rating.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rating: {RatingId}", ratingUpdateDto.RatingId);
                throw;
            }
        }

        public async Task<bool> DeleteRatingAsync(int ratingId)
        {
            try
            {
                var rating = await _context.Ratings
                    .FirstOrDefaultAsync(r => r.RatingId == ratingId);

                if (rating == null) return false;

                _context.Ratings.Remove(rating);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting rating: {RatingId}", ratingId);
                return false;
            }
        }

        public async Task<List<RatingDto>> GetRatingsByBookAsync(int bookId)
        {
            try
            {
                var ratings = await _context.Ratings
                    .Where(r => r.BookId == bookId)
                    .Include(r => r.User)
                    .Include(r => r.Book)
                        .ThenInclude(b => b!.Author)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                return ratings.Select(r => r.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ratings by book: {BookId}", bookId);
                return new List<RatingDto>();
            }
        }

        public async Task<List<RatingDto>> GetRatingsByUserAsync(int userId)
        {
            try
            {
                var ratings = await _context.Ratings
                    .Where(r => r.UserId == userId)
                    .Include(r => r.User)
                    .Include(r => r.Book)
                        .ThenInclude(b => b!.Author)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                return ratings.Select(r => r.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ratings by user: {UserId}", userId);
                return new List<RatingDto>();
            }
        }

        public async Task<(List<RatingDto> Ratings, int TotalCount)> GetRatingsPagedAsync(int bookId, int page, int pageSize)
        {
            try
            {
                var totalCount = await _context.Ratings
                    .Where(r => r.BookId == bookId)
                    .CountAsync();

                var ratings = await _context.Ratings
                    .Where(r => r.BookId == bookId)
                    .Include(r => r.User)
                    .Include(r => r.Book)
                        .ThenInclude(b => b!.Author)
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var ratingDtos = ratings.Select(r => r.ToDto()).ToList();
                return (ratingDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ratings paged for book: {BookId}", bookId);
                return (new List<RatingDto>(), 0);
            }
        }

        public async Task<double> GetAverageRatingAsync(int bookId)
        {
            try
            {
                var ratings = await _context.Ratings
                    .Where(r => r.BookId == bookId)
                    .Select(r => r.Star)
                    .ToListAsync();

                if (!ratings.Any()) return 0;
                return Math.Round(ratings.Average(), 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting average rating for book: {BookId}", bookId);
                return 0;
            }
        }

        public async Task<int> GetRatingCountAsync(int bookId)
        {
            try
            {
                return await _context.Ratings
                    .Where(r => r.BookId == bookId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rating count for book: {BookId}", bookId);
                return 0;
            }
        }

        public async Task<Dictionary<int, int>> GetStarStatisticsAsync(int bookId)
        {
            try
            {
                var stats = await _context.Ratings
                    .Where(r => r.BookId == bookId)
                    .GroupBy(r => r.Star)
                    .Select(g => new { Star = g.Key, Count = g.Count() })
                    .ToListAsync();

                var result = new Dictionary<int, int>();
                for (int i = 1; i <= 5; i++)
                {
                    result[i] = 0;
                }

                foreach (var stat in stats)
                {
                    result[stat.Star] = stat.Count;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting star statistics for book: {BookId}", bookId);
                return new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 } };
            }
        }

        public async Task<bool> HasUserRatedBookAsync(int userId, int bookId)
        {
            try
            {
                return await _context.Ratings
                    .AnyAsync(r => r.UserId == userId && r.BookId == bookId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user rated book: {UserId}, {BookId}", userId, bookId);
                return false;
            }
        }

        public async Task<(List<BookListDto> Books, int TotalCount)> GetTopRatedBooksPagedAsync(int page, int pageSize, double minRating = 0)
        {
            try
            {
                var ratingStats = await _context.Ratings
                    .GroupBy(r => r.BookId)
                    .Select(g => new { BookId = g.Key, Avg = g.Average(r => r.Star), Count = g.Count() })
                    .Where(x => x.Avg >= minRating)
                    .OrderByDescending(x => x.Avg)
                    .ThenByDescending(x => x.Count)
                    .ToListAsync();

                var totalCount = ratingStats.Count;
                var pagedStats = ratingStats
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

                var ratingsMap = pagedStats.ToDictionary(x => x.BookId, x => new { Avg = Math.Round(x.Avg, 2), x.Count });

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
                        AverageRating = ratingInfo?.Avg ?? 0,
                        RatingCount = ratingInfo?.Count ?? 0
                    };
                }).OrderByDescending(b => b.AverageRating).ThenByDescending(b => b.RatingCount).ToList();

                return (bookListDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top rated books paged");
                return (new List<BookListDto>(), 0);
            }
        }

        public async Task<List<BookListDto>> GetRecentlyRatedBooksAsync(int count = 10)
        {
            try
            {
                var recentRatings = await _context.Ratings
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(count)
                    .Select(r => r.BookId)
                    .Distinct()
                    .ToListAsync();

                var books = await _context.Books
                    .Where(b => recentRatings.Contains(b.BookId))
                    .Include(b => b.Author)
                    .Include(b => b.Category)
                    .Include(b => b.BookTags)
                        .ThenInclude(bt => bt.Tag)
                    .ToListAsync();

                var ratings = await _context.Ratings
                    .Where(r => recentRatings.Contains(r.BookId))
                    .GroupBy(r => r.BookId)
                    .Select(g => new { BookId = g.Key, Avg = g.Average(r => r.Star), Count = g.Count() })
                    .ToListAsync();

                var ratingsMap = ratings.ToDictionary(x => x.BookId, x => new { x.Avg, x.Count });

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
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recently rated books");
                return new List<BookListDto>();
            }
        }

        public async Task<(List<RatingDto> Ratings, int TotalCount)> GetUserRatingsPagedAsync(int userId, int page, int pageSize)
        {
            try
            {
                var totalCount = await _context.Ratings
                    .Where(r => r.UserId == userId)
                    .CountAsync();

                var ratings = await _context.Ratings
                    .Where(r => r.UserId == userId)
                    .Include(r => r.User)
                    .Include(r => r.Book)
                        .ThenInclude(b => b!.Author)
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var ratingDtos = ratings.Select(r => r.ToDto()).ToList();
                return (ratingDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user ratings paged: {UserId}", userId);
                return (new List<RatingDto>(), 0);
            }
        }

        public async Task<int> GetUserRatingCountAsync(int userId)
        {
            try
            {
                return await _context.Ratings
                    .Where(r => r.UserId == userId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user rating count: {UserId}", userId);
                return 0;
            }
        }
    }
}