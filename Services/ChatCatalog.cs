using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BookInfoFinder.Data;
using Microsoft.EntityFrameworkCore;

namespace BookInfoFinder.Services;

// Only public library information is exposed to the model; never account or audit tables.
public sealed class ChatCatalog(BookContext db)
{
    public sealed class BookFacts
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Isbn { get; set; } = "";
        public string Author { get; set; } = "";
        public string? AuthorBiography { get; set; }
        public string AuthorNationality { get; set; } = "";
        public string Category { get; set; } = "";
        public string CategoryDescription { get; set; } = "";
        public string Publisher { get; set; } = "";
        public int Year { get; set; }
        public DateTime PublicationDate { get; set; }
        public string? Description { get; set; }
        public string? Abstract { get; set; }
        public string[] Tags { get; set; } = [];
        public double AverageRating { get; set; }
        public int RatingCount { get; set; }
        public int Favorites { get; set; }
        public string[] Reviews { get; set; } = [];
        public string Url => $"/BookDetail/{Id}";
    }

    public sealed record Context(string Prompt, string? DirectAnswer, string? SearchFallback);

    private static string Normalize(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) builder.Append(character);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static readonly HashSet<string> StopWords = new("tim sach quyen cuon nao nhat hay tot moi hot toi minh cho goi de xuat co khong duoc yeu thich danh gia cao di voi trong thu vien cac cua va the loai tac ve nhieu nguoi pho bien thinh hanh xu huong la mot bao tong so nxb nha xuat ban dang ay vay do nhe oi thi biet muon doc xin giup cung cx".Split(' '));

    public async Task<Context> BuildAsync(string question)
    {
        var books = await db.Books.AsNoTracking().AsSplitQuery().Select(book => new BookFacts
        {
            Id = book.BookId,
            Title = book.Title,
            Isbn = book.ISBN,
            Author = book.Author == null ? "" : book.Author.Name,
            AuthorBiography = book.Author == null ? null : book.Author.Biography,
            AuthorNationality = book.Author == null ? "" : book.Author.Nationality,
            Category = book.Category == null ? "" : book.Category.Name,
            CategoryDescription = book.Category == null ? "" : book.Category.Description,
            Publisher = book.Publisher == null ? "" : book.Publisher.Name,
            Year = book.PublicationDate.Year,
            PublicationDate = book.PublicationDate,
            Description = book.Description,
            Abstract = book.Abstract,
            Tags = book.BookTags.Select(tag => tag.Tag.Name).ToArray(),
            AverageRating = book.Ratings.Select(rating => (double?)rating.Star).Average() ?? 0,
            RatingCount = book.Ratings.Count(),
            Favorites = book.Favorites.Count(),
            Reviews = book.Ratings.Where(rating => rating.Review != "").OrderByDescending(rating => rating.CreatedAt)
                .Select(rating => rating.Review).Take(3).ToArray()
        }).ToListAsync();
        var authors = await db.Authors.AsNoTracking().Select(author => new { author.Name, author.Biography, author.Nationality, author.DateOfBirth, BookCount = author.Books.Count() }).ToListAsync();
        var categories = await db.Categories.AsNoTracking().Select(category => new { category.Name, category.Description, BookCount = category.Books.Count() }).ToListAsync();
        var publishers = await db.Publishers.AsNoTracking().Select(publisher => new { publisher.Name, publisher.Address, publisher.ContactInfo, BookCount = publisher.Books.Count() }).ToListAsync();
        var tags = await db.Tags.AsNoTracking().Select(tag => new { tag.Name, tag.Description, BookCount = tag.BookTags.Count() }).ToListAsync();

        var normalized = Normalize(question);
        var terms = Regex.Matches(normalized, @"[\p{L}\p{N}]+")
            .Select(match => match.Value).Where(term => term.Length > 1 && !StopWords.Contains(term)).Distinct().ToArray();
        var searchIndex = books.ToDictionary(book => book.Id, book => Normalize(string.Join(" ", book.Title, book.Isbn, book.Author, book.Category, book.Publisher, book.Year, book.Abstract, string.Join(" ", book.Tags))));
        var scores = books.ToDictionary(book => book.Id, book => terms.Count(term => searchIndex[book.Id].Contains(term, StringComparison.Ordinal)));
        int Score(BookFacts book) => scores[book.Id];
        var matches = terms.Length == 0 ? books : books.Where(book => Score(book) > 0).OrderByDescending(Score).ThenBy(book => book.Id).ToList();
        var hot = Regex.IsMatch(normalized, @"\b(hot|thinh hanh|xu huong|yeu thich|pho bien)\b");
        var best = Regex.IsMatch(normalized, @"\b(hay|tot nhat|danh gia cao|diem cao)\b");
        var newest = Regex.IsMatch(normalized, @"\b(moi nhat|moi xuat ban)\b");
        // Questions about explanations or date windows go to the model rather than assuming an all-time ranking.
        var ranking = (hot || best || newest)
            && (!Regex.IsMatch(normalized, @"\b(tac gia|the loai|nha xuat ban)\b") || Regex.IsMatch(normalized, @"\b(sach|quyen|cuon)\b"))
            && !Regex.IsMatch(normalized, @"\b(tai sao|vi sao|tuan|thang|hom nay)\b");
        string Describe(BookFacts book) => $"• {book.Title} — {book.Author}; {book.AverageRating:0.##}/5 ({book.RatingCount} đánh giá), {book.Favorites} lượt yêu thích. {book.Url}";
        string? direct = null;
        if (ranking)
        {
            var rankMatches = terms.Length == 0 ? books : books.Where(book => Score(book) == terms.Length).ToList();
            var ranked = hot ? rankMatches.OrderByDescending(book => book.Favorites).ThenByDescending(book => book.AverageRating).ThenBy(book => book.Id)
                : newest ? rankMatches.OrderByDescending(book => book.PublicationDate).ThenBy(book => book.Id)
                : rankMatches.OrderByDescending(book => book.AverageRating).ThenByDescending(book => book.RatingCount).ThenByDescending(book => book.Favorites).ThenBy(book => book.Id);
            var eligible = hot ? ranked.Where(book => book.Favorites > 0) : best && !newest ? ranked.Where(book => book.RatingCount > 0) : ranked;
            var selected = eligible.Take(5).ToList();
            direct = selected.Count == 0
                ? "Chưa có sách phù hợp với đủ dữ liệu để xếp hạng. Bạn có thể thử một thể loại khác."
                : (hot ? "Sách được yêu thích nhiều nhất trong thư viện (theo tổng lượt lưu):\n"
                    : newest ? "Sách xuất bản mới nhất trong thư viện:\n" : "Sách được đánh giá cao nhất trong thư viện (điểm trung bình, rồi số lượt đánh giá):\n")
                    + string.Join("\n", selected.Select(Describe));
        }
        else if (terms.Length == 0 && Regex.IsMatch(normalized, @"\b(bao nhieu|tong so)\b"))
        {
            direct = normalized.Contains("tac gia") ? $"Thư viện có {authors.Count} tác giả."
                : normalized.Contains("the loai") ? $"Thư viện có {categories.Count} thể loại."
                : normalized.Contains("nha xuat ban") ? $"Thư viện có {publishers.Count} nhà xuất bản."
                : normalized.Contains("sach") ? $"Thư viện có {books.Count} cuốn sách." : null;
        }
        else if (Regex.IsMatch(normalized.Trim(), @"^(xin chao|chao|hi|hello)[!. ]*$"))
        {
            direct = $"Chào bạn! Thư viện có {books.Count} cuốn sách. Bạn có thể hỏi về sách, tác giả, thể loại hoặc nhờ mình tìm sách hay nhất, sách hot nhé.";
        }
        IEnumerable<T> Relevant<T>(IEnumerable<T> rows) => rows.OrderByDescending(row => terms.Count(term => Normalize(JsonSerializer.Serialize(row)).Contains(term, StringComparison.Ordinal))).Take(100);
        var selectedFacts = matches.Concat(books.OrderByDescending(book => book.AverageRating).ThenByDescending(book => book.Favorites)).DistinctBy(book => book.Id).Take(30).ToList();
        var prompt = JsonSerializer.Serialize(new
        {
            totalBooks = books.Count,
            totalAuthors = authors.Count,
            totalCategories = categories.Count,
            totalPublishers = publishers.Count,
            totalTags = tags.Count,
            books = selectedFacts,
            authors = Relevant(authors), categories = Relevant(categories), publishers = Relevant(publishers), tags = Relevant(tags),
            catalogIsPartial = selectedFacts.Count < books.Count,
            referenceListsMayBePartial = authors.Count > 100 || categories.Count > 100 || publishers.Count > 100 || tags.Count > 100
        });
        var fallback = terms.Length > 0 && matches.Count > 0
            ? "AI đang tạm gián đoạn. Dữ liệu thư viện tìm được cho câu hỏi của bạn:\n" + string.Join("\n", matches.Take(5).Select(Describe)) : null;
        return new Context(prompt, direct, fallback);
    }
}
