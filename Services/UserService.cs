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

        public UserService(BookContext context, IDashboardService dashboardService)
        {
            _context = context;
            _dashboardService = dashboardService;
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
            var user = userCreateDto.ToEntity();
            
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Log activity
            await _dashboardService.LogActivityAsync(
                user.UserName,
                "User Registered",
                $"New user '{user.UserName}' registered",
                "User",
                user.UserId,
                ""
            );
            
            return user.ToDto();
        }

        public async Task<UserDto> UpdateUserAsync(UserUpdateDto userUpdateDto)
        {
            var user = await _context.Users.FindAsync(userUpdateDto.UserId);
            if (user == null)
                throw new ArgumentException("User not found");

            // Apply updates. Do NOT modify CreatedAt here (it must remain the original creation time).
            // UpdateEntity will set UpdatedAt; ensure UpdatedAt is set to now.
            userUpdateDto.UpdateEntity(user);
            user.UpdatedAt = DateTime.UtcNow;

            _context.Users.Update(user);
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

            user.Password = newPassword;
            user.UpdatedAt = DateTime.UtcNow;
            
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }

        // Change password with validation of current password
        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            // In this sample app passwords are stored in plain text. Validate directly.
            if (user.Password != currentPassword) return false;

            user.Password = newPassword;
            user.UpdatedAt = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<UserDto?> ValidateUserAsync(LoginRequestDto loginRequest)
        {
            // Find user by username regardless of status so caller can decide how to handle locked accounts
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == loginRequest.UserName);

            if (user == null || user.Password != loginRequest.Password)
                return null;

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

        public async Task<bool> ActivateUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.Status = 1;
            user.UpdatedAt = DateTime.UtcNow;
            
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.Status = 0;
            user.UpdatedAt = DateTime.UtcNow;
            
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}