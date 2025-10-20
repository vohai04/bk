using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IBookService
    {
        Task<List<string>> SuggestBookTitlesAsync(string keyword);
        Task<IEnumerable<BookListDto>> GetAllBooksAsync();
        Task<BookDetailDto?> GetBookByIdAsync(int bookId);
        Task<BookDto> CreateBookAsync(BookCreateDto bookCreateDto);
        Task<BookDto> UpdateBookAsync(BookUpdateDto bookUpdateDto);
        Task<bool> DeleteBookAsync(int bookId);
        Task<BookDetailDto?> GetBookDetailWithStatsAndCommentsAsync(int bookId, int page, int pageSize);
        Task<(List<BookListDto> Books, int TotalCount)> SearchBooksWithStatsPagedAsync(string? title, string? author, string? category, DateTime? publicationDate, int page, int pageSize, string? tag);
        Task<(List<BookDto> Books, int TotalCount)> SearchBooksAdminPagedAsync(string? title, string? author, string? category, DateTime? publicationDate, int page, int pageSize, string? tag, string? sort = null);
    }
}