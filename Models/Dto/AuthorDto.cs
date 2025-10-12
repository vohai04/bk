namespace BookInfoFinder.Models.Dto;

public class AuthorDto
{
    public int AuthorId { get; set; }
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Biography { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Nationality { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int BookCount { get; set; } // Số lượng sách của tác giả
}

public class AuthorCreateDto
{
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Biography { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Nationality { get; set; } = string.Empty;
}

public class AuthorUpdateDto
{
    public int AuthorId { get; set; }
    public string Name { get; set; } = string.Empty; // Match entity property name
    public string Biography { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Nationality { get; set; } = string.Empty;
}