namespace BookInfoFinder.Models.Dto;

public class RatingDto
{
    public int RatingId { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty; // Match entity property name
    public int Star { get; set; } // Match entity property name (not RatingValue)
    public DateTime CreatedAt { get; set; }
}

public class RatingCreateDto
{
    public int BookId { get; set; }
    public int UserId { get; set; }
    public int Star { get; set; } // Match entity property name (not RatingValue)
}

public class RatingUpdateDto
{
    public int RatingId { get; set; }
    public int Star { get; set; } // Match entity property name (not RatingValue)
}