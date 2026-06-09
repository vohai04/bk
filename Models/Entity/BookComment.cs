using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookInfoFinder.Models.Entity
{
    [Table("BookComments")]
    public class BookComment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BookCommentId { get; set; }

        [Required]
        public int BookId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(500, ErrorMessage = "Bình luận không được vượt quá 500 ký tự.")]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Nếu là bình luận gốc thì ParentCommentId = null
        /// Nếu là reply thì ParentCommentId != null
        /// </summary>
        public int? ParentCommentId { get; set; }

        /// <summary>
        /// Số sao chỉ áp dụng cho bình luận gốc (1–5)
        /// </summary>
        [Range(1, 5)]
        public int? Star { get; set; }

        // 🔹 Quan hệ tự liên kết (một bình luận có thể có nhiều reply)
        [ForeignKey(nameof(ParentCommentId))]
        public BookComment? ParentComment { get; set; }

        public ICollection<BookComment> Replies { get; set; } = new List<BookComment>();

        // 🔹 Quan hệ với sách
        [ForeignKey(nameof(BookId))]
        public Book? Book { get; set; }

        // 🔹 Quan hệ với người dùng
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}