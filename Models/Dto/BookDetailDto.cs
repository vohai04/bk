namespace BookInfoFinder.Models.Dto;

public class BookDetailDto
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Abstract { get; set; } = string.Empty;
    public string ImageBase64 { get; set; } = string.Empty;
    public DateTime PublicationDate { get; set; }
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int PublisherId { get; set; }
    public string PublisherName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();

    // Rating
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }

    // Comment section
    public List<BookCommentDto> Comments { get; set; } = new();

    // Tổng số bình luận gốc (phục vụ phân trang)
    public int TotalComments { get; set; }
}