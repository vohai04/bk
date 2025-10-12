using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services
{
    public class BookTagService : IBookTagService
    {
        private readonly BookContext _context;
        private readonly ILogger<BookTagService> _logger;

        public BookTagService(BookContext context, ILogger<BookTagService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<TagDto>> GetTagsByBookIdAsync(int bookId)
        {
            try
            {
                var tags = await _context.BookTags
                    .Where(bt => bt.BookId == bookId)
                    .Include(bt => bt.Tag)
                    .Select(bt => bt.Tag)
                    .ToListAsync();

                return tags.Select(t => t.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tags for book: {BookId}", bookId);
                return new List<TagDto>();
            }
        }

        public async Task<List<BookListDto>> GetBooksByTagIdAsync(int tagId)
        {
            try
            {
                var books = await _context.BookTags
                    .Where(bt => bt.TagId == tagId)
                    .Include(bt => bt.Book)
                        .ThenInclude(b => b.Author)
                    .Include(bt => bt.Book)
                        .ThenInclude(b => b.Category)
                    .Include(bt => bt.Book)
                        .ThenInclude(b => b.BookTags)
                            .ThenInclude(bt => bt.Tag)
                    .Select(bt => bt.Book)
                    .Distinct()
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
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting books by tag: {TagId}", tagId);
                return new List<BookListDto>();
            }
        }

        public async Task<List<BookListDto>> GetBooksByTagNameAsync(string tagName)
        {
            try
            {
                var tag = await _context.Tags
                    .FirstOrDefaultAsync(t => t.Name == tagName);

                if (tag == null) return new List<BookListDto>();

                return await GetBooksByTagIdAsync(tag.TagId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting books by tag name: {TagName}", tagName);
                return new List<BookListDto>();
            }
        }

        public async Task<(List<BookListDto> Books, int TotalCount)> GetBooksByTagPagedAsync(int tagId, int page, int pageSize)
        {
            try
            {
                var totalCount = await _context.BookTags
                    .Where(bt => bt.TagId == tagId)
                    .Select(bt => bt.Book)
                    .Distinct()
                    .CountAsync();

                var books = await _context.BookTags
                    .Where(bt => bt.TagId == tagId)
                    .Include(bt => bt.Book)
                        .ThenInclude(b => b.Author)
                    .Include(bt => bt.Book)
                        .ThenInclude(b => b.Category)
                    .Include(bt => bt.Book)
                        .ThenInclude(b => b.BookTags)
                            .ThenInclude(bt => bt.Tag)
                    .Select(bt => bt.Book)
                    .Distinct()
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
                _logger.LogError(ex, "Error getting books by tag paged: {TagId}", tagId);
                return (new List<BookListDto>(), 0);
            }
        }

        public async Task<bool> AddBookTagAsync(int bookId, int tagId)
        {
            try
            {
                // Check if relationship already exists
                var exists = await _context.BookTags
                    .AnyAsync(bt => bt.BookId == bookId && bt.TagId == tagId);

                if (exists) return true; // Already exists

                // Verify book and tag exist
                var bookExists = await _context.Books.AnyAsync(b => b.BookId == bookId);
                var tagExists = await _context.Tags.AnyAsync(t => t.TagId == tagId);

                if (!bookExists || !tagExists) return false;

                var bookTag = new BookTag
                {
                    BookId = bookId,
                    TagId = tagId
                };

                _context.BookTags.Add(bookTag);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding book tag: {BookId}, {TagId}", bookId, tagId);
                return false;
            }
        }

        public async Task<bool> RemoveBookTagAsync(int bookId, int tagId)
        {
            try
            {
                var bookTag = await _context.BookTags
                    .FirstOrDefaultAsync(bt => bt.BookId == bookId && bt.TagId == tagId);

                if (bookTag == null) return false;

                _context.BookTags.Remove(bookTag);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing book tag: {BookId}, {TagId}", bookId, tagId);
                return false;
            }
        }

        public async Task<bool> UpdateBookTagsAsync(int bookId, List<int> tagIds)
        {
            try
            {
                // Remove existing tags
                var existingBookTags = await _context.BookTags
                    .Where(bt => bt.BookId == bookId)
                    .ToListAsync();

                _context.BookTags.RemoveRange(existingBookTags);

                // Add new tags
                var newBookTags = tagIds.Select(tagId => new BookTag
                {
                    BookId = bookId,
                    TagId = tagId
                }).ToList();

                _context.BookTags.AddRange(newBookTags);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating book tags: {BookId}", bookId);
                return false;
            }
        }

        public async Task<bool> RemoveAllTagsFromBookAsync(int bookId)
        {
            try
            {
                var bookTags = await _context.BookTags
                    .Where(bt => bt.BookId == bookId)
                    .ToListAsync();

                _context.BookTags.RemoveRange(bookTags);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing all tags from book: {BookId}", bookId);
                return false;
            }
        }

        public async Task<int> GetBookCountByTagAsync(int tagId)
        {
            try
            {
                return await _context.BookTags
                    .Where(bt => bt.TagId == tagId)
                    .Select(bt => bt.Book)
                    .Distinct()
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting book count by tag: {TagId}", tagId);
                return 0;
            }
        }

        public async Task<bool> IsBookTaggedAsync(int bookId, int tagId)
        {
            try
            {
                return await _context.BookTags
                    .AnyAsync(bt => bt.BookId == bookId && bt.TagId == tagId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if book is tagged: {BookId}, {TagId}", bookId, tagId);
                return false;
            }
        }

        public async Task<List<TagDto>> GetPopularTagsAsync(int count = 10)
        {
            try
            {
                var popularTags = await _context.BookTags
                    .GroupBy(bt => bt.TagId)
                    .Select(g => new { TagId = g.Key, BookCount = g.Count() })
                    .OrderByDescending(x => x.BookCount)
                    .Take(count)
                    .ToListAsync();

                var tagIds = popularTags.Select(pt => pt.TagId).ToList();
                
                var tags = await _context.Tags
                    .Where(t => tagIds.Contains(t.TagId))
                    .ToListAsync();

                var tagDtos = tags.Select(tag =>
                {
                    var bookCount = popularTags.First(pt => pt.TagId == tag.TagId).BookCount;
                    return tag.ToDto(bookCount);
                }).OrderByDescending(t => t.BookCount).ToList();

                return tagDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular tags");
                return new List<TagDto>();
            }
        }
    }
}