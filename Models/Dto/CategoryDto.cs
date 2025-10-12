namespace BookInfoFinder.Models.Dto;

public class CategoryDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int BookCount { get; set; } // Số lượng sách trong danh mục
}

public class CategoryCreateDto
{
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Description { get; set; } = string.Empty;
}

public class CategoryUpdateDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Description { get; set; } = string.Empty;
}