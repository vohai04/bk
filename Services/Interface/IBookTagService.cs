using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IBookTagService
    {
        Task<List<TagDto>> GetTagsByBookIdAsync(int bookId);
        Task<List<BookListDto>> GetBooksByTagIdAsync(int tagId);
        Task<List<BookListDto>> GetBooksByTagNameAsync(string tagName);
        Task<(List<BookListDto> Books, int TotalCount)> GetBooksByTagPagedAsync(int tagId, int page, int pageSize);
        Task<bool> AddBookTagAsync(int bookId, int tagId);
        Task<bool> RemoveBookTagAsync(int bookId, int tagId);
        Task<bool> UpdateBookTagsAsync(int bookId, List<int> tagIds);
        Task<bool> RemoveAllTagsFromBookAsync(int bookId);
        Task<int> GetBookCountByTagAsync(int tagId);
        Task<bool> IsBookTaggedAsync(int bookId, int tagId);
        Task<List<TagDto>> GetPopularTagsAsync(int count = 10);
    }
}