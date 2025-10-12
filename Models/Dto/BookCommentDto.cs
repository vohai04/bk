namespace BookInfoFinder.Models.Dto
{
    public class BookCommentDto
    {
        public int BookCommentId { get; set; }
        public int BookId { get; set; }
        public int UserId { get; set; }
        public string Comment { get; set; } = "";
        public int? Star { get; set; } // Chỉ có ở comment gốc (1-5)
        public string UserName { get; set; } = "";
        public int Role { get; set; }
        public string RoleName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? ParentCommentId { get; set; } // null = comment gốc, !null = reply
        
        // Hierarchy information
        public int Level { get; set; } = 0; // 0 = root, 1 = reply to root, 2 = reply to reply...
        public bool IsRootComment => ParentCommentId == null;
        public bool IsReply => ParentCommentId != null;
        
        // Statistics
        public int ReplyCount { get; set; } // Số replies trực tiếp
        public int TotalRepliesCount { get; set; } // Tổng số replies (bao gồm cả nested)
        
        // Nested replies (optional - chỉ load khi cần)
        public List<BookCommentDto> Replies { get; set; } = new();
        
        // Parent comment info (cho replies)
        public string? ParentUserName { get; set; }
        public string? ParentComment { get; set; }
    }

    public class BookCommentCreateDto
    {
        public int BookId { get; set; }
        public int UserId { get; set; }
        public string Comment { get; set; } = "";
        
        // Star rating - chỉ cho comment gốc (ParentCommentId == null)
        public int? Star { get; set; } // 1-5, required for root comments
        
        // Reply information
        public int? ParentCommentId { get; set; } // null = root comment, !null = reply
        
        // Validation
        public bool IsRootComment => ParentCommentId == null;
        public bool IsReply => ParentCommentId != null;
    }

    public class BookCommentUpdateDto
    {
        public int BookCommentId { get; set; }
        public string Comment { get; set; } = "";
        public int? Star { get; set; } // Chỉ update star cho root comments
    }

    // DTO for comment tree/hierarchy display
    public class BookCommentTreeDto
    {
        public BookCommentDto RootComment { get; set; } = new();
        public List<BookCommentDto> AllReplies { get; set; } = new(); // Flattened replies for performance
        public int MaxDepth { get; set; }
        public int TotalRepliesCount { get; set; }
    }
}