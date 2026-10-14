using BookInfoFinder.Data;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace BookInfoFinder.Services;

public static class ImageHelper
{
    public const int MaxCoverBytes = 5 * 1024 * 1024;

    public static async Task<string> ToDataUrlAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0 || file.Length > MaxCoverBytes)
            throw new ArgumentException("Chọn ảnh bìa có dung lượng tối đa 5 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".gif" or ".webp"))
            throw new ArgumentException("Chỉ nhận ảnh JPG, PNG, GIF hoặc WebP.");
        await using var input = new MemoryStream();
        await file.CopyToAsync(input, cancellationToken);
        input.Position = 0;
        try
        {
            var options = new DecoderOptions { MaxFrames = 1, SkipMetadata = true };
            var info = await Image.IdentifyAsync(options, input, cancellationToken);
            if (info is null || info.Width <= 0 || info.Height <= 0 || info.Width > 8192 || info.Height > 8192 || (long)info.Width * info.Height > 20000000)
                throw new ArgumentException("Ảnh quá lớn. Chọn ảnh dưới 20 megapixel và mỗi chiều không quá 8192 px.");
            if (info.Metadata.DecodedImageFormat?.Name.ToUpperInvariant() is not ("JPEG" or "PNG" or "GIF" or "WEBP"))
                throw new ArgumentException("Nội dung file phải là ảnh JPG, PNG, GIF hoặc WebP.");
            input.Position = 0;
            using var image = await Image.LoadAsync(new DecoderOptions { MaxFrames = 1 }, input, cancellationToken);
            image.Mutate(operation => operation.AutoOrient().Resize(new ResizeOptions { Size = new Size(1600, 1600), Mode = ResizeMode.Max }));
            await using var output = new MemoryStream();
            await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = 85, SkipMetadata = true }, cancellationToken);
            return "data:image/webp;base64," + Convert.ToBase64String(output.ToArray());
        }
        catch (Exception error) when (error is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new ArgumentException("Không đọc được ảnh bìa. Chọn một ảnh khác.", error);
        }
    }

    // Rescue legacy file references while those files are still present; never erase missing covers.
    public static async Task PersistLegacyCoversAsync(BookContext db, string webRoot, ILogger logger)
    {
        var books = await db.Books.Where(book => book.ImageBase64 != null && book.ImageBase64.StartsWith("/images/")).ToListAsync();
        foreach (var book in books)
        {
            var name = book.ImageBase64!["/images/".Length..];
            if (name != Path.GetFileName(name) || name.Contains('\\') || Path.GetExtension(name).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png" or ".gif" or ".webp")) continue;
            var path = Path.Combine(webRoot, "images", name);
            if (!File.Exists(path)) { logger.LogWarning("Legacy cover file missing for book {BookId}; upload it again", book.BookId); continue; }
            try
            {
                await using var stream = File.OpenRead(path);
                var file = new FormFile(stream, 0, stream.Length, "ImageFile", name);
                book.ImageBase64 = await ToDataUrlAsync(file);
            }
            catch (ArgumentException) { logger.LogWarning("Legacy cover could not be imported for book {BookId}", book.BookId); }
        }
        await db.SaveChangesAsync();
    }
}
