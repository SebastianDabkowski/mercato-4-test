using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller.Products
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class ImportModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ImportProductCatalog _importer;

        public ImportModel(UserManager<ApplicationUser> userManager, ImportProductCatalog importer)
        {
            _userManager = userManager;
            _importer = importer;
        }

        [BindProperty]
        public IFormFile? Upload { get; set; }

        [BindProperty]
        public string? PreviewFileContent { get; set; }

        [BindProperty]
        public string? PreviewFileName { get; set; }

        public ImportProductCatalog.ImportPreviewResult? Preview { get; private set; }

        public IReadOnlyList<ProductImportJob> History { get; private set; } = Array.Empty<ProductImportJob>();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return;
            }

            await LoadHistoryAsync(user.Id);
        }

        public async Task<IActionResult> OnPostPreviewAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (Upload is null || Upload.Length == 0)
            {
                ModelState.AddModelError(nameof(Upload), "Upload a CSV/XLS file that follows the template.");
                await LoadHistoryAsync(user.Id);
                return Page();
            }

            using var memory = new MemoryStream();
            await Upload.CopyToAsync(memory);
            var bytes = memory.ToArray();
            PreviewFileContent = Convert.ToBase64String(bytes);
            PreviewFileName = Upload.FileName;

            memory.Position = 0;
            Preview = await _importer.PreviewAsync(memory, Upload.FileName, user.Id);
            await LoadHistoryAsync(user.Id);
            return Page();
        }

        public async Task<IActionResult> OnPostImportAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(PreviewFileContent) || string.IsNullOrWhiteSpace(PreviewFileName))
            {
                ModelState.AddModelError(string.Empty, "Re-run the preview to confirm the file content.");
                await LoadHistoryAsync(user.Id);
                return Page();
            }

            var bytes = Convert.FromBase64String(PreviewFileContent);
            using var memory = new MemoryStream(bytes);

            var job = await _importer.ImportAsync(memory, PreviewFileName, user.Id);
            StatusMessage = job.Status == ProductImportStatuses.Completed
                ? $"Import finished. Created {job.CreatedCount}, updated {job.UpdatedCount}, failed {job.FailedCount}."
                : "Import failed. See error report.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetErrorReportAsync(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var job = await _importer.GetJobAsync(id, user.Id);
            if (job is null || string.IsNullOrWhiteSpace(job.ErrorReport))
            {
                return NotFound();
            }

            var fileName = $"import-errors-{job.Id}.txt";
            var bytes = Encoding.UTF8.GetBytes(job.ErrorReport);
            return File(bytes, "text/plain", fileName);
        }

        private async Task LoadHistoryAsync(string sellerId)
        {
            History = await _importer.GetHistoryAsync(sellerId);
        }
    }
}
