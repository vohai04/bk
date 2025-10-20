namespace BookInfoFinder.Models.Dto;

public class FavoriteDto
{
    public int FavoriteId { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string BookImage { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty; // Match entity property name
    public string CategoryName { get; set; } = string.Empty; // Added property for category name
    public List<string> Tags { get; set; } = new(); // Added property for tags
    public DateTime CreatedAt { get; set; }
}

public class FavoriteCreateDto
{
    public int BookId { get; set; }
    public int UserId { get; set; }
}

public class FavoriteDeleteDto
{
    public int BookId { get; set; }
    public int UserId { get; set; }
}