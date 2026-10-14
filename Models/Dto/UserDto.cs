namespace BookInfoFinder.Models.Dto;

public class UserDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty; // Match entity property name
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public int Status { get; set; } // Match entity property (1 = active, 0 = inactive)
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UserCreateDto
{
    public string UserName { get; set; } = string.Empty; // Match entity property name
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}

public class UserUpdateDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty; // Match entity property name
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int Status { get; set; } // Match entity property (1 = active, 0 = inactive)
}

public class LoginRequestDto
{
    public string UserName { get; set; } = string.Empty; // Match entity property name
    public string Password { get; set; } = string.Empty;
}

public static class AvatarUrls
{
    public static string? For(int userId, string? version) => string.IsNullOrEmpty(version)
        ? null : $"/avatars/{userId}?v={Uri.EscapeDataString(version)}";
}

public sealed record AvatarUpdateDto(int UserId, string? AvatarUrl, DateTime UpdatedAt);
public sealed record AvatarImageDto(byte[] Image, string Version);
