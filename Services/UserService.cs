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

        public UserService(BookContext context)
        {
            _context = context;
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
            
            return user.ToDto();
        }

        public async Task<UserDto> UpdateUserAsync(UserUpdateDto userUpdateDto)
        {
            var user = await _context.Users.FindAsync(userUpdateDto.UserId);
            if (user == null)
                throw new ArgumentException("User not found");

            userUpdateDto.UpdateEntity(user);
            
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            
            return user.ToDto();
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

        public async Task<UserDto?> ValidateUserAsync(LoginRequestDto loginRequest)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == loginRequest.UserName && u.Status == 1);

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