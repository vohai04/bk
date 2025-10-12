using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface ITagService
    {
        Task<List<TagDto>> GetAllTagsAsync();
        Task<TagDto?> GetTagByIdAsync(int tagId);
        Task<TagDto> CreateTagAsync(TagCreateDto tagCreateDto);
        Task<TagDto> UpdateTagAsync(TagUpdateDto tagUpdateDto);
        Task<bool> DeleteTagAsync(int tagId);
        Task<bool> IsTagNameExistsAsync(string tagName);
        Task<List<BookListDto>> GetBooksByTagAsync(int tagId);
        Task<List<TagDto>> GetTagsByBookAsync(int bookId);
        Task<(List<TagDto> Tags, int TotalCount)> GetTagsPagedAsync(int page, int pageSize, string? search = null);
        Task<List<string>> SuggestTagsAsync(string keyword);
        Task<List<TagDto>> SearchTagsAsync(string searchTerm);
    }
}