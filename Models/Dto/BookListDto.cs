namespace BookInfoFinder.Models.Dto;

public class BookListDto
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ImageBase64 { get; set; } = string.Empty;
    public DateTime PublicationDate { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Abstract { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int TotalFavorites { get; set; }
}
 