using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
 
namespace BookInfoFinder.Models.Entity
{
    [Table("Users")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }
 
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;
 
        [Required]
        [StringLength(256)]
        public string UserName { get; set; } = string.Empty;
 
        [Required]
        [StringLength(60)]
        public string Password { get; set; } = string.Empty;
 
        [Required]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
 
        public ICollection<Book> Books { get; set; } = new List<Book>();
 
        [Required]
        public Role Role { get; set; } = Role.User;
 
        [Required]
        public int Status { get; set; } = 1; // 1: hoạt động, 0: off
 
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
        public ICollection<SearchHistory> SearchHistories { get; set; } = new List<SearchHistory>();
        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public ICollection<BookComment> BookComments { get; set; } = new List<BookComment>();
    }
}