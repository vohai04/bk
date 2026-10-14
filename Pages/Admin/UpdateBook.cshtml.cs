using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;

namespace BookInfoFinder.Pages.Admin
{
    public class UpdateBookModel : PageModel
    {
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly ICategoryService _categoryService;
        private readonly IPublisherService _publisherService;
        private readonly ITagService _tagService;
        private readonly IBookTagService _bookTagService;

        public UpdateBookModel(
            IBookService bookService,
            IAuthorService authorService,
            ICategoryService categoryService,
            IPublisherService publisherService,
            ITagService tagService,
            IBookTagService bookTagService)
        {
            _bookService = bookService;
            _authorService = authorService;
            _categoryService = categoryService;
            _publisherService = publisherService;
            _tagService = tagService;
            _bookTagService = bookTagService;
        }

        public List<AuthorDto> Authors { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<PublisherDto> Publishers { get; set; } = new();
        public List<TagDto> Tags { get; set; } = new();

        [BindProperty] public BookUpdateDto Book { get; set; } = new();
        [BindProperty] public List<int> SelectedTagIds { get; set; } = new();
        [BindProperty] public IFormFile? ImageFile { get; set; }

        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [BindProperty(SupportsGet = true)] public int? Category { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                await LoadDataAsync();

                var bookDetail = await _bookService.GetBookByIdAsync(id);
                if (bookDetail == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sách!";
                    return RedirectToPage("/Admin/ManageBook");
                }

                // Map BookDetailDto to BookUpdateDto
                Book = new BookUpdateDto
                {
                    BookId = bookDetail.BookId,
                    Title = bookDetail.Title,
                    ISBN = bookDetail.ISBN,
                    Description = bookDetail.Description,
                    Abstract = bookDetail.Abstract,
                    ImageBase64 = bookDetail.ImageBase64,
                    PublicationDate = bookDetail.PublicationDate,
                    AuthorId = bookDetail.AuthorId,
                    CategoryId = bookDetail.CategoryId,
                    PublisherId = bookDetail.PublisherId,
                    UserId = bookDetail.UserId
                };

                // Get selected tags
                var tags = await _bookTagService.GetTagsByBookIdAsync(id);
                SelectedTagIds = tags.Select(t => t.TagId).ToList();
                Book.TagIds = SelectedTagIds;

                return Page();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi khi tải dữ liệu: {ex.Message}";
                return RedirectToPage("/Admin/ManageBook");
            }
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            try
            {
                if (Book.PublicationDate == default) ModelState.AddModelError("Book.PublicationDate", "Chọn ngày xuất bản.");
                if (!ModelState.IsValid)
                {
                    await LoadDataAsync();
                    return Page();
                }

                // Get current user ID from session
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                {
                    TempData["ErrorMessage"] = "Bạn cần đăng nhập để cập nhật sách.";
                    return RedirectToPage("/Account/Login");
                }

                if (Book.BookId != id) return BadRequest();
                var existingBook = await _bookService.GetBookByIdAsync(id);
                if (existingBook is null) return NotFound();
                Book.UserId = existingBook.UserId;
                if (ImageFile is null) Book.ImageBase64 = existingBook.ImageBase64;

                // Handle image upload
                if (ImageFile != null)
                {
                    var imagePath = await EncodeCoverAsync(ImageFile);
                    if (imagePath == null)
                    {
                        await LoadDataAsync();
                        ModelState.AddModelError("ImageFile", "Lỗi khi upload ảnh. Vui lòng thử lại.");
                        return Page();
                    }
                    Book.ImageBase64 = imagePath;
                }

                // Set update properties
                // Preserve the original owner; the audit entry records the editor.
                Book.TagIds = SelectedTagIds;

                // Ensure publication date is UTC
                if (Book.PublicationDate != default && Book.PublicationDate.Kind != DateTimeKind.Utc)
                {
                    Book.PublicationDate = DateTime.SpecifyKind(Book.PublicationDate, DateTimeKind.Utc);
                }

                // Update book
                var updatedBook = await _bookService.UpdateBookAsync(Book);
                if (updatedBook == null)
                {
                    await LoadDataAsync();
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật sách.";
                    return Page();
                }

                TempData["SuccessMessage"] = "Cập nhật sách thành công!";
                return RedirectToPage("/Admin/ManageBook", new { search = Search, category = Category });
            }
            catch (Exception ex)
            {
                await LoadDataAsync();
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
                return Page();
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                Authors = await _authorService.GetAllAuthorsAsync();
                Categories = await _categoryService.GetAllCategoriesAsync();
                Publishers = await _publisherService.GetAllPublishersAsync();
                Tags = await _tagService.GetAllTagsAsync();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi khi tải dữ liệu: {ex.Message}";
            }
        }

        private async Task<string?> EncodeCoverAsync(IFormFile imageFile)
        {
            try { return await Services.ImageHelper.ToDataUrlAsync(imageFile, HttpContext.RequestAborted); }
            catch (ArgumentException error)
            {
                ModelState.AddModelError("ImageFile", error.Message);
                return null;
            }
        }
    }
}
