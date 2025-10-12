using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
 
namespace BookInfoFinder.Models.Entity
{
    [Table("Authors")]
    public class Author
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AuthorId { get; set; }
 
        [StringLength(30)]
        public string Name { get; set; } = string.Empty;
 
        [StringLength(100)]
        public string? Biography { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(50)]
        public string Nationality { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [JsonIgnore]
        public ICollection<Book> Books { get; set; } = new List<Book>();
 
    }
}