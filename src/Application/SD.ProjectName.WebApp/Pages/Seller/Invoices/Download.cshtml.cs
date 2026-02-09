using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Seller.Invoices;

[Authorize(Roles = IdentityRoles.Seller)]
public class DownloadModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CommissionInvoiceService _invoiceService;
    private readonly ICommissionInvoicePdfGenerator _pdfGenerator;

    public DownloadModel(
        UserManager<ApplicationUser> userManager,
        CommissionInvoiceService invoiceService,
        ICommissionInvoicePdfGenerator pdfGenerator)
    {
        _userManager = userManager;
        _invoiceService = invoiceService;
        _pdfGenerator = pdfGenerator;
    }

    public async Task<IActionResult> OnGetAsync(int invoiceId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        if (user.AccountType != AccountType.Seller)
        {
            return Forbid();
        }

        var invoice = await _invoiceService.GetInvoiceAsync(invoiceId, user.Id);
        if (invoice is null)
        {
            return NotFound();
        }

        var content = _pdfGenerator.Generate(invoice);
        var fileName = $"{invoice.Number}.pdf";
        return File(content, "application/pdf", fileName);
    }
}
