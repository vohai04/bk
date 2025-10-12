using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services
{
    public class PublisherService : IPublisherService
    {
        private readonly BookContext _context;
        private readonly ILogger<PublisherService> _logger;

        public PublisherService(BookContext context, ILogger<PublisherService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<PublisherDto>> GetAllPublishersAsync()
        {
            try
            {
                var publishers = await _context.Publishers.ToListAsync();
                var publisherDtos = new List<PublisherDto>();

                foreach (var publisher in publishers)
                {
                    var bookCount = await _context.Books.CountAsync(b => b.PublisherId == publisher.PublisherId);
                    publisherDtos.Add(publisher.ToDto(bookCount));
                }

                return publisherDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all publishers");
                return new List<PublisherDto>();
            }
        }

        public async Task<PublisherDto?> GetPublisherByIdAsync(int publisherId)
        {
            try
            {
                var publisher = await _context.Publishers
                    .FirstOrDefaultAsync(p => p.PublisherId == publisherId);

                if (publisher == null) return null;

                var bookCount = await _context.Books.CountAsync(b => b.PublisherId == publisherId);
                return publisher.ToDto(bookCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting publisher by id: {PublisherId}", publisherId);
                return null;
            }
        }

        public async Task<PublisherDto> CreatePublisherAsync(PublisherCreateDto publisherCreateDto)
        {
            try
            {
                var publisher = publisherCreateDto.ToEntity();
                publisher.CreatedAt = DateTime.UtcNow;

                _context.Publishers.Add(publisher);
                await _context.SaveChangesAsync();

                return publisher.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating publisher");
                throw;
            }
        }

        public async Task<PublisherDto> UpdatePublisherAsync(PublisherUpdateDto publisherUpdateDto)
        {
            try
            {
                var publisher = await _context.Publishers
                    .FirstOrDefaultAsync(p => p.PublisherId == publisherUpdateDto.PublisherId);

                if (publisher == null)
                    throw new ArgumentException("Publisher not found");

                publisherUpdateDto.UpdateEntity(publisher);
                await _context.SaveChangesAsync();

                var bookCount = await _context.Books.CountAsync(b => b.PublisherId == publisher.PublisherId);
                return publisher.ToDto(bookCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating publisher: {PublisherId}", publisherUpdateDto.PublisherId);
                throw;
            }
        }

        public async Task<bool> DeletePublisherAsync(int publisherId)
        {
            try
            {
                var publisher = await _context.Publishers
                    .FirstOrDefaultAsync(p => p.PublisherId == publisherId);

                if (publisher == null) return false;

                // Check if publisher has books
                var hasBooks = await _context.Books.AnyAsync(b => b.PublisherId == publisherId);
                if (hasBooks)
                {
                    _logger.LogWarning("Cannot delete publisher {PublisherId} - has associated books", publisherId);
                    return false;
                }

                _context.Publishers.Remove(publisher);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting publisher: {PublisherId}", publisherId);
                return false;
            }
        }

        public async Task<bool> IsPublisherNameExistsAsync(string name)
        {
            try
            {
                return await _context.Publishers.AnyAsync(p => p.Name == name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if publisher name exists: {Name}", name);
                return false;
            }
        }

        public async Task<List<BookListDto>> GetBooksByPublisherAsync(int publisherId)
        {
            try
            {
                var books = await _context.Books
                    .Where(b => b.PublisherId == publisherId)
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
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting books by publisher: {PublisherId}", publisherId);
                return new List<BookListDto>();
            }
        }

        public async Task<(List<PublisherDto> Publishers, int TotalCount)> GetPublishersPagedAsync(int page, int pageSize, string? search = null)
        {
            try
            {
                var query = _context.Publishers.AsQueryable();
                
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(p => p.Name.Contains(search) || 
                                           (p.Address != null && p.Address.Contains(search)));
                }

                var totalCount = await query.CountAsync();
                
                var publishers = await query
                    .OrderBy(p => p.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var publisherDtos = new List<PublisherDto>();
                foreach (var publisher in publishers)
                {
                    var bookCount = await _context.Books.CountAsync(b => b.PublisherId == publisher.PublisherId);
                    publisherDtos.Add(publisher.ToDto(bookCount));
                }

                return (publisherDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting publishers paged");
                return (new List<PublisherDto>(), 0);
            }
        }

        public async Task<List<PublisherDto>> SearchPublishersAsync(string searchTerm)
        {
            try
            {
                var publishers = await _context.Publishers
                    .Where(p => p.Name.Contains(searchTerm) || 
                               (p.Address != null && p.Address.Contains(searchTerm)))
                    .ToListAsync();

                var publisherDtos = new List<PublisherDto>();
                foreach (var publisher in publishers)
                {
                    var bookCount = await _context.Books.CountAsync(b => b.PublisherId == publisher.PublisherId);
                    publisherDtos.Add(publisher.ToDto(bookCount));
                }

                return publisherDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching publishers with term: {SearchTerm}", searchTerm);
                return new List<PublisherDto>();
            }
        }
    }
}