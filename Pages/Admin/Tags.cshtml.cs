using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
 
namespace BookInfoFinder.Pages.Admin
{
    public class TagsModel : PageModel
    {
        private readonly ITagService _tagService;
        
        public TagsModel(ITagService tagService) => _tagService = tagService;

        public List<TagDto> Tags { get; set; } = new();
        [BindProperty(SupportsGet = true)] public int? EditTagId { get; set; }
        [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }

        public async Task OnGetAsync(int? edit, int page = 1)
        {
            CurrentPage = page < 1 ? 1 : page;
            int pageSize = 10;
            
            var result = await _tagService.GetTagsPagedAsync(CurrentPage, pageSize);
            Tags = result.Tags;
            TotalCount = result.TotalCount;
            TotalPages = (int)Math.Ceiling((double)TotalCount / pageSize);
            EditTagId = edit;
        }

        // AJAX handler cho filter + phân trang
        public async Task<JsonResult> OnGetAjaxSearchAsync()
        {
            try
            {
                var query = Request.Query;
                string search = query["search"].ToString() ?? "";
                int.TryParse(query["page"], out int page);
                int.TryParse(query["pageSize"], out int pageSize);

                page = page <= 0 ? 1 : page;
                pageSize = pageSize <= 0 ? 10 : pageSize;

                var result = await _tagService.GetTagsPagedAsync(page, pageSize, search);

                var tagResult = result.Tags.Select(t => new
                {
                    t.TagId,
                    t.Name,
                    t.Description,
                    t.BookCount,
                    CreatedAtFormatted = t.CreatedAt.ToString("dd/MM/yyyy")
                });

                var totalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize);
                return new JsonResult(new { tags = tagResult, totalPages, totalCount = result.TotalCount });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
        public async Task<JsonResult> OnPostAjaxAddAsync([FromForm] string name, [FromForm] string description)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) || name.Length > 30)
                    return new JsonResult(new { success = false, message = "Tên tag không hợp lệ." });

                if (await _tagService.IsTagNameExistsAsync(name.Trim()))
                    return new JsonResult(new { success = false, message = "Tên tag đã tồn tại." });

                var tagCreateDto = new TagCreateDto 
                { 
                    Name = name.Trim(),
                    Description = description?.Trim() ?? string.Empty
                };
                
                var createdTag = await _tagService.CreateTagAsync(tagCreateDto);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "Đã xảy ra lỗi: " + ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxEditAsync([FromForm] int tagId, [FromForm] string name, [FromForm] string description)
        {
            try
            {
                var existingTag = await _tagService.GetTagByIdAsync(tagId);
                if (existingTag == null) 
                    return new JsonResult(new { success = false, message = "Không tìm thấy tag." });
                
                if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
                    return new JsonResult(new { success = false, message = "Tên tag không hợp lệ." });

                // Check if name exists for other tags
                var nameExists = await _tagService.IsTagNameExistsAsync(name.Trim());
                if (nameExists && !existingTag.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return new JsonResult(new { success = false, message = "Tên tag đã tồn tại." });

                var tagUpdateDto = new TagUpdateDto 
                { 
                    TagId = tagId,
                    Name = name.Trim(),
                    Description = description?.Trim() ?? string.Empty
                };
                var updatedTag = await _tagService.UpdateTagAsync(tagUpdateDto);
                
                return new JsonResult(new { 
                    success = true,
                    tag = new { updatedTag.TagId, updatedTag.Name }
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxDeleteAsync([FromForm] int tagId)
        {
            try
            {
                var success = await _tagService.DeleteTagAsync(tagId);
                return new JsonResult(new { success });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}