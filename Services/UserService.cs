using BookInfoFinder.Data;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Models;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services
{
    public class UserService : IUserService
    {
        private readonly BookContext _context;
        private readonly IDashboardService _dashboardService;
        private readonly IHttpContextAccessor _http;

        public UserService(BookContext context, IDashboardService dashboardService, IHttpContextAccessor http)
        {
            _context = context;
            _dashboardService = dashboardService;
            _http = http;
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
        {
            var users = await _context.Users.ToListAsync();
            return users.Select(u => u.ToDto());
        }

        public async Task<UserDto?> GetUserByIdAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user?.ToDto();
        }

        public async Task<UserDto?> GetUserByEmailAsync(string email)
        {
            var normalized = email.Trim().ToLower();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.Trim().ToLower() == normalized);
            return user?.ToDto();
        }

        public async Task<UserDto> CreateUserAsync(UserCreateDto userCreateDto)
        {
            ValidateProfile(userCreateDto.FullName, userCreateDto.Email);
            if (string.IsNullOrWhiteSpace(userCreateDto.UserName) || userCreateDto.UserName.Length > 256)
                throw new ArgumentException("Tên đăng nhập không hợp lệ.");
            if (await IsUserNameExistsAsync(userCreateDto.UserName) || await IsEmailExistsAsync(userCreateDto.Email))
                throw new ArgumentException("Tên đăng nhập hoặc email đã được sử dụng.");
            userCreateDto.UserName = userCreateDto.UserName.Trim();
            userCreateDto.Email = userCreateDto.Email.Trim().ToLowerInvariant();
            userCreateDto.FullName = userCreateDto.FullName.Trim();
            var user = userCreateDto.ToEntity();
            user.Password = PasswordStorage.Hash(user, userCreateDto.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Log activity
            await _dashboardService.LogActivityAsync(
                _http.HttpContext?.Session.GetString("UserName") ?? user.UserName,
                "User Registered",
                $"Tạo tài khoản: “{user.UserName}”",
                "User",
                user.UserId,
                _http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? ""
            );

            return user.ToDto();
        }

        public async Task<UserDto> UpdateUserAsync(UserUpdateDto userUpdateDto)
        {
            ValidateProfile(userUpdateDto.FullName, userUpdateDto.Email);
            if (await _context.Users.AnyAsync(u => u.UserId != userUpdateDto.UserId && u.Email.ToLower() == userUpdateDto.Email.Trim().ToLower()))
                throw new ArgumentException("Email đã được sử dụng.");
            userUpdateDto.FullName = userUpdateDto.FullName.Trim();
            userUpdateDto.Email = userUpdateDto.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FindAsync(userUpdateDto.UserId);
            if (user == null)
                throw new ArgumentException("User not found");

            // Apply updates. Do NOT modify CreatedAt here (it must remain the original creation time).
            // UpdateEntity will set UpdatedAt; ensure UpdatedAt is set to now.
            userUpdateDto.UpdateEntity(user);
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // reload fresh entity from database to include any DB-side changes
            var refreshed = await _context.Users.FindAsync(user.UserId);
            return refreshed?.ToDto() ?? user.ToDto();
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            user.Password = PasswordStorage.Hash(user, newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        // Change password with validation of current password
        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            if (user.Status != 1 || !PasswordStorage.Verify(user, currentPassword)) return false;

            user.Password = PasswordStorage.Hash(user, newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<UserDto?> ValidateUserAsync(LoginRequestDto loginRequest)
        {
            if (loginRequest.Password.Length > 100 || loginRequest.UserName.Length > 256) return null;
            // Caller supplies the account-locked message after authentication.
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == loginRequest.UserName.Trim());

            if (user == null || !PasswordStorage.Verify(user, loginRequest.Password)) return null;
            if (!PasswordStorage.IsHash(user.Password))
            {
                user.Password = PasswordStorage.Hash(user, loginRequest.Password);
                await _context.SaveChangesAsync();
            }

            return user.ToDto();
        }

        public async Task<bool> IsUserNameExistsAsync(string userName)
        {
            var normalized = userName.Trim().ToLower();
            return await _context.Users
                .AnyAsync(u => u.UserName.Trim().ToLower() == normalized);
        }

        public async Task<bool> IsEmailExistsAsync(string email)
        {
            var normalized = email.Trim().ToLower();
            return await _context.Users
                .AnyAsync(u => u.Email.Trim().ToLower() == normalized);
        }

        private static void ValidateProfile(string name, string email)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || string.IsNullOrWhiteSpace(email) || email.Length > 256 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
                throw new ArgumentException("Tên hiển thị hoặc email không hợp lệ.");
        }

        public async Task<bool> ActivateUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.Status = 1;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.Status = 0;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
