using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IAuthorService
    {
        Task<List<AuthorDto>> GetAllAuthorsAsync();
        Task<AuthorDto?> GetAuthorByIdAsync(int authorId);
        Task<AuthorDto?> GetAuthorByNameAsync(string name);
        Task<AuthorDto> CreateAuthorAsync(AuthorCreateDto authorCreateDto);
        Task<AuthorDto> UpdateAuthorAsync(AuthorUpdateDto authorUpdateDto);
        Task<bool> DeleteAuthorAsync(int authorId);
        Task<List<AuthorDto>> SearchAuthorsAsync(string searchTerm);
        Task<bool> IsAuthorNameExistsAsync(string name);
        Task<List<BookListDto>> GetBooksByAuthorAsync(int authorId);
        Task<(List<AuthorDto> Authors, int TotalCount)> GetAuthorsPagedAsync(int page, int pageSize);
    }
}