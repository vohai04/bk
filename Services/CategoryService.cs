using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly BookContext _context;
        private readonly ILogger<CategoryService> _logger;
        private readonly IDashboardService _dashboardService;

        public CategoryService(BookContext context, ILogger<CategoryService> logger, IDashboardService dashboardService)
        {
            _context = context;
            _logger = logger;
            _dashboardService = dashboardService;
        }

        public async Task<List<CategoryDto>> GetAllCategoriesAsync()
        {
            try
            {
                var categories = await _context.Categories.ToListAsync();
                var categoryDtos = new List<CategoryDto>();

                foreach (var category in categories)
                {
                    var bookCount = await _context.Books.CountAsync(b => b.CategoryId == category.CategoryId);
                    categoryDtos.Add(category.ToDto(bookCount));
                }

                return categoryDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all categories");
                return new List<CategoryDto>();
            }
        }

        public async Task<CategoryDto?> GetCategoryByIdAsync(int categoryId)
        {
            try
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

                if (category == null) return null;

                var bookCount = await _context.Books.CountAsync(b => b.CategoryId == categoryId);
                return category.ToDto(bookCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category by id: {CategoryId}", categoryId);
                return null;
            }
        }

        public async Task<CategoryDto?> GetCategoryByNameAsync(string name)
        {
            try
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Name == name);

                if (category == null) return null;

                var bookCount = await _context.Books.CountAsync(b => b.CategoryId == category.CategoryId);
                return DtoMapper.ToDto(category, bookCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category by name: {Name}", name);
                return null;
            }
        }

        public async Task<CategoryDto> CreateCategoryAsync(CategoryCreateDto categoryCreateDto)
        {
            try
            {
                var category = categoryCreateDto.ToEntity();
                category.CreatedAt = DateTime.UtcNow;

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                return category.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                throw;
            }
        }

        public async Task<CategoryDto> UpdateCategoryAsync(CategoryUpdateDto categoryUpdateDto)
        {
            try
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == categoryUpdateDto.CategoryId);

                if (category == null)
                    throw new ArgumentException("Category not found");

                categoryUpdateDto.UpdateEntity(category);
                await _context.SaveChangesAsync();

                var bookCount = await _context.Books.CountAsync(b => b.CategoryId == category.CategoryId);
                return category.ToDto(bookCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category: {CategoryId}", categoryUpdateDto.CategoryId);
                throw;
            }
        }

        public async Task<bool> DeleteCategoryAsync(int categoryId)
        {
            try
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

                if (category == null) return false;

                // Check if category has books
                var hasBooks = await _context.Books.AnyAsync(b => b.CategoryId == categoryId);
                if (hasBooks)
                {
                    _logger.LogWarning("Cannot delete category {CategoryId} - has associated books", categoryId);
                    return false;
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category: {CategoryId}", categoryId);
                return false;
            }
        }

        public async Task<List<CategoryDto>> SearchCategoriesAsync(string searchTerm)
        {
            try
            {
                var categories = await _context.Categories
                    .Where(c => c.Name.ToLower().Contains(searchTerm.ToLower()) || 
                               (c.Description != null && c.Description.ToLower().Contains(searchTerm.ToLower())))
                    .ToListAsync();

                var categoryDtos = new List<CategoryDto>();
                foreach (var category in categories)
                {
                    var bookCount = await _context.Books.CountAsync(b => b.CategoryId == category.CategoryId);
                    categoryDtos.Add(category.ToDto(bookCount));
                }

                return categoryDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching categories with term: {SearchTerm}", searchTerm);
                return new List<CategoryDto>();
            }
        }

        public async Task<bool> IsCategoryNameExistsAsync(string name)
        {
            try
            {
                return await _context.Categories.AnyAsync(c => c.Name == name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if category name exists: {Name}", name);
                return false;
            }
        }

        public async Task<List<BookListDto>> GetBooksByCategoryAsync(int categoryId)
        {
            try
            {
                var books = await _context.Books
                    .Where(b => b.CategoryId == categoryId)
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
                _logger.LogError(ex, "Error getting books by category: {CategoryId}", categoryId);
                return new List<BookListDto>();
            }
        }

        public async Task<(List<CategoryDto> Categories, int TotalCount)> GetCategoriesPagedAsync(int page, int pageSize)
        {
            try
            {
                var totalCount = await _context.Categories.CountAsync();
                
                var categories = await _context.Categories
                    .OrderBy(c => c.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var categoryDtos = new List<CategoryDto>();
                foreach (var category in categories)
                {
                    var bookCount = await _context.Books.CountAsync(b => b.CategoryId == category.CategoryId);
                    categoryDtos.Add(category.ToDto(bookCount));
                }

                return (categoryDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories paged");
                return (new List<CategoryDto>(), 0);
            }
        }
    }
}