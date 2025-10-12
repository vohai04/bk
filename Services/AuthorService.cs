using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services
{
    public class AuthorService : IAuthorService
    {
        private readonly BookContext _context;
        private readonly ILogger<AuthorService> _logger;

        public AuthorService(BookContext context, ILogger<AuthorService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<AuthorDto>> GetAllAuthorsAsync()
        {
            try
            {
                var authors = await _context.Authors.ToListAsync();
                var authorDtos = new List<AuthorDto>();
                
                foreach (var author in authors)
                {
                    var bookCount = await _context.Books.CountAsync(b => b.AuthorId == author.AuthorId);
                    authorDtos.Add(DtoMapper.ToDto(author, bookCount));
                }
                
                return authorDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all authors");
                return new List<AuthorDto>();
            }
        }

        public async Task<AuthorDto?> GetAuthorByIdAsync(int authorId)
        {
            try
            {
                var author = await _context.Authors
                    .FirstOrDefaultAsync(a => a.AuthorId == authorId);
                
                if (author == null) return null;

                var bookCount = await _context.Books.CountAsync(b => b.AuthorId == authorId);
                return DtoMapper.ToDto(author, bookCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting author by id: {AuthorId}", authorId);
                return null;
            }
        }

        public async Task<AuthorDto?> GetAuthorByNameAsync(string name)
        {
            try
            {
                var author = await _context.Authors
                    .FirstOrDefaultAsync(a => a.Name == name);
                
                if (author == null) return null;

                var bookCount = await _context.Books.CountAsync(b => b.AuthorId == author.AuthorId);
                return DtoMapper.ToDto(author, bookCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting author by name: {Name}", name);
                return null;
            }
        }

        public async Task<AuthorDto> CreateAuthorAsync(AuthorCreateDto authorCreateDto)
        {
            try
            {
                var author = DtoMapper.ToEntity(authorCreateDto);
                
                _context.Authors.Add(author);
                await _context.SaveChangesAsync();

                return DtoMapper.ToDto(author);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating author");
                throw;
            }
        }

        public async Task<AuthorDto> UpdateAuthorAsync(AuthorUpdateDto authorUpdateDto)
        {
            try
            {
                var author = await _context.Authors
                    .FirstOrDefaultAsync(a => a.AuthorId == authorUpdateDto.AuthorId);
                
                if (author == null)
                    throw new ArgumentException("Author not found");

                DtoMapper.UpdateEntity(authorUpdateDto, author);
                await _context.SaveChangesAsync();

                var bookCount = await _context.Books.CountAsync(b => b.AuthorId == author.AuthorId);
                return DtoMapper.ToDto(author, bookCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating author: {AuthorId}", authorUpdateDto.AuthorId);
                throw;
            }
        }

        public async Task<bool> DeleteAuthorAsync(int authorId)
        {
            try
            {
                var author = await _context.Authors
                    .FirstOrDefaultAsync(a => a.AuthorId == authorId);
                
                if (author == null) return false;

                // Check if author has books
                var hasBooks = await _context.Books.AnyAsync(b => b.AuthorId == authorId);
                if (hasBooks)
                {
                    _logger.LogWarning("Cannot delete author {AuthorId} - has associated books", authorId);
                    return false;
                }

                _context.Authors.Remove(author);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting author: {AuthorId}", authorId);
                return false;
            }
        }

        public async Task<List<AuthorDto>> SearchAuthorsAsync(string searchTerm)
        {
            try
            {
                var authors = await _context.Authors
                    .Where(a => a.Name.Contains(searchTerm) || 
                               (a.Biography != null && a.Biography.Contains(searchTerm)))
                    .ToListAsync();

                var authorDtos = new List<AuthorDto>();
                foreach (var author in authors)
                {
                    var bookCount = await _context.Books.CountAsync(b => b.AuthorId == author.AuthorId);
                    authorDtos.Add(DtoMapper.ToDto(author, bookCount));
                }

                return authorDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching authors with term: {SearchTerm}", searchTerm);
                return new List<AuthorDto>();
            }
        }

        public async Task<bool> IsAuthorNameExistsAsync(string name)
        {
            try
            {
                return await _context.Authors.AnyAsync(a => a.Name == name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if author name exists: {Name}", name);
                return false;
            }
        }

        public async Task<List<BookListDto>> GetBooksByAuthorAsync(int authorId)
        {
            try
            {
                var books = await _context.Books
                    .Where(b => b.AuthorId == authorId)
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
                _logger.LogError(ex, "Error getting books by author: {AuthorId}", authorId);
                return new List<BookListDto>();
            }
        }

        public async Task<(List<AuthorDto> Authors, int TotalCount)> GetAuthorsPagedAsync(int page, int pageSize)
        {
            try
            {
                var totalCount = await _context.Authors.CountAsync();
                
                var authors = await _context.Authors
                    .OrderBy(a => a.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var authorDtos = new List<AuthorDto>();
                foreach (var author in authors)
                {
                    var bookCount = await _context.Books.CountAsync(b => b.AuthorId == author.AuthorId);
                    authorDtos.Add(DtoMapper.ToDto(author, bookCount));
                }

                return (authorDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting authors paged");
                return (new List<AuthorDto>(), 0);
            }
        }
    }
}