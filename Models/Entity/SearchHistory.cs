using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
 
namespace BookInfoFinder.Models.Entity
{
    [Table("SearchHistories")]
    public class SearchHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SearchHistoryId { get; set; }
 
        [StringLength(100)]
        public string? Title { get; set; }
 
        [StringLength(100)]
        public string? Author { get; set; }
 
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }
 
        public DateTime? Date { get; set; }

        [StringLength(200)]
        public string SearchQuery { get; set; } = string.Empty;

        public DateTime SearchedAt { get; set; } = DateTime.UtcNow;

        public int ResultCount { get; set; } = 0;
 
        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; } = null!;
 
        [ForeignKey("Book")]
        public int? BookId { get; set; } // Cho phép null
        public Book? Book { get; set; }  // Cho phép null
 
    }
}