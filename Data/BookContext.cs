using System;
using System.Linq;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Models;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Data
{
    public class BookContext : DbContext
    {
        public BookContext() { }
        public BookContext(DbContextOptions<BookContext> options) : base(options) { }

    public DbSet<Book> Books { get; set; }
    public DbSet<Author> Authors { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Publisher> Publishers { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<BookTag> BookTags { get; set; }
    public DbSet<SearchHistory> SearchHistories { get; set; }
    public DbSet<Rating> Ratings { get; set; }
    public DbSet<BookComment> BookComments { get; set; }
    public DbSet<Chatbot> Chatbots { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                optionsBuilder.UseNpgsql(connectionString);
#if DEBUG
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.LogTo(Console.WriteLine);
#endif
            }
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Book>(static entity =>
 {
     entity.ToTable("Books");
     entity.HasKey(static b => b.BookId);
     entity.Property(static b => b.BookId).ValueGeneratedOnAdd();

     entity.Property(static b => b.Title)
         .IsRequired()
         .HasMaxLength(100);

     entity.Property(static b => b.ISBN)
         .IsRequired()
         .HasMaxLength(13);

     entity.Property(static b => b.PublicationDate)
         .IsRequired();

     entity.Property(static b => b.Description)
         .IsRequired()
         .HasMaxLength(50);

     entity.Property(static b => b.Abstract)
         .IsRequired()
         .HasMaxLength(200);

     // ✅ Sửa lại để lưu ảnh dưới dạng chuỗi base64
     entity.Property(static b => b.ImageBase64)
         .HasColumnType("text"); // hoặc "varchar" nếu bạn muốn giới hạn độ dài

     entity.HasOne(static b => b.Author)
         .WithMany(static a => a.Books)
         .HasForeignKey(static b => b.AuthorId)
         .IsRequired();

     entity.HasOne(static b => b.Category)
         .WithMany(static c => c.Books)
         .HasForeignKey(static b => b.CategoryId)
         .IsRequired();

     entity.HasOne(static b => b.Publisher)
         .WithMany(static p => p.Books)
         .HasForeignKey(static b => b.PublisherId)
         .IsRequired();

     entity.HasOne(static b => b.User)
         .WithMany(static u => u.Books)
         .HasForeignKey(static b => b.UserId)
         .IsRequired(false);

     entity.HasMany(static b => b.BookTags)
         .WithOne(static bt => bt.Book)
         .HasForeignKey(static bt => bt.BookId)
         .IsRequired();

     entity.HasMany(static b => b.SearchHistories)
         .WithOne(static sh => sh.Book)
         .HasForeignKey(static sh => sh.BookId)
         .IsRequired(false);

 });



            modelBuilder.Entity<User>(static entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(static u => u.UserId);
                entity.Property(static u => u.UserId).ValueGeneratedOnAdd();
                entity.Property(static u => u.FullName).IsRequired().HasMaxLength(100);
                entity.Property(static u => u.UserName).IsRequired().HasMaxLength(256);
                entity.Property(static u => u.Password).IsRequired().HasMaxLength(100);
                entity.Property(static u => u.Email).IsRequired().HasMaxLength(256);
                
                // Add unique constraints
                entity.HasIndex(static u => u.UserName).IsUnique();
                entity.HasIndex(static u => u.Email).IsUnique();
                entity.Property(static u => u.CreatedAt).IsRequired();
                entity.Property(static u => u.UpdatedAt);
                entity.Property(static u => u.Role).IsRequired().HasConversion<int>();

                // Bổ sung cấu hình cho Status
                entity.Property(static u => u.Status)
                    .IsRequired()
                    .HasDefaultValue(1); // 1 là hoạt động, 0 là off

                entity.HasMany(static u => u.Books)
                    .WithOne(static b => b.User)
                    .HasForeignKey(static b => b.UserId)
                    .IsRequired(false);

                // Quan hệ: User → Favorites
                entity.HasMany(static u => u.Favorites)
                    .WithOne(static f => f.User)
                    .HasForeignKey(static f => f.UserId)
                    .IsRequired();

                // Quan hệ: User → SearchHistories
                entity.HasMany(static u => u.SearchHistories)
                    .WithOne(static s => s.User)
                    .HasForeignKey(static s => s.UserId)
                    .IsRequired();

                // Quan hệ: User → Ratings
                entity.HasMany(static u => u.Ratings)
                    .WithOne(static r => r.User)
                    .HasForeignKey(static r => r.UserId)
                    .IsRequired();

                // Quan hệ: User → BookComments
                entity.HasMany(static u => u.BookComments)
                    .WithOne(static bc => bc.User)
                    .HasForeignKey(static bc => bc.UserId)
                    .IsRequired();
            });
            modelBuilder.Entity<Author>(static entity =>
            {
                entity.ToTable("Authors");
                entity.HasKey(static a => a.AuthorId);
                entity.Property(static a => a.AuthorId).ValueGeneratedOnAdd();
                entity.Property(static a => a.Name).IsRequired().HasMaxLength(100);
                entity.Property(static a => a.Biography).IsRequired().HasMaxLength(256);
                entity.HasMany(static a => a.Books).WithOne(static b => b.Author).HasForeignKey(static b => b.AuthorId);
            });
            modelBuilder.Entity<Category>(static entity =>
{
    entity.ToTable("Categories");
    entity.HasKey(static c => c.CategoryId);
    entity.Property(static c => c.CategoryId).ValueGeneratedOnAdd();
    entity.Property(static c => c.Name).IsRequired().HasMaxLength(50);

    // Quan hệ: Category → Books
    entity.HasMany(static c => c.Books)
        .WithOne(static b => b.Category)
        .HasForeignKey(static b => b.CategoryId)
        .IsRequired(false);
});

            modelBuilder.Entity<Publisher>(static entity =>
            {
                entity.ToTable("Publishers");
                entity.HasKey(static p => p.PublisherId);
                entity.Property(static p => p.PublisherId).ValueGeneratedOnAdd();
                entity.Property(static p => p.Name).IsRequired().HasMaxLength(100);
                entity.Property(static p => p.Address).IsRequired().HasMaxLength(200);
                entity.HasMany(static p => p.Books).WithOne(static b => b.Publisher).HasForeignKey(static b => b.PublisherId).IsRequired();
            });

            modelBuilder.Entity<Favorite>(static entity =>
{
    entity.ToTable("Favorites");
    entity.HasKey(static f => f.FavoriteId);
    entity.Property(static f => f.FavoriteId).ValueGeneratedOnAdd();

    entity.HasOne(static f => f.User)
        .WithMany(static u => u.Favorites)
        .HasForeignKey(static f => f.UserId)
        .IsRequired();

    entity.HasOne(static f => f.Book)
        .WithMany(static b => b.Favorites)
        .HasForeignKey(static f => f.BookId)
        .IsRequired();
});
            modelBuilder.Entity<Tag>(static entity =>
            {
                entity.ToTable("Tags");
                entity.HasKey(static t => t.TagId);
                entity.Property(static t => t.TagId).ValueGeneratedOnAdd();
                entity.Property(static t => t.Name).IsRequired().HasMaxLength(30);
            });

            modelBuilder.Entity<BookTag>(static entity =>
            {
                entity.ToTable("BookTags");
                entity.HasKey(static bt => bt.BookTagId);
                entity.Property(static bt => bt.BookTagId).ValueGeneratedOnAdd();

                entity.HasOne(static bt => bt.Book)
                    .WithMany(static b => b.BookTags)
                    .HasForeignKey(static bt => bt.BookId)
                    .IsRequired();

                entity.HasOne(static bt => bt.Tag)
                    .WithMany(static t => t.BookTags)
                    .HasForeignKey(static bt => bt.TagId)
                    .IsRequired();
            });
            modelBuilder.Entity<SearchHistory>(static entity =>
{
    entity.ToTable("SearchHistories");
    entity.HasKey(static sh => sh.SearchHistoryId);
    entity.Property(static sh => sh.SearchHistoryId).ValueGeneratedOnAdd();
    entity.Property(static sh => sh.Title).HasMaxLength(100);
    entity.Property(static sh => sh.Author).HasMaxLength(100);
    entity.Property(static sh => sh.CategoryName).HasMaxLength(50);

    entity.HasOne(static sh => sh.User)
        .WithMany(static u => u.SearchHistories)
        .HasForeignKey(static sh => sh.UserId)
        .IsRequired();

    entity.HasOne(static sh => sh.Book)
        .WithMany(static b => b.SearchHistories)
        .HasForeignKey(static sh => sh.BookId)
        .IsRequired(false);

});
            modelBuilder.Entity<Rating>(static entity =>
            {
                entity.ToTable("Ratings");
                entity.HasKey(static r => r.RatingId);
                entity.Property(static r => r.RatingId).ValueGeneratedOnAdd();
                entity.Property(static r => r.Star).IsRequired();
                entity.Property(static r => r.CreatedAt).IsRequired();

                entity.HasOne(static r => r.Book)
                    .WithMany(static b => b.Ratings)
                    .HasForeignKey(static r => r.BookId)
                    .IsRequired();

                entity.HasOne(static r => r.User)
                    .WithMany(static u => u.Ratings)
                    .HasForeignKey(static r => r.UserId)
                    .IsRequired();
            });
            modelBuilder.Entity<BookComment>(static entity =>
            {
                entity.ToTable("BookComments");

                // 🔹 Khóa chính
                entity.HasKey(static bc => bc.BookCommentId);

                entity.Property(static bc => bc.BookCommentId)
                  .ValueGeneratedOnAdd();

                // 🔹 Thuộc tính cơ bản
                entity.Property(static bc => bc.Comment)
                  .IsRequired()
                  .HasMaxLength(500);

                entity.Property(static bc => bc.Star)
                  .HasDefaultValue(null);

                entity.Property(static bc => bc.CreatedAt)
                  .IsRequired();

                entity.Property(static bc => bc.UpdatedAt)
                  .HasDefaultValue(null);

                // 🔹 Quan hệ với Book
                entity.HasOne(static bc => bc.Book)
                  .WithMany(static b => b.BookComments)
                  .HasForeignKey(static bc => bc.BookId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .IsRequired();

                // 🔹 Quan hệ với User
                entity.HasOne(static bc => bc.User)
                  .WithMany(static u => u.BookComments)
                  .HasForeignKey(static bc => bc.UserId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .IsRequired();

                // 🔹 Quan hệ đệ quy (Parent - Reply)
                entity.HasOne(static bc => bc.ParentComment)
                  .WithMany(static bc => bc.Replies)
                  .HasForeignKey(static bc => bc.ParentCommentId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .IsRequired(false);
            });

            // Seed Data
            SeedData(modelBuilder);
        }

        private static void SeedData(ModelBuilder modelBuilder)
        {
            // Use static DateTime values with UTC kind for PostgreSQL compatibility
            var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            
            // Seed Roles
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserId = 1,
                    FullName = "Admin User",
                    UserName = "admin",
                    Password = "admin123", // Plain text for testing
                    Email = "haivo3225@gmail.com",
                    CreatedAt = seedDate,
                    Role = Role.Admin,
                    Status = 1
                },
                new User
                {
                    UserId = 2,
                    FullName = "John Doe",
                    UserName = "johndoe",
                    Password = "user123", // Plain text for testing
                    Email = "john@example.com",
                    CreatedAt = seedDate,
                    Role = Role.User,
                    Status = 1
                }
            );

            // Seed Authors
            modelBuilder.Entity<Author>().HasData(
                new Author
                {
                    AuthorId = 1,
                    Name = "J.K. Rowling",
                    Biography = "British author, best known for the Harry Potter series",
                    DateOfBirth = new DateTime(1965, 7, 31, 0, 0, 0, DateTimeKind.Utc),
                    Nationality = "British",
                    CreatedAt = seedDate
                },
                new Author
                {
                    AuthorId = 2,
                    Name = "George Orwell",
                    Biography = "English novelist and essayist, known for 1984 and Animal Farm",
                    DateOfBirth = new DateTime(1903, 6, 25, 0, 0, 0, DateTimeKind.Utc),
                    Nationality = "British",
                    CreatedAt = seedDate
                },
                new Author
                {
                    AuthorId = 3,
                    Name = "Haruki Murakami",
                    Biography = "Japanese writer known for surreal fiction",
                    DateOfBirth = new DateTime(1949, 1, 12, 0, 0, 0, DateTimeKind.Utc),
                    Nationality = "Japanese",
                    CreatedAt = seedDate
                }
            );

            // Seed Categories
            modelBuilder.Entity<Category>().HasData(
                new Category
                {
                    CategoryId = 1,
                    Name = "Fantasy",
                    Description = "Fantasy novels and stories",
                    CreatedAt = seedDate
                },
                new Category
                {
                    CategoryId = 2,
                    Name = "Science Fiction",
                    Description = "Science fiction books",
                    CreatedAt = seedDate
                },
                new Category
                {
                    CategoryId = 3,
                    Name = "Literary Fiction",
                    Description = "Literary and contemporary fiction",
                    CreatedAt = seedDate
                },
                new Category
                {
                    CategoryId = 4,
                    Name = "Dystopian",
                    Description = "Dystopian and political fiction",
                    CreatedAt = seedDate
                }
            );

            // Seed Publishers
            modelBuilder.Entity<Publisher>().HasData(
                new Publisher
                {
                    PublisherId = 1,
                    Name = "Bloomsbury Publishing",
                    Address = "50 Bedford Square, London, UK",
                    ContactInfo = "contact@bloomsbury.com",
                    CreatedAt = seedDate
                },
                new Publisher
                {
                    PublisherId = 2,
                    Name = "Penguin Random House",
                    Address = "1745 Broadway, New York, NY 10019, USA",
                    ContactInfo = "info@penguinrandomhouse.com",
                    CreatedAt = seedDate
                },
                new Publisher
                {
                    PublisherId = 3,
                    Name = "Vintage Books",
                    Address = "1745 Broadway, New York, NY, USA",
                    ContactInfo = "vintage@randomhouse.com",
                    CreatedAt = seedDate
                }
            );

            // Seed Books
            modelBuilder.Entity<Book>().HasData(
                new Book
                {
                    BookId = 1,
                    Title = "Harry Potter and the Philosopher's Stone",
                    ISBN = "9780747532699",
                    AuthorId = 1,
                    CategoryId = 1,
                    PublisherId = 1,
                    UserId = 1,
                    PublicationDate = new DateTime(1997, 6, 26, 0, 0, 0, DateTimeKind.Utc),
                    Description = "A young wizard's journey begins",
                    Abstract = "Harry Potter discovers he is a wizard on his 11th birthday and begins his magical education at Hogwarts School of Witchcraft and Wizardry."
                },
                new Book
                {
                    BookId = 2,
                    Title = "1984",
                    ISBN = "9780451524935",
                    AuthorId = 2,
                    CategoryId = 4,
                    PublisherId = 2,
                    UserId = 1,
                    PublicationDate = new DateTime(1949, 6, 8, 0, 0, 0, DateTimeKind.Utc),
                    Description = "A dystopian social science fiction novel",
                    Abstract = "Winston Smith struggles for freedom in a world where Big Brother is always watching and the Thought Police control minds."
                },
                new Book
                {
                    BookId = 3,
                    Title = "Norwegian Wood",
                    ISBN = "9780375704024",
                    AuthorId = 3,
                    CategoryId = 3,
                    PublisherId = 3,
                    UserId = 1,
                    PublicationDate = new DateTime(1987, 8, 4, 0, 0, 0, DateTimeKind.Utc),
                    Description = "A coming-of-age story set in 1960s Tokyo",
                    Abstract = "Toru Watanabe looks back on his days as a college student living in Tokyo and recalls his relationships and personal growth."
                }
            );

            // Seed Tags
            modelBuilder.Entity<Tag>().HasData(
                new Tag { TagId = 1, Name = "Magic", CreatedAt = seedDate },
                new Tag { TagId = 2, Name = "Wizard", CreatedAt = seedDate },
                new Tag { TagId = 3, Name = "Adventure", CreatedAt = seedDate },
                new Tag { TagId = 4, Name = "Dystopia", CreatedAt = seedDate },
                new Tag { TagId = 5, Name = "Surveillance", CreatedAt = seedDate },
                new Tag { TagId = 6, Name = "Romance", CreatedAt = seedDate },
                new Tag { TagId = 7, Name = "Coming of Age", CreatedAt = seedDate },
                new Tag { TagId = 8, Name = "Japan", CreatedAt = seedDate }
            );

            // Seed BookTags
            modelBuilder.Entity<BookTag>().HasData(
                new BookTag { BookTagId = 1, BookId = 1, TagId = 1 },
                new BookTag { BookTagId = 2, BookId = 1, TagId = 2 },
                new BookTag { BookTagId = 3, BookId = 1, TagId = 3 },
                new BookTag { BookTagId = 4, BookId = 2, TagId = 4 },
                new BookTag { BookTagId = 5, BookId = 2, TagId = 5 },
                new BookTag { BookTagId = 6, BookId = 3, TagId = 6 },
                new BookTag { BookTagId = 7, BookId = 3, TagId = 7 },
                new BookTag { BookTagId = 8, BookId = 3, TagId = 8 }
            );

            // Seed Favorites
            modelBuilder.Entity<Favorite>().HasData(
                new Favorite
                {
                    FavoriteId = 1,
                    UserId = 2,
                    BookId = 1,
                    CreatedAt = seedDate
                },
                new Favorite
                {
                    FavoriteId = 2,
                    UserId = 2,
                    BookId = 3,
                    CreatedAt = seedDate
                }
            );

            // Seed Ratings
            modelBuilder.Entity<Rating>().HasData(
                new Rating
                {
                    RatingId = 1,
                    BookId = 1,
                    UserId = 2,
                    Star = 5,
                    CreatedAt = seedDate
                },
                new Rating
                {
                    RatingId = 2,
                    BookId = 2,
                    UserId = 2,
                    Star = 4,
                    CreatedAt = seedDate
                },
                new Rating
                {
                    RatingId = 3,
                    BookId = 3,
                    UserId = 2,
                    Star = 5,
                    CreatedAt = seedDate
                }
            );

            // Seed SearchHistory
            modelBuilder.Entity<SearchHistory>().HasData(
                new SearchHistory
                {
                    SearchHistoryId = 1,
                    UserId = 2,
                    Title = "Harry Potter",
                    Author = "J.K. Rowling",
                    CategoryName = "Fantasy",
                    BookId = 1,
                    SearchQuery = "Harry Potter fantasy",
                    SearchedAt = seedDate,
                    ResultCount = 1
                },
                new SearchHistory
                {
                    SearchHistoryId = 2,
                    UserId = 2,
                    Title = "1984",
                    Author = "George Orwell",
                    CategoryName = "Dystopian",
                    BookId = 2,
                    SearchQuery = "1984 dystopian",
                    SearchedAt = seedDate,
                    ResultCount = 1
                }
            );

            // Seed BookComments
            modelBuilder.Entity<BookComment>().HasData(
                new BookComment
                {
                    BookCommentId = 1,
                    BookId = 1,
                    UserId = 2,
                    Comment = "Amazing book! The magical world is so well-crafted.",
                    Star = 5,
                    CreatedAt = seedDate
                },
                new BookComment
                {
                    BookCommentId = 2,
                    BookId = 2,
                    UserId = 2,
                    Comment = "A thought-provoking and chilling vision of the future.",
                    Star = 4,
                    CreatedAt = seedDate
                },
                new BookComment
                {
                    BookCommentId = 3,
                    BookId = 3,
                    UserId = 2,
                    Comment = "Beautiful and melancholic. Murakami's writing is exceptional.",
                    Star = 5,
                    CreatedAt = seedDate
                }
            );
        }
    }
}