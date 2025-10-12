using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface ICategoryService
    {
        Task<List<CategoryDto>> GetAllCategoriesAsync();
        Task<CategoryDto?> GetCategoryByIdAsync(int categoryId);
        Task<CategoryDto?> GetCategoryByNameAsync(string name);
        Task<CategoryDto> CreateCategoryAsync(CategoryCreateDto categoryCreateDto);
        Task<CategoryDto> UpdateCategoryAsync(CategoryUpdateDto categoryUpdateDto);
        Task<bool> DeleteCategoryAsync(int categoryId);
        Task<List<CategoryDto>> SearchCategoriesAsync(string searchTerm);
        Task<bool> IsCategoryNameExistsAsync(string name);
        Task<List<BookListDto>> GetBooksByCategoryAsync(int categoryId);
        Task<(List<CategoryDto> Categories, int TotalCount)> GetCategoriesPagedAsync(int page, int pageSize);
    }
}