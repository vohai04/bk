using System.ComponentModel.DataAnnotations;

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
    [Required(ErrorMessage = "Nhập tên sách.")]
    [StringLength(100, ErrorMessage = "Tên sách không quá 100 ký tự.")]
    public string Title { get; set; } = string.Empty;
    [Required(ErrorMessage = "Nhập mã ISBN.")]
    [RegularExpression(@"(?:[0-9]{13}|[0-9]{9}[0-9Xx])", ErrorMessage = "ISBN cần 10 hoặc 13 ký tự, không có dấu gạch nối.")]
    public string ISBN { get; set; } = string.Empty;
    [Required(ErrorMessage = "Nhập mô tả ngắn.")]
    [StringLength(50, ErrorMessage = "Mô tả ngắn không quá 50 ký tự.")]
    public string Description { get; set; } = string.Empty;
    [Required(ErrorMessage = "Nhập tóm tắt sách.")]
    [StringLength(200, ErrorMessage = "Tóm tắt không quá 200 ký tự.")]
    public string Abstract { get; set; } = string.Empty;
    public string? ImageBase64 { get; set; } = string.Empty;
    public DateTime PublicationDate { get; set; } // Match entity property name
    [Range(1, int.MaxValue, ErrorMessage = "Chọn tác giả.")]
    public int AuthorId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Chọn thể loại.")]
    public int CategoryId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Chọn nhà xuất bản.")]
    public int PublisherId { get; set; }
    public int UserId { get; set; } // Add UserId for book owner
    public List<int> TagIds { get; set; } = new();
}

public class BookUpdateDto
{
    public int BookId { get; set; }
    [Required(ErrorMessage = "Nhập tên sách.")]
    [StringLength(100, ErrorMessage = "Tên sách không quá 100 ký tự.")]
    public string Title { get; set; } = string.Empty;
    [Required(ErrorMessage = "Nhập mã ISBN.")]
    [RegularExpression(@"(?:[0-9]{13}|[0-9]{9}[0-9Xx])", ErrorMessage = "ISBN cần 10 hoặc 13 ký tự, không có dấu gạch nối.")]
    public string ISBN { get; set; } = string.Empty;
    [Required(ErrorMessage = "Nhập mô tả ngắn.")]
    [StringLength(50, ErrorMessage = "Mô tả ngắn không quá 50 ký tự.")]
    public string Description { get; set; } = string.Empty;
    [Required(ErrorMessage = "Nhập tóm tắt sách.")]
    [StringLength(200, ErrorMessage = "Tóm tắt không quá 200 ký tự.")]
    public string Abstract { get; set; } = string.Empty;
    public string? ImageBase64 { get; set; } = string.Empty;
    public DateTime PublicationDate { get; set; } // Match entity property name
    [Range(1, int.MaxValue, ErrorMessage = "Chọn tác giả.")]
    public int AuthorId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Chọn thể loại.")]
    public int CategoryId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Chọn nhà xuất bản.")]
    public int PublisherId { get; set; }
    public int UserId { get; set; } // Add UserId for book owner
    public List<int> TagIds { get; set; } = new();
}