namespace BookInfoFinder.Models.Entity;

// Image bytes are stored separately so loading users/comments stays lightweight.
public sealed class UserAvatar
{
    public int UserId { get; set; }
    public byte[] Image { get; set; } = [];
    public string Version { get; set; } = string.Empty;
}
