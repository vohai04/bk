namespace BookInfoFinder.Models.Dto;

public class PublisherDto
{
    public int PublisherId { get; set; }
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Address { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int BookCount { get; set; } // Số lượng sách của nhà xuất bản
}

public class PublisherCreateDto
{
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Address { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
}

public class PublisherUpdateDto
{
    public int PublisherId { get; set; }
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Address { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
}