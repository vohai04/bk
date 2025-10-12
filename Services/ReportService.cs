using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace BookInfoFinder.Services
{
    public class ReportService : IReportService
    {
        private readonly BookContext _context;
        private readonly ILogger<ReportService> _logger;
        private readonly IConfiguration _configuration;

        public ReportService(BookContext context, ILogger<ReportService> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<DashboardReportDto> GetDashboardReportAsync()
        {
            try
            {
                var totalBooks = await _context.Books.CountAsync();
                var totalCategories = await _context.Categories.CountAsync();
                var totalTags = await _context.Tags.CountAsync();
                var totalAuthors = await _context.Authors.CountAsync();
                var totalPublishers = await _context.Publishers.CountAsync();
                var totalUsers = await _context.Users.CountAsync();
                var totalComments = await _context.BookComments.CountAsync();
                var totalFavorites = await _context.Favorites.CountAsync();
                var totalRatings = await _context.Ratings.CountAsync();
                
                var averageRating = totalRatings > 0 
                    ? Math.Round(await _context.Ratings.AverageAsync(r => r.Star), 2)
                    : 0;

                return new DashboardReportDto
                {
                    TotalBooks = totalBooks,
                    TotalCategories = totalCategories,
                    TotalTags = totalTags,
                    TotalAuthors = totalAuthors,
                    TotalPublishers = totalPublishers,
                    TotalUsers = totalUsers,
                    TotalComments = totalComments,
                    TotalFavorites = totalFavorites,
                    TotalRatings = totalRatings,
                    AverageRating = averageRating,
                    ReportGeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard report");
                throw;
            }
        }

        public async Task<DashboardReportDto> GetDashboardReportByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Books don't have CreatedAt, so use PublicationDate as filter
                var totalBooks = await _context.Books
                    .Where(b => b.PublicationDate >= startDate && b.PublicationDate <= endDate)
                    .CountAsync();
                
                var totalCategories = await _context.Categories
                    .Where(c => c.CreatedAt >= startDate && c.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalTags = await _context.Tags
                    .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalAuthors = await _context.Authors
                    .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalPublishers = await _context.Publishers
                    .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalUsers = await _context.Users
                    .Where(u => u.CreatedAt >= startDate && u.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalComments = await _context.BookComments
                    .Where(c => c.CreatedAt >= startDate && c.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalFavorites = await _context.Favorites
                    .Where(f => f.CreatedAt >= startDate && f.CreatedAt <= endDate)
                    .CountAsync();
                
                var totalRatings = await _context.Ratings
                    .Where(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate)
                    .CountAsync();
                
                var averageRating = totalRatings > 0 
                    ? Math.Round(await _context.Ratings
                        .Where(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate)
                        .AverageAsync(r => r.Star), 2)
                    : 0;

                return new DashboardReportDto
                {
                    TotalBooks = totalBooks,
                    TotalCategories = totalCategories,
                    TotalTags = totalTags,
                    TotalAuthors = totalAuthors,
                    TotalPublishers = totalPublishers,
                    TotalUsers = totalUsers,
                    TotalComments = totalComments,
                    TotalFavorites = totalFavorites,
                    TotalRatings = totalRatings,
                    AverageRating = averageRating,
                    ReportGeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard report by date range");
                throw;
            }
        }

        public async Task<List<BookListDto>> GetBooksReportAsync(string? title = null, string? author = null, string? category = null, int? year = null, string? tag = null)
        {
            try
            {
                var query = _context.Books
                    .Include(b => b.Author)
                    .Include(b => b.Category)
                    .Include(b => b.BookTags)
                        .ThenInclude(bt => bt.Tag)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(title))
                    query = query.Where(b => b.Title.Contains(title));

                if (!string.IsNullOrWhiteSpace(author))
                    query = query.Where(b => b.Author != null && b.Author.Name.Contains(author));

                if (!string.IsNullOrWhiteSpace(category))
                    query = query.Where(b => b.Category != null && b.Category.Name.Contains(category));

                if (year.HasValue)
                    query = query.Where(b => b.PublicationDate.Year == year.Value);

                if (!string.IsNullOrWhiteSpace(tag))
                    query = query.Where(b => b.BookTags.Any(bt => bt.Tag.Name.Contains(tag)));

                var books = await query.ToListAsync();
                var bookIds = books.Select(b => b.BookId).ToList();
                
                var ratings = await _context.Ratings
                    .Where(r => bookIds.Contains(r.BookId))
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
                _logger.LogError(ex, "Error getting books report");
                return new List<BookListDto>();
            }
        }

        public async Task<(List<BookListDto> Books, int TotalCount)> GetBooksReportPagedAsync(int page, int pageSize, string? title = null, string? author = null, string? category = null, int? year = null, string? tag = null)
        {
            try
            {
                var query = _context.Books
                    .Include(b => b.Author)
                    .Include(b => b.Category)
                    .Include(b => b.BookTags)
                        .ThenInclude(bt => bt.Tag)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(title))
                    query = query.Where(b => b.Title.Contains(title));

                if (!string.IsNullOrWhiteSpace(author))
                    query = query.Where(b => b.Author != null && b.Author.Name.Contains(author));

                if (!string.IsNullOrWhiteSpace(category))
                    query = query.Where(b => b.Category != null && b.Category.Name.Contains(category));

                if (year.HasValue)
                    query = query.Where(b => b.PublicationDate.Year == year.Value);

                if (!string.IsNullOrWhiteSpace(tag))
                    query = query.Where(b => b.BookTags.Any(bt => bt.Tag.Name.Contains(tag)));

                var totalCount = await query.CountAsync();
                
                var books = await query
                    .OrderBy(b => b.Title)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var bookIds = books.Select(b => b.BookId).ToList();
                
                var ratings = await _context.Ratings
                    .Where(r => bookIds.Contains(r.BookId))
                    .GroupBy(r => r.BookId)
                    .Select(g => new { BookId = g.Key, Avg = g.Average(r => r.Star), Count = g.Count() })
                    .ToListAsync();

                var ratingsMap = ratings.ToDictionary(x => x.BookId, x => new { x.Avg, x.Count });

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
                }).ToList();

                return (bookListDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting books report paged");
                return (new List<BookListDto>(), 0);
            }
        }

        public async Task<List<AuthorStatisticsDto>> GetAuthorStatisticsAsync()
        {
            try
            {
                var authors = await _context.Authors
                    .Include(a => a.Books)
                        .ThenInclude(b => b.Ratings)
                    .Include(a => a.Books)
                        .ThenInclude(b => b.Favorites)
                    .ToListAsync();

                return authors.Select(author =>
                {
                    var bookCount = author.Books.Count;
                    var allRatings = author.Books.SelectMany(b => b.Ratings).ToList();
                    var totalRatings = allRatings.Count;
                    var averageRating = totalRatings > 0 ? Math.Round(allRatings.Average(r => r.Star), 2) : 0;
                    var totalFavorites = author.Books.SelectMany(b => b.Favorites).Count();

                    return new AuthorStatisticsDto
                    {
                        AuthorId = author.AuthorId,
                        AuthorName = author.Name,
                        BookCount = bookCount,
                        AverageRating = averageRating,
                        TotalRatings = totalRatings,
                        TotalFavorites = totalFavorites
                    };
                }).OrderByDescending(a => a.BookCount).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting author statistics");
                return new List<AuthorStatisticsDto>();
            }
        }

        public async Task<List<CategoryStatisticsDto>> GetCategoryStatisticsAsync()
        {
            try
            {
                var categories = await _context.Categories
                    .Include(c => c.Books)
                        .ThenInclude(b => b.Ratings)
                    .Include(c => c.Books)
                        .ThenInclude(b => b.Favorites)
                    .ToListAsync();

                return categories.Select(category =>
                {
                    var bookCount = category.Books.Count;
                    var allRatings = category.Books.SelectMany(b => b.Ratings).ToList();
                    var totalRatings = allRatings.Count;
                    var averageRating = totalRatings > 0 ? Math.Round(allRatings.Average(r => r.Star), 2) : 0;
                    var totalFavorites = category.Books.SelectMany(b => b.Favorites).Count();

                    return new CategoryStatisticsDto
                    {
                        CategoryId = category.CategoryId,
                        CategoryName = category.Name,
                        BookCount = bookCount,
                        AverageRating = averageRating,
                        TotalRatings = totalRatings,
                        TotalFavorites = totalFavorites
                    };
                }).OrderByDescending(c => c.BookCount).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category statistics");
                return new List<CategoryStatisticsDto>();
            }
        }

        public async Task<List<TagStatisticsDto>> GetTagStatisticsAsync()
        {
            try
            {
                var tags = await _context.Tags
                    .Include(t => t.BookTags)
                        .ThenInclude(bt => bt.Book)
                            .ThenInclude(b => b.Ratings)
                    .ToListAsync();

                return tags.Select(tag =>
                {
                    var books = tag.BookTags.Select(bt => bt.Book).ToList();
                    var bookCount = books.Count;
                    var allRatings = books.SelectMany(b => b.Ratings).ToList();
                    var averageRating = allRatings.Any() ? Math.Round(allRatings.Average(r => r.Star), 2) : 0;
                    var usageCount = tag.BookTags.Count;

                    return new TagStatisticsDto
                    {
                        TagId = tag.TagId,
                        TagName = tag.Name,
                        BookCount = bookCount,
                        AverageRating = averageRating,
                        UsageCount = usageCount
                    };
                }).OrderByDescending(t => t.BookCount).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tag statistics");
                return new List<TagStatisticsDto>();
            }
        }

        public async Task<List<UserActivityDto>> GetUserActivityReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.Users.AsQueryable();

                var users = await query.ToListAsync();
                var userActivityDtos = new List<UserActivityDto>();

                foreach (var user in users)
                {
                    var commentsQuery = _context.BookComments.Where(c => c.UserId == user.UserId);
                    var ratingsQuery = _context.Ratings.Where(r => r.UserId == user.UserId);
                    var favoritesQuery = _context.Favorites.Where(f => f.UserId == user.UserId);

                    if (startDate.HasValue)
                    {
                        commentsQuery = commentsQuery.Where(c => c.CreatedAt >= startDate.Value);
                        ratingsQuery = ratingsQuery.Where(r => r.CreatedAt >= startDate.Value);
                        favoritesQuery = favoritesQuery.Where(f => f.CreatedAt >= startDate.Value);
                    }

                    if (endDate.HasValue)
                    {
                        commentsQuery = commentsQuery.Where(c => c.CreatedAt <= endDate.Value);
                        ratingsQuery = ratingsQuery.Where(r => r.CreatedAt <= endDate.Value);
                        favoritesQuery = favoritesQuery.Where(f => f.CreatedAt <= endDate.Value);
                    }

                    var commentsCount = await commentsQuery.CountAsync();
                    var ratingsCount = await ratingsQuery.CountAsync();
                    var favoritesCount = await favoritesQuery.CountAsync();

                    // Get last activity date
                    var lastComment = await commentsQuery.OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();
                    var lastRating = await ratingsQuery.OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
                    var lastFavorite = await favoritesQuery.OrderByDescending(f => f.CreatedAt).FirstOrDefaultAsync();

                    var lastActivityDate = new[] { lastComment?.CreatedAt, lastRating?.CreatedAt, lastFavorite?.CreatedAt }
                        .Where(d => d.HasValue)
                        .DefaultIfEmpty(user.CreatedAt)
                        .Max();

                    var isActive = (commentsCount + ratingsCount + favoritesCount) > 0;

                    userActivityDtos.Add(new UserActivityDto
                    {
                        UserId = user.UserId,
                        UserName = user.UserName,
                        CommentsCount = commentsCount,
                        RatingsCount = ratingsCount,
                        FavoritesCount = favoritesCount,
                        LastActivityDate = lastActivityDate ?? user.CreatedAt,
                        IsActive = isActive
                    });
                }

                return userActivityDtos.OrderByDescending(u => u.LastActivityDate).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user activity report");
                return new List<UserActivityDto>();
            }
        }

        // Export methods - simplified without external libraries
        public async Task<byte[]> ExportBookListToPdfAsync(string? title = null, string? author = null, string? category = null)
        {
            try
            {
                // This would normally use a PDF library like iTextSharp or similar
                // For now, return a simple CSV-like format as bytes
                var books = await GetBooksReportAsync(title, author, category);
                var csvContent = "Title,Author,Category,Publication Date,Average Rating,Rating Count\n";
                
                foreach (var book in books)
                {
                    csvContent += $"{book.Title},{book.AuthorName},{book.CategoryName},{book.PublicationDate:yyyy-MM-dd},{book.AverageRating},{book.RatingCount}\n";
                }

                return System.Text.Encoding.UTF8.GetBytes(csvContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting book list to PDF");
                throw;
            }
        }

        public async Task<byte[]> ExportAuthorStatisticsToPdfAsync()
        {
            try
            {
                var stats = await GetAuthorStatisticsAsync();
                var csvContent = "Author Name,Book Count,Average Rating,Total Ratings,Total Favorites\n";
                
                foreach (var stat in stats)
                {
                    csvContent += $"{stat.AuthorName},{stat.BookCount},{stat.AverageRating},{stat.TotalRatings},{stat.TotalFavorites}\n";
                }

                return System.Text.Encoding.UTF8.GetBytes(csvContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting author statistics to PDF");
                throw;
            }
        }

        public async Task<byte[]> ExportCategoryStatisticsToPdfAsync()
        {
            try
            {
                var stats = await GetCategoryStatisticsAsync();
                var csvContent = "Category Name,Book Count,Average Rating,Total Ratings,Total Favorites\n";
                
                foreach (var stat in stats)
                {
                    csvContent += $"{stat.CategoryName},{stat.BookCount},{stat.AverageRating},{stat.TotalRatings},{stat.TotalFavorites}\n";
                }

                return System.Text.Encoding.UTF8.GetBytes(csvContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting category statistics to PDF");
                throw;
            }
        }

        public async Task<byte[]> ExportUserActivityToPdfAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var activities = await GetUserActivityReportAsync(startDate, endDate);
                var csvContent = "User Name,Comments Count,Ratings Count,Favorites Count,Last Activity Date,Is Active\n";
                
                foreach (var activity in activities)
                {
                    csvContent += $"{activity.UserName},{activity.CommentsCount},{activity.RatingsCount},{activity.FavoritesCount},{activity.LastActivityDate:yyyy-MM-dd HH:mm:ss},{activity.IsActive}\n";
                }

                return System.Text.Encoding.UTF8.GetBytes(csvContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting user activity to PDF");
                throw;
            }
        }

        // Popular content methods
        public async Task<List<BookListDto>> GetMostPopularBooksAsync(int count = 10)
        {
            try
            {
                var popularBooks = await _context.Favorites
                    .GroupBy(f => f.BookId)
                    .Select(g => new { BookId = g.Key, FavoriteCount = g.Count() })
                    .OrderByDescending(x => x.FavoriteCount)
                    .Take(count)
                    .ToListAsync();

                var bookIds = popularBooks.Select(x => x.BookId).ToList();

                var books = await _context.Books
                    .Where(b => bookIds.Contains(b.BookId))
                    .Include(b => b.Author)
                    .Include(b => b.Category)
                    .Include(b => b.BookTags)
                        .ThenInclude(bt => bt.Tag)
                    .ToListAsync();

                var ratings = await _context.Ratings
                    .Where(r => bookIds.Contains(r.BookId))
                    .GroupBy(r => r.BookId)
                    .Select(g => new { BookId = g.Key, Avg = g.Average(r => r.Star), Count = g.Count() })
                    .ToListAsync();

                var ratingsMap = ratings.ToDictionary(x => x.BookId, x => new { x.Avg, x.Count });
                var favoriteMap = popularBooks.ToDictionary(x => x.BookId, x => x.FavoriteCount);

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
                _logger.LogError(ex, "Error getting most popular books");
                return new List<BookListDto>();
            }
        }

        public async Task<List<AuthorDto>> GetMostPopularAuthorsAsync(int count = 10)
        {
            try
            {
                var authorStats = await _context.Books
                    .Include(b => b.Author)
                    .Include(b => b.Favorites)
                    .GroupBy(b => b.AuthorId)
                    .Select(g => new { AuthorId = g.Key, FavoriteCount = g.SelectMany(b => b.Favorites).Count() })
                    .OrderByDescending(x => x.FavoriteCount)
                    .Take(count)
                    .ToListAsync();

                var authorIds = authorStats.Select(x => x.AuthorId).ToList();
                var authors = await _context.Authors
                    .Where(a => authorIds.Contains(a.AuthorId))
                    .ToListAsync();

                var authorDtos = new List<AuthorDto>();
                foreach (var author in authors)
                {
                    var bookCount = await _context.Books.CountAsync(b => b.AuthorId == author.AuthorId);
                    authorDtos.Add(author.ToDto(bookCount));
                }

                return authorDtos.OrderByDescending(a => a.BookCount).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting most popular authors");
                return new List<AuthorDto>();
            }
        }

        public async Task<List<CategoryDto>> GetMostPopularCategoriesAsync(int count = 10)
        {
            try
            {
                var categoryStats = await _context.Books
                    .Include(b => b.Category)
                    .Include(b => b.Favorites)
                    .GroupBy(b => b.CategoryId)
                    .Select(g => new { CategoryId = g.Key, FavoriteCount = g.SelectMany(b => b.Favorites).Count() })
                    .OrderByDescending(x => x.FavoriteCount)
                    .Take(count)
                    .ToListAsync();

                var categoryIds = categoryStats.Select(x => x.CategoryId).ToList();
                var categories = await _context.Categories
                    .Where(c => categoryIds.Contains(c.CategoryId))
                    .ToListAsync();

                var categoryDtos = new List<CategoryDto>();
                foreach (var category in categories)
                {
                    var bookCount = await _context.Books.CountAsync(b => b.CategoryId == category.CategoryId);
                    categoryDtos.Add(category.ToDto(bookCount));
                }

                return categoryDtos.OrderByDescending(c => c.BookCount).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting most popular categories");
                return new List<CategoryDto>();
            }
        }

        public async Task<List<TagDto>> GetMostPopularTagsAsync(int count = 10)
        {
            try
            {
                var tagStats = await _context.BookTags
                    .GroupBy(bt => bt.TagId)
                    .Select(g => new { TagId = g.Key, BookCount = g.Count() })
                    .OrderByDescending(x => x.BookCount)
                    .Take(count)
                    .ToListAsync();

                var tagIds = tagStats.Select(x => x.TagId).ToList();
                var tags = await _context.Tags
                    .Where(t => tagIds.Contains(t.TagId))
                    .ToListAsync();

                var tagDtos = tags.Select(tag =>
                {
                    var bookCount = tagStats.First(ts => ts.TagId == tag.TagId).BookCount;
                    return tag.ToDto(bookCount);
                }).OrderByDescending(t => t.BookCount).ToList();

                return tagDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting most popular tags");
                return new List<TagDto>();
            }
        }

        // RDLX Export methods
        public async Task<object> GetSystemStatisticsForReportAsync()
        {
            try
            {
                var dashboardData = await GetDashboardReportAsync();
                
                return new
                {
                    totalBooks = dashboardData.TotalBooks,
                    totalAuthors = dashboardData.TotalAuthors,
                    totalCategories = dashboardData.TotalCategories,
                    totalPublishers = dashboardData.TotalPublishers,
                    totalUsers = dashboardData.TotalUsers,
                    totalTags = dashboardData.TotalTags,
                    totalComments = dashboardData.TotalComments,
                    totalFavorites = dashboardData.TotalFavorites,
                    totalRatings = dashboardData.TotalRatings,
                    averageRating = dashboardData.AverageRating,
                    generatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system statistics for report");
                return new { error = true, message = ex.Message };
            }
        }

        public async Task<object> GetChartDataForReportAsync()
        {
            try
            {
                // Yearly books data
                var yearlyBooks = await _context.Books
                    .Where(b => b.PublicationDate != default(DateTime))
                    .GroupBy(b => b.PublicationDate.Year)
                    .Select(g => new { year = g.Key.ToString(), count = g.Count() })
                    .OrderBy(x => x.year)
                    .ToListAsync();

                // Top authors data
                var topAuthors = await _context.Authors
                    .Include(a => a.Books)
                    .Select(a => new { name = a.Name, count = a.Books.Count })
                    .OrderByDescending(x => x.count)
                    .Take(5)
                    .ToListAsync();

                // Publisher distribution
                var publisherData = await _context.Publishers
                    .Include(p => p.Books)
                    .Select(p => new { name = p.Name, count = p.Books.Count })
                    .OrderByDescending(x => x.count)
                    .Take(5)
                    .ToListAsync();

                // Category distribution
                var categoryData = await _context.Categories
                    .Include(c => c.Books)
                    .Select(c => new { name = c.Name, count = c.Books.Count })
                    .OrderByDescending(x => x.count)
                    .Take(6)
                    .ToListAsync();

                return new
                {
                    yearData = new { labels = yearlyBooks.Select(x => x.year), data = yearlyBooks.Select(x => x.count) },
                    authorData = new { labels = topAuthors.Select(x => x.name), data = topAuthors.Select(x => x.count) },
                    publisherData = new { labels = publisherData.Select(x => x.name), data = publisherData.Select(x => x.count) },
                    categoryData = new { labels = categoryData.Select(x => x.name), data = categoryData.Select(x => x.count) }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chart data for report");
                return new { error = true, message = ex.Message };
            }
        }

        public async Task<string> GenerateRDLXDataAsync()
        {
            try
            {
                var statistics = await GetSystemStatisticsForReportAsync();
                var chartData = await GetChartDataForReportAsync();

                var rdlxData = new
                {
                    reportMetadata = new
                    {
                        title = "Báo cáo thống kê hệ thống BookInfoFinder",
                        generatedDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        version = "1.0",
                        format = "RDLX-JSON"
                    },
                    dataSources = new
                    {
                        systemStats = new
                        {
                            connectionString = "Local",
                            data = new[] { statistics }
                        },
                        chartData = chartData
                    },
                    reportLayout = new
                    {
                        pageSize = "A4",
                        margins = new { top = 20, bottom = 20, left = 20, right = 20 },
                        sections = new object[]
                        {
                            new { type = "header", content = "Báo cáo thống kê hệ thống BookInfoFinder" },
                            new { type = "statistics", data = statistics },
                            new { type = "charts", charts = new[] { "yearlyBooks", "topAuthors", "publisherDistribution", "categoryDistribution" } },
                            new { type = "footer", content = $"Tạo ngày: {DateTime.Now:dd/MM/yyyy}" }
                        }
                    }
                };

                return JsonSerializer.Serialize(rdlxData, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating RDLX data");
                throw;
            }
        }

        public async Task<byte[]> ExportSystemReportToPdfAsync(string rdlxData)
        {
            try
            {
                // For now, return a comprehensive CSV report
                // In a real implementation, you would use ActiveReports to generate PDF from RDLX
                var statistics = await GetSystemStatisticsForReportAsync();
                var authorStats = await GetAuthorStatisticsAsync();
                var categoryStats = await GetCategoryStatisticsAsync();
                
                var reportContent = $@"BÁO CÁO THỐNG KÊ HỆ THỐNG BOOKINFOFINDER
Tạo ngày: {DateTime.Now:dd/MM/yyyy HH:mm:ss}

TỔNG QUAN HỆ THỐNG:
- Tổng số sách: {((dynamic)statistics).totalBooks}
- Tác giả: {((dynamic)statistics).totalAuthors}
- Thể loại: {((dynamic)statistics).totalCategories}
- Nhà xuất bản: {((dynamic)statistics).totalPublishers}
- Người dùng: {((dynamic)statistics).totalUsers}
- Tags: {((dynamic)statistics).totalTags}
- Bình luận: {((dynamic)statistics).totalComments}
- Yêu thích: {((dynamic)statistics).totalFavorites}
- Đánh giá: {((dynamic)statistics).totalRatings}
- Điểm trung bình: {((dynamic)statistics).averageRating:F2}/5

TOP TÁC GIẢ:
{string.Join("\n", authorStats.Take(5).Select(a => $"- {a.AuthorName}: {a.BookCount} sách"))}

TOP THỂ LOẠI:
{string.Join("\n", categoryStats.Take(5).Select(c => $"- {c.CategoryName}: {c.BookCount} sách"))}

RDLX Data Length: {rdlxData.Length} characters
";
                
                _logger.LogInformation("System report PDF export requested with RDLX data length: {Length}", rdlxData.Length);
                
                return System.Text.Encoding.UTF8.GetBytes(reportContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting system report to PDF");
                throw;
            }
        }
    }
}