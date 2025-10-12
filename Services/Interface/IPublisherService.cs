using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IPublisherService
    {
        Task<List<PublisherDto>> GetAllPublishersAsync();
        Task<PublisherDto?> GetPublisherByIdAsync(int publisherId);
        Task<PublisherDto> CreatePublisherAsync(PublisherCreateDto publisherCreateDto);
        Task<PublisherDto> UpdatePublisherAsync(PublisherUpdateDto publisherUpdateDto);
        Task<bool> DeletePublisherAsync(int publisherId);
        Task<bool> IsPublisherNameExistsAsync(string name);
        Task<List<BookListDto>> GetBooksByPublisherAsync(int publisherId);
        Task<(List<PublisherDto> Publishers, int TotalCount)> GetPublishersPagedAsync(int page, int pageSize, string? search = null);
        Task<List<PublisherDto>> SearchPublishersAsync(string searchTerm);
    }
}