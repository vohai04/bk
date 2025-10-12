using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
 
namespace BookInfoFinder.Models.Entity
{
    [Table("BookTags")]
    public class BookTag
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BookTagId { get; set; }
 
        [ForeignKey("Book")]
        public int BookId { get; set; }
        public Book Book { get; set; } = null!;
 
        [ForeignKey("Tag")]
        public int TagId { get; set; }
        public Tag Tag { get; set; } = null!;
    }
}
 