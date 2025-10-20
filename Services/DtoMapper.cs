using BookInfoFinder.Models.Entity;
using BookInfoFinder.Models;
using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Services
{
    public static class DtoMapper
    {
        // User Mapping
        public static UserDto ToDto(this User user)
        {
            return new UserDto
            {
                UserId = user.UserId,
                UserName = user.UserName, // Fixed property name
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role.ToString(),
                Status = user.Status, // Use int status instead of bool
                CreatedAt = user.CreatedAt, // Now available in entity
                UpdatedAt = user.UpdatedAt // Now available in entity
            };
        }

        public static User ToEntity(this UserCreateDto dto)
        {
            return new User
            {
                UserName = dto.UserName, // Fixed property name
                Email = dto.Email,
                Password = dto.Password,
                FullName = dto.FullName,
                Role = Enum.Parse<Role>(dto.Role),
                Status = 1, // Active
                CreatedAt = DateTime.UtcNow
            };
        }

        public static void UpdateEntity(this UserUpdateDto dto, User entity)
        {
            entity.UserName = dto.UserName; // Fixed property name
            entity.Email = dto.Email;
            entity.FullName = dto.FullName;
            entity.Role = Enum.Parse<Role>(dto.Role);
            entity.Status = dto.Status; // Use int status
            entity.UpdatedAt = DateTime.UtcNow;
        }

        // Author Mapping
        public static AuthorDto ToDto(this Author author, int bookCount = 0)
        {
            return new AuthorDto
            {
                AuthorId = author.AuthorId,
                Name = author.Name, // Fixed property name
                Biography = author.Biography ?? "",
                DateOfBirth = author.DateOfBirth, // Now available in entity
                Nationality = author.Nationality ?? "", // Now available in entity
                CreatedAt = author.CreatedAt, // Now available in entity
                UpdatedAt = author.UpdatedAt, // Now available in entity
                BookCount = bookCount
            };
        }

        public static Author ToEntity(this AuthorCreateDto dto)
        {
            return new Author
            {
                Name = dto.Name, // Fixed property name
                Biography = dto.Biography,
                DateOfBirth = dto.DateOfBirth?.Kind == DateTimeKind.Unspecified 
                    ? DateTime.SpecifyKind(dto.DateOfBirth.Value, DateTimeKind.Utc) 
                    : dto.DateOfBirth?.ToUniversalTime(),
                Nationality = dto.Nationality,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static void UpdateEntity(this AuthorUpdateDto dto, Author entity)
        {
            entity.Name = dto.Name; // Fixed property name
            entity.Biography = dto.Biography;
            entity.DateOfBirth = dto.DateOfBirth?.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(dto.DateOfBirth.Value, DateTimeKind.Utc) 
                : dto.DateOfBirth?.ToUniversalTime();
            entity.Nationality = dto.Nationality;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        // Category Mapping
        public static CategoryDto ToDto(this Category category, int bookCount = 0)
        {
            return new CategoryDto
            {
                CategoryId = category.CategoryId,
                Name = category.Name, // Fixed property name
                Description = category.Description ?? "", // Now available in entity
                CreatedAt = category.CreatedAt, // Now available in entity
                UpdatedAt = category.UpdatedAt, // Now available in entity
                BookCount = bookCount
            };
        }

        public static Category ToEntity(this CategoryCreateDto dto)
        {
            return new Category
            {
                Name = dto.Name, // Fixed property name
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static void UpdateEntity(this CategoryUpdateDto dto, Category entity)
        {
            entity.Name = dto.Name; // Fixed property name
            entity.Description = dto.Description;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        // Publisher Mapping
        public static PublisherDto ToDto(this Publisher publisher, int bookCount = 0)
        {
            return new PublisherDto
            {
                PublisherId = publisher.PublisherId,
                Name = publisher.Name, // Fixed property name
                Address = publisher.Address ?? "",
                ContactInfo = publisher.ContactInfo ?? "", // Now available in entity
                CreatedAt = publisher.CreatedAt, // Now available in entity
                UpdatedAt = publisher.UpdatedAt, // Now available in entity
                BookCount = bookCount
            };
        }

        public static Publisher ToEntity(this PublisherCreateDto dto)
        {
            return new Publisher
            {
                Name = dto.Name, // Fixed property name
                Address = dto.Address,
                ContactInfo = dto.ContactInfo,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static void UpdateEntity(this PublisherUpdateDto dto, Publisher entity)
        {
            entity.Name = dto.Name; // Fixed property name
            entity.Address = dto.Address;
            entity.ContactInfo = dto.ContactInfo;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        // Tag Mapping
        public static TagDto ToDto(this Tag tag, int bookCount = 0)
        {
            return new TagDto
            {
                TagId = tag.TagId,
                Name = tag.Name, // Use correct property name
                Description = tag.Description ?? string.Empty, // Fix: use actual description
                CreatedAt = tag.CreatedAt, // Now available in entity
                UpdatedAt = tag.UpdatedAt, // Now available in entity
                BookCount = bookCount
            };
        }

        public static Tag ToEntity(this TagCreateDto dto)
        {
            return new Tag
            {
                Name = dto.Name, // Use correct property name
                CreatedAt = DateTime.UtcNow
            };
        }

        public static void UpdateEntity(this TagUpdateDto dto, Tag entity)
        {
            entity.Name = dto.Name; // Use correct property name
            entity.UpdatedAt = DateTime.UtcNow;
        }

        // Rating Mapping
        public static RatingDto ToDto(this Rating rating)
        {
            return new RatingDto
            {
                RatingId = rating.RatingId,
                BookId = rating.BookId,
                BookTitle = rating.Book?.Title ?? "",
                UserId = rating.UserId,
                UserName = rating.User?.UserName ?? "",
                Star = rating.Star,
                CreatedAt = rating.CreatedAt
            };
        }

        public static Rating ToEntity(this RatingCreateDto dto)
        {
            return new Rating
            {
                BookId = dto.BookId,
                UserId = dto.UserId,
                Star = dto.Star,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static void UpdateEntity(this RatingUpdateDto dto, Rating entity)
        {
            entity.Star = dto.Star;
        }

        // Favorite Mapping
        public static FavoriteDto ToDto(this Favorite favorite, string bookTitle = "", string bookImage = "", string authorName = "", string userName = "", string categoryName = "Không rõ", List<string>? tags = null)
        {
            return new FavoriteDto
            {
                FavoriteId = favorite.FavoriteId,
                BookId = favorite.BookId,
                BookTitle = bookTitle,
                BookImage = bookImage,
                AuthorName = authorName,
                UserId = favorite.UserId,
                UserName = userName, // Fixed property name
                CategoryName = categoryName ?? "Không rõ", // Ensure non-null value
                Tags = tags ?? new List<string>(), // Ensure non-null value
                CreatedAt = favorite.CreatedAt
            };
        }

        public static Favorite ToEntity(this FavoriteCreateDto dto)
        {
            return new Favorite
            {
                BookId = dto.BookId,
                UserId = dto.UserId,
                CreatedAt = DateTime.UtcNow
            };
        }

        // SearchHistory Mapping
        public static SearchHistoryDto ToDto(this SearchHistory searchHistory, string userName = "")
        {
            return new SearchHistoryDto
            {
                SearchHistoryId = searchHistory.SearchHistoryId,
                UserId = searchHistory.UserId,
                UserName = userName, // Fixed property name
                Title = searchHistory.Title,
                Author = searchHistory.Author,
                CategoryId = searchHistory.CategoryId,
                BookId = searchHistory.BookId,
                SearchQuery = searchHistory.SearchQuery,
                SearchedAt = searchHistory.SearchedAt,
                ResultCount = searchHistory.ResultCount
            };
        }

        public static SearchHistory ToEntity(this SearchHistoryCreateDto dto)
        {
            return new SearchHistory
            {
                UserId = dto.UserId,
                Title = dto.Title,
                Author = dto.Author,
                CategoryId = dto.CategoryId,
                BookId = dto.BookId,
                SearchQuery = dto.SearchQuery,
                ResultCount = dto.ResultCount,
                SearchedAt = DateTime.UtcNow
            };
        }

        // Book Mapping
        public static BookDto ToDto(this Book book)
        {
            return new BookDto
            {
                BookId = book.BookId,
                Title = book.Title,
                ISBN = book.ISBN,
                Description = book.Description ?? "",
                Abstract = book.Abstract ?? "",
                ImageBase64 = book.ImageBase64 ?? "",
                PublicationDate = book.PublicationDate, // Use correct property name
                AuthorId = book.AuthorId,
                AuthorName = book.Author?.Name ?? "",
                CategoryId = book.CategoryId,
                CategoryName = book.Category?.Name ?? "",
                PublisherId = book.PublisherId,
                PublisherName = book.Publisher?.Name ?? "",
                UserId = book.UserId,
                UserName = book.User?.UserName ?? "",
                Tags = book.BookTags?.Select(bt => bt.Tag.ToDto()).ToList() ?? new List<TagDto>()
            };
        }

        public static Book ToEntity(this BookCreateDto dto)
        {
            return new Book
            {
                Title = dto.Title,
                ISBN = dto.ISBN,
                Description = dto.Description,
                Abstract = dto.Abstract,
                ImageBase64 = dto.ImageBase64,
                PublicationDate = dto.PublicationDate, // Use correct property name
                AuthorId = dto.AuthorId,
                CategoryId = dto.CategoryId,
                PublisherId = dto.PublisherId,
                UserId = dto.UserId
            };
        }

        public static void UpdateEntity(this BookUpdateDto dto, Book entity)
        {
            entity.Title = dto.Title;
            entity.ISBN = dto.ISBN;
            entity.Description = dto.Description;
            entity.Abstract = dto.Abstract;
            entity.ImageBase64 = dto.ImageBase64;
            entity.PublicationDate = dto.PublicationDate; // Use correct property name
            entity.AuthorId = dto.AuthorId;
            entity.CategoryId = dto.CategoryId;
            entity.PublisherId = dto.PublisherId;
            entity.UserId = dto.UserId;
        }

        // BookComment Mapping
        public static BookCommentDto ToDto(this BookComment comment)
        {
            return new BookCommentDto
            {
                BookCommentId = comment.BookCommentId,
                BookId = comment.BookId,
                UserId = comment.UserId,
                UserName = comment.User?.UserName ?? "",
                Role = (int)(comment.User?.Role ?? Models.Role.User),
                RoleName = (comment.User?.Role ?? Models.Role.User).ToString(),
                ParentCommentId = comment.ParentCommentId,
                Comment = comment.Comment,
                Star = comment.Star,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                ReplyCount = comment.Replies?.Count ?? 0,
                TotalRepliesCount = comment.Replies?.Count ?? 0, // Will be calculated separately for nested
                Replies = comment.Replies?.Select(r => r.ToDto()).ToList() ?? new List<BookCommentDto>()
            };
        }

        public static BookComment ToEntity(this BookCommentCreateDto dto)
        {
            return new BookComment
            {
                BookId = dto.BookId,
                UserId = dto.UserId,
                ParentCommentId = dto.ParentCommentId,
                Comment = dto.Comment,
                Star = dto.Star,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static void UpdateEntity(this BookCommentUpdateDto dto, BookComment entity)
        {
            entity.Comment = dto.Comment;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}