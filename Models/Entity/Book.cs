namespace BookInfoFinder.Models.Entity
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
 
    [Table("Books")]
    public class Book
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BookId { get; set; }
 
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;
 
        [Required]
        [StringLength(15)]
        public string ISBN { get; set; } = string.Empty;
 
        [ForeignKey("Author")]
        [Required]
        public int AuthorId { get; set; }
        public Author? Author { get; set; } // <-- nullable
 
        [ForeignKey("Category")]
        [Required]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
 
        [ForeignKey("Publisher")]
        [Required]
        public int PublisherId { get; set; }
        public Publisher? Publisher { get; set; }
 
        [ForeignKey("User")]
        [Required]
        public int UserId { get; set; }
        public User? User { get; set; } // <-- nullable
 
        public DateTime PublicationDate { get; set; }
 
        [StringLength(50)]
        public string? Description { get; set; }
 
        [StringLength(500)]
        public string? Abstract { get; set; }
 
        [Column(TypeName = "text")]
        public string? ImageBase64 { get; set; }
 
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
        public ICollection<BookTag> BookTags { get; set; } = new List<BookTag>();
        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public ICollection<BookComment> BookComments { get; set; } = new List<BookComment>();
        public ICollection<SearchHistory> SearchHistories { get; set; } = new List<SearchHistory>();
       
    }
}