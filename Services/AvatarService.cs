using BookInfoFinder.Data;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Models.Entity;
using BookInfoFinder.Services.Interface;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace BookInfoFinder.Services;

public sealed class AvatarService(BookContext context) : IAvatarService
{
    public const int MaxFileBytes = 2 * 1024 * 1024;

    private static async Task<byte[]> NormalizeAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > MaxFileBytes)
            throw new ArgumentException("Chọn ảnh có dung lượng tối đa 2 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new ArgumentException("Chỉ nhận ảnh JPG, PNG hoặc WebP.");
        await using var input = new MemoryStream();
        await file.CopyToAsync(input, cancellationToken);
        input.Position = 0;
        try
        {
            var info = await Image.IdentifyAsync(new DecoderOptions { MaxFrames = 1, SkipMetadata = true }, input, cancellationToken);
            if (info is null || info.Width <= 0 || info.Height <= 0 || info.Width > 8192 || info.Height > 8192 || (long)info.Width * info.Height > 20000000)
                throw new ArgumentException("Ảnh quá lớn. Chọn ảnh dưới 20 megapixel và mỗi chiều không quá 8192 px.");
            if (info.Metadata.DecodedImageFormat?.Name.ToUpperInvariant() is not ("JPEG" or "PNG" or "WEBP"))
                throw new ArgumentException("Nội dung file phải là ảnh JPG, PNG hoặc WebP.");
            input.Position = 0;
            using var image = await Image.LoadAsync(new DecoderOptions { MaxFrames = 1, TargetSize = new Size(512, 512) }, input, cancellationToken);
            image.Mutate(operation => operation.AutoOrient().Resize(new ResizeOptions { Size = new Size(256, 256), Mode = ResizeMode.Crop }));
            await using var output = new MemoryStream();
            await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = 85, SkipMetadata = true }, cancellationToken);
            return output.ToArray();
        }
        catch (Exception error) when (error is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new ArgumentException("Không đọc được ảnh. Chọn một ảnh JPG, PNG hoặc WebP khác.", error);
        }
    }

    // Serialize changes per user, including concurrent upload/delete from separate tabs.
    private Task<User?> LockUserAsync(int userId, CancellationToken cancellationToken) => context.Users
        .FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"UserId\" = {userId} FOR UPDATE")
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<AvatarUpdateDto> UploadAsync(int userId, IFormFile file, CancellationToken cancellationToken)
    {
        var bytes = await NormalizeAsync(file, cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(userId, cancellationToken);
        if (user is null || user.Status != 1) throw new ArgumentException("Tài khoản không còn hoạt động.");
        var avatar = await context.UserAvatars.FindAsync([userId], cancellationToken);
        if (avatar is null) { avatar = new UserAvatar { UserId = userId }; context.UserAvatars.Add(avatar); }
        avatar.Image = bytes;
        avatar.Version = Guid.NewGuid().ToString("N");
        user.AvatarVersion = avatar.Version;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(userId, AvatarUrls.For(userId, avatar.Version), user.UpdatedAt.Value);
    }
    public async Task<AvatarUpdateDto> DeleteAsync(int userId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(userId, cancellationToken);
        if (user is null || user.Status != 1) throw new ArgumentException("Tài khoản không còn hoạt động.");
        var avatar = await context.UserAvatars.FindAsync([userId], cancellationToken);
        if (avatar != null) context.UserAvatars.Remove(avatar);
        user.AvatarVersion = null;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(userId, null, user.UpdatedAt.Value);
    }
    public async Task<AvatarImageDto?> GetAsync(int userId, CancellationToken cancellationToken) => await context.UserAvatars
        .AsNoTracking().Where(a => a.UserId == userId).Select(a => new AvatarImageDto(a.Image, a.Version)).SingleOrDefaultAsync(cancellationToken);
}
