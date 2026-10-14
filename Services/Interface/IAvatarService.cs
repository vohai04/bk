using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface;

public interface IAvatarService
{
    Task<AvatarUpdateDto> UploadAsync(int userId, IFormFile file, CancellationToken cancellationToken);
    Task<AvatarUpdateDto> DeleteAsync(int userId, CancellationToken cancellationToken);
    Task<AvatarImageDto?> GetAsync(int userId, CancellationToken cancellationToken);
}
