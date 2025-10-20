using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services.Interface
{
    public interface IUserService
    {
        Task<IEnumerable<UserDto>> GetAllUsersAsync();
        Task<UserDto?> GetUserByIdAsync(int userId);
        Task<UserDto?> GetUserByEmailAsync(string email);
        Task<UserDto> CreateUserAsync(UserCreateDto userCreateDto);
        Task<UserDto> UpdateUserAsync(UserUpdateDto userUpdateDto);
        Task<bool> DeleteUserAsync(int userId);
        Task<bool> ResetPasswordAsync(int userId, string newPassword);
    // Change password with current-password validation
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
        Task<UserDto?> ValidateUserAsync(LoginRequestDto loginRequest);
        Task<bool> IsUserNameExistsAsync(string userName); // Fixed property name
        Task<bool> IsEmailExistsAsync(string email);
        Task<bool> ActivateUserAsync(int userId);
        Task<bool> DeactivateUserAsync(int userId);
    }
}