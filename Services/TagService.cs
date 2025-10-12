using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services
{
    public class TagService : ITagService
    {
        private readonly BookContext _context;
        private readonly ILogger<TagService> _logger;

        public TagService(BookContext context, ILogger<TagService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<TagDto> CreateTagAsync(TagCreateDto tagCreateDto)
        {
            try
            {
                // Check if tag already exists
                var existingTag = await _context.Tags
                    .FirstOrDefaultAsync(t => t.Name.ToLower() == tagCreateDto.Name.ToLower());

                if (existingTag != null)
                {
                    _logger.LogWarning($"Tag with name '{tagCreateDto.Name}' already exists");
                    throw new ArgumentException("Tag name already exists");
                }

                var tag = new Tag
                {
                    Name = tagCreateDto.Name,
                    Description = tagCreateDto.Description,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Tags.Add(tag);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Tag '{tag.Name}' created with ID {tag.TagId}");
                return tag.ToDto(0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating tag: {tagCreateDto.Name}");
                throw;
            }
        }

        public async Task<TagDto> UpdateTagAsync(TagUpdateDto tagUpdateDto)
        {
            try
            {
                var tag = await _context.Tags.FindAsync(tagUpdateDto.TagId);
                if (tag == null)
                {
                    _logger.LogWarning($"Tag with ID {tagUpdateDto.TagId} not found");
                    throw new ArgumentException("Tag not found");
                }

                // Check if another tag with the same name exists
                var duplicateTag = await _context.Tags
                    .FirstOrDefaultAsync(t => t.TagId != tagUpdateDto.TagId && t.Name.ToLower() == tagUpdateDto.Name.ToLower());

                if (duplicateTag != null)
                {
                    _logger.LogWarning($"Another tag with name '{tagUpdateDto.Name}' already exists");
                    throw new ArgumentException("Tag name already exists");
                }

                tag.Name = tagUpdateDto.Name;
                tag.Description = tagUpdateDto.Description;

                await _context.SaveChangesAsync();

                var bookCount = await _context.BookTags.CountAsync(bt => bt.TagId == tagUpdateDto.TagId);
                _logger.LogInformation($"Tag with ID {tagUpdateDto.TagId} updated");
                return tag.ToDto(bookCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating tag with ID {tagUpdateDto.TagId}");
                throw;
            }
        }

        public async Task<bool> DeleteTagAsync(int tagId)
        {
            try
            {
                var tag = await _context.Tags
                    .Include(t => t.BookTags)
                    .FirstOrDefaultAsync(t => t.TagId == tagId);

                if (tag == null)
                {
                    _logger.LogWarning($"Tag with ID {tagId} not found");
                    return false;
                }

                // Remove all book-tag relationships first
                if (tag.BookTags.Any())
                {
                    _context.BookTags.RemoveRange(tag.BookTags);
                }

                _context.Tags.Remove(tag);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Tag with ID {tagId} deleted");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting tag with ID {tagId}");
                return false;
            }
        }

        public async Task<TagDto?> GetTagByIdAsync(int tagId)
        {
            try
            {
                var tag = await _context.Tags.FindAsync(tagId);
                if (tag == null)
                {
                    return null;
                }

                var bookCount = await _context.BookTags.CountAsync(bt => bt.TagId == tagId);
                return tag.ToDto(bookCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting tag with ID {tagId}");
                return null;
            }
        }

        public async Task<List<TagDto>> GetAllTagsAsync()
        {
            try
            {
                var tags = await _context.Tags.OrderBy(t => t.Name).ToListAsync();
                var tagDtos = new List<TagDto>();

                foreach (var tag in tags)
                {
                    var bookCount = await _context.BookTags.CountAsync(bt => bt.TagId == tag.TagId);
                    tagDtos.Add(tag.ToDto(bookCount));
                }

                return tagDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all tags");
                return new List<TagDto>();
            }
        }

        public async Task<(List<TagDto> Tags, int TotalCount)> GetTagsPagedAsync(int page, int pageSize, string? search = null)
        {
            try
            {
                var query = _context.Tags.AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(t => t.Name.Contains(search) || 
                                           (t.Description != null && t.Description.Contains(search)));
                }

                var totalCount = await query.CountAsync();
                
                var tags = await query
                    .OrderBy(t => t.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var tagDtos = new List<TagDto>();
                foreach (var tag in tags)
                {
                    var bookCount = await _context.BookTags.CountAsync(bt => bt.TagId == tag.TagId);
                    tagDtos.Add(tag.ToDto(bookCount));
                }

                return (tagDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tags paged with search");
                return (new List<TagDto>(), 0);
            }
        }

        public async Task<List<string>> SuggestTagsAsync(string keyword)
        {
            try
            {
                keyword = keyword?.ToLower() ?? "";
                return await _context.Tags
                    .Where(t => !string.IsNullOrEmpty(t.Name) && t.Name.ToLower().Contains(keyword))
                    .Select(t => t.Name)
                    .Distinct()
                    .Take(10)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error suggesting tags for keyword: {keyword}");
                return new List<string>();
            }
        }

        public async Task<List<TagDto>> SearchTagsAsync(string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return await GetAllTagsAsync();
                }

                var tags = await _context.Tags
                    .Where(t => t.Name.Contains(searchTerm) || 
                               (t.Description != null && t.Description.Contains(searchTerm)))
                    .OrderBy(t => t.Name)
                    .ToListAsync();

                var tagDtos = new List<TagDto>();
                foreach (var tag in tags)
                {
                    var bookCount = await _context.BookTags.CountAsync(bt => bt.TagId == tag.TagId);
                    tagDtos.Add(tag.ToDto(bookCount));
                }

                return tagDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching tags with term: {searchTerm}");
                return new List<TagDto>();
            }
        }

        public async Task<(List<TagDto> Tags, int TotalCount)> SearchTagsPagedAsync(string searchTerm, int page, int pageSize)
        {
            try
            {
                var query = _context.Tags.AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(t => t.Name.Contains(searchTerm) || 
                                           (t.Description != null && t.Description.Contains(searchTerm)));
                }

                var totalCount = await query.CountAsync();
                
                var tags = await query
                    .OrderBy(t => t.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var tagDtos = new List<TagDto>();
                foreach (var tag in tags)
                {
                    var bookCount = await _context.BookTags.CountAsync(bt => bt.TagId == tag.TagId);
                    tagDtos.Add(tag.ToDto(bookCount));
                }

                return (tagDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching tags paged with term: {searchTerm}");
                return (new List<TagDto>(), 0);
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

        public async Task<List<TagDto>> GetTagsByBookIdAsync(int bookId)
        {
            try
            {
                var bookTags = await _context.BookTags
                    .Where(bt => bt.BookId == bookId)
                    .Include(bt => bt.Tag)
                    .ToListAsync();

                var tagDtos = new List<TagDto>();
                foreach (var bookTag in bookTags)
                {
                    if (bookTag.Tag != null)
                    {
                        var bookCount = await _context.BookTags.CountAsync(bt => bt.TagId == bookTag.TagId);
                        tagDtos.Add(bookTag.Tag.ToDto(bookCount));
                    }
                }

                return tagDtos.OrderBy(t => t.Name).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting tags for book {bookId}");
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
                            .ThenInclude(bt2 => bt2.Tag)
                    .Select(bt => bt.Book)
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
                _logger.LogError(ex, $"Error getting books for tag {tagId}");
                return new List<BookListDto>();
            }
        }

        public async Task<(List<BookListDto> Books, int TotalCount)> GetBooksByTagIdPagedAsync(int tagId, int page, int pageSize)
        {
            try
            {
                var query = _context.BookTags
                    .Where(bt => bt.TagId == tagId)
                    .Include(bt => bt.Book)
                        .ThenInclude(b => b.Author)
                    .Include(bt => bt.Book)
                        .ThenInclude(b => b.Category)
                    .Include(bt => bt.Book)
                        .ThenInclude(b => b.BookTags)
                            .ThenInclude(bt2 => bt2.Tag)
                    .Select(bt => bt.Book);

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
                _logger.LogError(ex, $"Error getting books paged for tag {tagId}");
                return (new List<BookListDto>(), 0);
            }
        }

        public async Task<bool> TagExistsAsync(int tagId)
        {
            try
            {
                return await _context.Tags.AnyAsync(t => t.TagId == tagId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if tag {tagId} exists");
                return false;
            }
        }

        public async Task<bool> IsTagNameExistsAsync(string tagName)
        {
            try
            {
                return await _context.Tags.AnyAsync(t => t.Name.ToLower() == tagName.ToLower());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if tag name '{tagName}' exists");
                return false;
            }
        }

        public async Task<List<BookListDto>> GetBooksByTagAsync(int tagId)
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
                            .ThenInclude(bt2 => bt2.Tag)
                    .Select(bt => bt.Book)
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
                _logger.LogError(ex, $"Error getting books for tag {tagId}");
                return new List<BookListDto>();
            }
        }

        public async Task<List<TagDto>> GetTagsByBookAsync(int bookId)
        {
            try
            {
                var unusedTags = await _context.Tags
                    .Where(t => !_context.BookTags.Any(bt => bt.TagId == t.TagId))
                    .OrderBy(t => t.Name)
                    .ToListAsync();

                return unusedTags.Select(t => t.ToDto(0)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unused tags");
                return new List<TagDto>();
            }
        }

        public async Task<bool> DeleteUnusedTagsAsync()
        {
            try
            {
                var unusedTags = await _context.Tags
                    .Where(t => !_context.BookTags.Any(bt => bt.TagId == t.TagId))
                    .ToListAsync();

                if (!unusedTags.Any())
                {
                    _logger.LogInformation("No unused tags found to delete");
                    return true;
                }

                _context.Tags.RemoveRange(unusedTags);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Deleted {unusedTags.Count} unused tags");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting unused tags");
                return false;
            }
        }
    }
}