namespace BookInfoFinder.Models.Dto;

public class TagDto
{
    public int TagId { get; set; }
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int BookCount { get; set; } // Số lượng sách có tag này
}

public class TagCreateDto
{
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Description { get; set; } = string.Empty;
}

public class TagUpdateDto
{
    public int TagId { get; set; }
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Description { get; set; } = string.Empty;
}