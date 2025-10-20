using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace BookInfoFinder.Services
{
    public class BookService : IBookService
    {
        private readonly BookContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BookService> _logger;

        public BookService(BookContext context, IWebHostEnvironment env, ILogger<BookService> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        public async Task<List<string>> SuggestBookTitlesAsync(string keyword)
        {
            try
            {
                keyword = keyword?.ToLower() ?? "";
                return await _context.Books
                    .Where(b => b.Title.ToLower().Contains(keyword))
                    .Select(b => b.Title)
                    .Distinct()
                    .Take(10)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suggesting book titles for keyword: {Keyword}", keyword);
                return new List<string>();
            }
        }

        public async Task<IEnumerable<BookListDto>> GetAllBooksAsync()
        {
            try
            {
                var books = await _context.Books
                    .Include(b => b.Author)
                    .Include(b => b.Category)
                    .Include(b => b.BookTags)
                        .ThenInclude(bt => bt.Tag)
                    .ToListAsync();

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
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all books");
                return new List<BookListDto>();
            }
        }

        public async Task<BookDetailDto?> GetBookByIdAsync(int bookId)
        {
            try
            {
                var book = await _context.Books
                    .Include(b => b.Author)
                    .Include(b => b.Category)
                    .Include(b => b.Publisher)
                    .Include(b => b.User)
                    .Include(b => b.BookTags)
                        .ThenInclude(bt => bt.Tag)
                    .FirstOrDefaultAsync(b => b.BookId == bookId);

                if (book == null) return null;

                var ratings = await _context.Ratings
                    .Where(r => r.BookId == bookId)
                    .ToListAsync();

                var comments = await _context.BookComments
                    .Where(c => c.BookId == bookId && c.ParentCommentId == null)
                    .Include(c => c.User)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                return new BookDetailDto
                {
                    BookId = book.BookId,
                    Title = book.Title,
                    ISBN = book.ISBN,
                    Description = book.Description ?? "",
                    Abstract = book.Abstract ?? "",
                    ImageBase64 = book.ImageBase64 ?? "",
                    PublicationDate = book.PublicationDate,
                    AuthorId = book.AuthorId,
                    AuthorName = book.Author?.Name ?? "Không rõ",
                    CategoryId = book.CategoryId,
                    CategoryName = book.Category?.Name ?? "Không rõ",
                    PublisherId = book.PublisherId,
                    PublisherName = book.Publisher?.Name ?? "Không rõ",
                    UserId = book.UserId,
                    UserName = book.User?.UserName ?? "Không rõ",
                    Tags = book.BookTags.Select(bt => bt.Tag.Name).ToList(),
                    AverageRating = ratings.Any() ? Math.Round(ratings.Average(r => r.Star), 2) : 0,
                    RatingCount = ratings.Count,
                    Comments = comments.Select(c => new BookCommentDto
                    {
                        BookCommentId = c.BookCommentId,
                        BookId = c.BookId,
                        UserId = c.UserId,
                        Comment = c.Comment,
                        Star = c.Star ?? 0,
                        UserName = c.User?.UserName ?? "Ẩn danh",
                        Role = (int)c.User?.Role!,
                        RoleName = c.User?.Role.ToString() ?? "user",
                        CreatedAt = c.CreatedAt.ToLocalTime(),
                        ReplyCount = 0 // Will be populated separately if needed
                    }).ToList(),
                    TotalComments = await _context.BookComments.CountAsync(c => c.BookId == bookId)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting book by id: {BookId}", bookId);
                return null;
            }
        }

        public async Task<BookDto> CreateBookAsync(BookCreateDto bookCreateDto)
        {
            try
            {
                var book = DtoMapper.ToEntity(bookCreateDto);
                
                _context.Books.Add(book);
                await _context.SaveChangesAsync();

                // Add tags if any
                if (bookCreateDto.TagIds.Any())
                {
                    var bookTags = bookCreateDto.TagIds.Select(tagId => new BookTag
                    {
                        BookId = book.BookId,
                        TagId = tagId
                    }).ToList();

                    _context.BookTags.AddRange(bookTags);
                    await _context.SaveChangesAsync();
                }

                return await GetBookDtoByIdAsync(book.BookId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating book");
                throw;
            }
        }

        public async Task<BookDto> UpdateBookAsync(BookUpdateDto bookUpdateDto)
        {
            try
            {
                var book = await _context.Books.FirstOrDefaultAsync(b => b.BookId == bookUpdateDto.BookId);
                if (book == null)
                    throw new ArgumentException("Book not found");

                DtoMapper.UpdateEntity(bookUpdateDto, book);

                // Update tags
                var existingTags = await _context.BookTags
                    .Where(bt => bt.BookId == book.BookId)
                    .ToListAsync();

                _context.BookTags.RemoveRange(existingTags);

                if (bookUpdateDto.TagIds.Any())
                {
                    var newTags = bookUpdateDto.TagIds.Select(tagId => new BookTag
                    {
                        BookId = book.BookId,
                        TagId = tagId
                    }).ToList();

                    _context.BookTags.AddRange(newTags);
                }

                await _context.SaveChangesAsync();

                return await GetBookDtoByIdAsync(book.BookId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating book: {BookId}", bookUpdateDto.BookId);
                throw;
            }
        }

        public async Task<bool> DeleteBookAsync(int bookId)
        {
            try
            {
                var book = await _context.Books.FirstOrDefaultAsync(b => b.BookId == bookId);
                if (book == null) return false;

                _context.Books.Remove(book);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting book: {BookId}", bookId);
                return false;
            }
        }

        public async Task<BookDetailDto?> GetBookDetailWithStatsAndCommentsAsync(int bookId, int page, int pageSize)
        {
            try
            {
                var book = await GetBookByIdAsync(bookId);
                if (book == null) return null;

                var comments = await _context.BookComments
                    .Where(c => c.BookId == bookId && c.ParentCommentId == null)
                    .Include(c => c.User)
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                book.Comments = comments.Select(c => new BookCommentDto
                {
                    BookCommentId = c.BookCommentId,
                    BookId = c.BookId,
                    UserId = c.UserId,
                    Comment = c.Comment,
                    Star = c.Star ?? 0,
                    UserName = c.User?.UserName ?? "Ẩn danh",
                    Role = (int)c.User?.Role!,
                    RoleName = c.User?.Role.ToString() ?? "user",
                    CreatedAt = c.CreatedAt.ToLocalTime(),
                    ReplyCount = 0
                }).ToList();

                return book;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting book detail with comments: {BookId}", bookId);
                return null;
            }
        }

        public async Task<(List<BookListDto> Books, int TotalCount)> SearchBooksWithStatsPagedAsync(string? title, string? author, string? category, DateTime? publicationDate, int page, int pageSize, string? tag)
        {
            try
            {
                var query = _context.Books
                    .Include(b => b.Author)
                    .Include(b => b.Category)
                    .Include(b => b.BookTags)
                        .ThenInclude(bt => bt.Tag)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(title))
                    query = query.Where(b => b.Title.ToLower().Contains(title.ToLower()));

                if (!string.IsNullOrEmpty(author))
                    query = query.Where(b => b.Author != null && b.Author.Name.ToLower().Contains(author.ToLower()));

                if (!string.IsNullOrEmpty(category))
                    query = query.Where(b => b.Category != null && b.Category.Name.ToLower().Contains(category.ToLower()));

                if (publicationDate.HasValue)
                    query = query.Where(b => b.PublicationDate.Year == publicationDate.Value.Year);

                if (!string.IsNullOrEmpty(tag))
                    query = query.Where(b => b.BookTags.Any(bt => bt.Tag.Name.ToLower().Contains(tag.ToLower())));

                var totalCount = await query.CountAsync();

                var books = await query
                    .OrderBy(b => b.BookId)
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

                var result = books.Select(book =>
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

                return (result, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching books");
                return (new List<BookListDto>(), 0);
            }
        }

        public async Task<(List<BookDto> Books, int TotalCount)> SearchBooksAdminPagedAsync(string? title, string? author, string? category, DateTime? publicationDate, int page, int pageSize, string? tag)
        {
            try
            {
                var query = _context.Books
                    .Include(b => b.Author)
                    .Include(b => b.Category)
                    .Include(b => b.Publisher)
                    .Include(b => b.User)
                    .Include(b => b.BookTags)
                        .ThenInclude(bt => bt.Tag)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(title))
                    query = query.Where(b => b.Title.Contains(title));
                
                if (!string.IsNullOrEmpty(author))
                    query = query.Where(b => b.Author != null && b.Author.Name.Contains(author));
                
                if (!string.IsNullOrEmpty(category))
                    query = query.Where(b => b.Category != null && b.Category.Name.Contains(category));
                
                if (publicationDate.HasValue)
                    query = query.Where(b => b.PublicationDate.Year == publicationDate.Value.Year);
                
                if (!string.IsNullOrEmpty(tag))
                    query = query.Where(b => b.BookTags.Any(bt => bt.Tag.Name.Contains(tag)));

                var totalCount = await query.CountAsync();

                var books = await query
                    .OrderBy(b => b.BookId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var bookIds = books.Select(b => b.BookId).ToList();

                var ratings = await _context.Ratings
                    .Where(r => bookIds.Contains(r.BookId))
                    .GroupBy(r => r.BookId)
                    .Select(g => new { BookId = g.Key, Avg = g.Average(r => r.Star), Count = g.Count() })
                    .ToListAsync();

                var comments = await _context.BookComments
                    .Where(c => bookIds.Contains(c.BookId))
                    .GroupBy(c => c.BookId)
                    .Select(g => new { BookId = g.Key, Count = g.Count() })
                    .ToListAsync();

                var favorites = await _context.Favorites
                    .Where(f => bookIds.Contains(f.BookId))
                    .GroupBy(f => f.BookId)
                    .Select(g => new { BookId = g.Key, Count = g.Count() })
                    .ToListAsync();

                var ratingsMap = ratings.ToDictionary(x => x.BookId, x => new { x.Avg, x.Count });
                var commentsMap = comments.ToDictionary(x => x.BookId, x => x.Count);
                var favoritesMap = favorites.ToDictionary(x => x.BookId, x => x.Count);

                var result = books.Select(book =>
                {
                    var ratingInfo = ratingsMap.TryGetValue(book.BookId, out var info) ? info : null;
                    return new BookDto
                    {
                        BookId = book.BookId,
                        Title = book.Title,
                        ISBN = book.ISBN,
                        Description = book.Description ?? "",
                        Abstract = book.Abstract ?? "",
                        ImageBase64 = book.ImageBase64 ?? "",
                        PublicationDate = book.PublicationDate,
                        AuthorId = book.AuthorId,
                        AuthorName = book.Author?.Name ?? "Không rõ",
                        CategoryId = book.CategoryId,
                        CategoryName = book.Category?.Name ?? "Không rõ",
                        PublisherId = book.PublisherId,
                        PublisherName = book.Publisher?.Name ?? "Không rõ",
                        UserId = book.UserId,
                        UserName = book.User?.UserName ?? "Không rõ",
                        Tags = book.BookTags.Select(bt => new TagDto { TagId = bt.Tag.TagId, Name = bt.Tag.Name }).ToList(),
                        AverageRating = Math.Round(ratingInfo?.Avg ?? 0, 2),
                        RatingCount = ratingInfo?.Count ?? 0,
                        TotalComments = commentsMap.TryGetValue(book.BookId, out var commentCount) ? commentCount : 0,
                        TotalFavorites = favoritesMap.TryGetValue(book.BookId, out var favoriteCount) ? favoriteCount : 0
                    };
                }).ToList();

                return (result, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting books for admin");
                return (new List<BookDto>(), 0);
            }
        }

        private async Task<BookDto> GetBookDtoByIdAsync(int bookId)
        {
            var book = await _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.User)
                .Include(b => b.BookTags)
                    .ThenInclude(bt => bt.Tag)
                .FirstOrDefaultAsync(b => b.BookId == bookId);

            if (book == null)
                throw new ArgumentException("Book not found");

            var ratings = await _context.Ratings.Where(r => r.BookId == bookId).ToListAsync();
            var commentCount = await _context.BookComments.CountAsync(c => c.BookId == bookId);
            var favoriteCount = await _context.Favorites.CountAsync(f => f.BookId == bookId);

            return new BookDto
            {
                BookId = book.BookId,
                Title = book.Title,
                ISBN = book.ISBN,
                Description = book.Description ?? "",
                Abstract = book.Abstract ?? "",
                ImageBase64 = book.ImageBase64 ?? "",
                PublicationDate = book.PublicationDate,
                AuthorId = book.AuthorId,
                AuthorName = book.Author?.Name ?? "Không rõ",
                CategoryId = book.CategoryId,
                CategoryName = book.Category?.Name ?? "Không rõ",
                PublisherId = book.PublisherId,
                PublisherName = book.Publisher?.Name ?? "Không rõ",
                UserId = book.UserId,
                UserName = book.User?.UserName ?? "Không rõ",
                Tags = book.BookTags.Select(bt => new TagDto { TagId = bt.Tag.TagId, Name = bt.Tag.Name }).ToList(),
                AverageRating = ratings.Any() ? Math.Round(ratings.Average(r => r.Star), 2) : 0,
                RatingCount = ratings.Count,
                TotalComments = commentCount,
                TotalFavorites = favoriteCount
            };
        }
    }
}
