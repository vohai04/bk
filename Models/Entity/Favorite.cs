using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
 
namespace BookInfoFinder.Models.Entity
{
    [Table("Favorites")]
    public class Favorite
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FavoriteId { get; set; }
 
        [ForeignKey("User")]
        public int UserId { get; set; }
       
        public User User { get; set; } = null!;
 
        [ForeignKey("Book")]
        public int BookId { get; set; }
 
        public Book Book { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 
    }
}