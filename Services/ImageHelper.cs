using Microsoft.AspNetCore.Http;
using System.IO;
 
namespace BookInfoFinder.Services
{
    public static class ImageHelper
    {
        // Lưu IFormFile vào thư mục images và trả về đường dẫn tương đối
        public static string? SaveFileToImages(IFormFile? imageFile, string wwwRootPath)
        {
            if (imageFile == null || imageFile.Length == 0)
                return null;
 
            var ext = Path.GetExtension(imageFile.FileName);
            var fileName = Guid.NewGuid().ToString("N") + ext;
            var folder = Path.Combine(wwwRootPath, "images");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
 
            var filePath = Path.Combine(folder, fileName);
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                imageFile.CopyTo(fs);
            }
 
            // Trả về đường dẫn tương đối để lưu vào DB
            return "/images/" + fileName;
        }
    }
}