using BookInfoFinder.Data;
using BookInfoFinder.Models.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace BookInfoFinder.Services;

public static class PasswordStorage
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static bool IsHash(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length >= 49 && bytes[0] is 0 or 1;
        }
        catch (FormatException) { return false; }
    }

    public static string Hash(User user, string password)
    {
        if (password.Length is < 6 or > 100)
            throw new ArgumentException("Mật khẩu cần từ 6 đến 100 ký tự.");
        return Hasher.HashPassword(user, password);
    }

    // Legacy credentials are upgraded without changing the user's password.
    public static bool Verify(User user, string password)
    {
        if (IsHash(user.Password))
        {
            try { return Hasher.VerifyHashedPassword(user, user.Password, password) != PasswordVerificationResult.Failed; }
            catch (Exception error) when (error is FormatException or ArgumentException) { return false; }
        }
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(user.Password), Encoding.UTF8.GetBytes(password));
    }

    public static async Task UpgradeLegacyAsync(BookContext context)
    {
        foreach (var user in await context.Users.ToListAsync())
            if (!IsHash(user.Password)) user.Password = Hasher.HashPassword(user, user.Password);
        await context.SaveChangesAsync();
    }
}
