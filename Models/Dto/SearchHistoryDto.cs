namespace BookInfoFinder.Models.Dto;

public class SearchHistoryDto
{
    public int SearchHistoryId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty; // Match entity property name
    public string? Title { get; set; } // Match entity property
    public string? Author { get; set; } // Match entity property
    public int? CategoryId { get; set; } // Match entity property
    public string? CategoryName { get; set; } // Category name for display
    public int? BookId { get; set; } // Match entity property
    public string SearchQuery { get; set; } = string.Empty;
    public DateTime SearchedAt { get; set; }
    public int ResultCount { get; set; }
}

public class SearchHistoryCreateDto
{
    public int UserId { get; set; }
    public string? Title { get; set; } // Match entity property
    public string? Author { get; set; } // Match entity property
    public int? CategoryId { get; set; } // Match entity property
    public int? BookId { get; set; } // Match entity property
    public string SearchQuery { get; set; } = string.Empty;
    public int ResultCount { get; set; }
}