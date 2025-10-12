using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
 
namespace BookInfoFinder.Models.Entity
{
    [Table("Publisher")]
    public class Publisher
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PublisherId { get; set; }
 
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
 
        [StringLength(100)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string ContactInfo { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
 
        public ICollection<Book> Books { get; set; } = new List<Book>();
 
    }
}