using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookInfoFinder.Models.Entity
{
    [Table("ChatMessages")]
    public class ChatMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ChatMessageId { get; set; }

        [Required]
        [StringLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = string.Empty; // "user" hoặc "assistant"

        [Required]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
