namespace BookInfoFinder.Models.Dto;

public class BookDto
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
    public List<TagDto> Tags { get; set; } = new();
    
    // Statistics (optional for admin views)
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int TotalComments { get; set; }
    public int TotalFavorites { get; set; }
}

public class BookCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Abstract { get; set; } = string.Empty;
    public string ImageBase64 { get; set; } = string.Empty;
    public DateTime PublicationDate { get; set; } // Match entity property name
    public int AuthorId { get; set; }
    public int CategoryId { get; set; }
    public int PublisherId { get; set; }
    public int UserId { get; set; } // Add UserId for book owner
    public List<int> TagIds { get; set; } = new();
}

public class BookUpdateDto
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Abstract { get; set; } = string.Empty;
    public string ImageBase64 { get; set; } = string.Empty;
    public DateTime PublicationDate { get; set; } // Match entity property name
    public int AuthorId { get; set; }
    public int CategoryId { get; set; }
    public int PublisherId { get; set; }
    public int UserId { get; set; } // Add UserId for book owner
    public List<int> TagIds { get; set; } = new();
}