using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
 
namespace BookInfoFinder.Models.Entity
{
    [Table("Ratings")]
    public class Rating
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RatingId { get; set; }
 
        [Required]
        public int BookId { get; set; }
 
        [Required]
        public int UserId { get; set; }
 
        [Required]
        [Range(1, 5)]
        public int Star { get; set; } // Bắt buộc

        [StringLength(500)]
        public string Review { get; set; } = string.Empty;
 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
 
        [ForeignKey(nameof(BookId))]
        public Book? Book { get; set; }
 
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}