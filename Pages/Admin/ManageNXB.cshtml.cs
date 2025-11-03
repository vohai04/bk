using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookInfoFinder.Models.Dto;
using BookInfoFinder.Services.Interface;
 
namespace BookInfoFinder.Pages.Admin
{
    public class ManageNXBModel : PageModel
    {
        private readonly IPublisherService _publisherService;

        public ManageNXBModel(IPublisherService publisherService)
        {
            _publisherService = publisherService;
        }

        public List<PublisherDto> Publishers { get; set; } = new();

        [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }

        public async Task OnGetAsync(int page = 1)
        {
            CurrentPage = page < 1 ? 1 : page;
            int pageSize = 10;
            
            var result = await _publisherService.GetPublishersPagedAsync(CurrentPage, pageSize);
            Publishers = result.Publishers;
            TotalCount = result.TotalCount;
            TotalPages = (int)Math.Ceiling((double)TotalCount / pageSize);
        }

        // AJAX
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

                var result = await _publisherService.GetPublishersPagedAsync(page, pageSize, search);

                var publisherResult = result.Publishers.Select(p => new {
                    p.PublisherId,
                    p.Name,
                    p.Address,
                    p.ContactInfo,
                    CreatedAtFormatted = p.CreatedAt.ToString("dd/MM/yyyy"),
                    p.BookCount
                });

                var totalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize);
                return new JsonResult(new { publishers = publisherResult, totalPages, totalCount = result.TotalCount });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxAddAsync([FromForm] string name, [FromForm] string address, [FromForm] string contactInfo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
                    return new JsonResult(new { success = false, message = "Tên nhà xuất bản không hợp lệ." });

                if (await _publisherService.IsPublisherNameExistsAsync(name.Trim()))
                    return new JsonResult(new { success = false, message = "Tên nhà xuất bản đã tồn tại." });

                var publisherCreateDto = new PublisherCreateDto 
                { 
                    Name = name.Trim(), 
                    Address = address?.Trim() ?? string.Empty,
                    ContactInfo = contactInfo?.Trim() ?? string.Empty
                };
                var createdPublisher = await _publisherService.CreatePublisherAsync(publisherCreateDto);
                
                return new JsonResult(new { 
                    success = true, 
                    publisher = new { createdPublisher.PublisherId, createdPublisher.Name, createdPublisher.Address }
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxEditAsync([FromForm] int publisherId, [FromForm] string name, [FromForm] string address, [FromForm] string contactInfo)
        {
            try
            {
                var existingPublisher = await _publisherService.GetPublisherByIdAsync(publisherId);
                if (existingPublisher == null) 
                    return new JsonResult(new { success = false, message = "Không tìm thấy nhà xuất bản." });
                
                if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
                    return new JsonResult(new { success = false, message = "Tên nhà xuất bản không hợp lệ." });

                // Check if name exists for other publishers
                var nameExists = await _publisherService.IsPublisherNameExistsAsync(name.Trim());
                if (nameExists && !existingPublisher.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return new JsonResult(new { success = false, message = "Tên nhà xuất bản đã tồn tại." });

                var publisherUpdateDto = new PublisherUpdateDto 
                { 
                    PublisherId = publisherId,
                    Name = name.Trim(),
                    Address = address?.Trim() ?? string.Empty,
                    ContactInfo = contactInfo?.Trim() ?? string.Empty
                };
                var updatedPublisher = await _publisherService.UpdatePublisherAsync(publisherUpdateDto);
                
                return new JsonResult(new { 
                    success = true,
                    publisher = new { updatedPublisher.PublisherId, updatedPublisher.Name, updatedPublisher.Address }
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> OnPostAjaxDeleteAsync([FromForm] int publisherId)
        {
            try
            {
                var success = await _publisherService.DeletePublisherAsync(publisherId);
                return new JsonResult(new { success });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}