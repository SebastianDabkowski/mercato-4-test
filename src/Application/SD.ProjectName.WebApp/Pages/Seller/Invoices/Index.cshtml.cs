using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller.Invoices;

[Authorize(Roles = IdentityRoles.Seller)]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CommissionInvoiceService _invoiceService;
    private const int PageSize = 10;

    public IndexModel(UserManager<ApplicationUser> userManager, CommissionInvoiceService invoiceService)
    {
        _userManager = userManager;
        _invoiceService = invoiceService;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? PeriodFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? PeriodTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<string> Statuses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public bool IncludeCreditNotes { get; set; } = true;

    [BindProperty(SupportsGet = true)]
    public new int Page { get; set; } = 1;

    public List<CommissionInvoice> Invoices { get; private set; } = new();
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }

    public IReadOnlyCollection<string> StatusOptions { get; } = new[]
    {
        CommissionInvoiceStatus.Issued,
        CommissionInvoiceStatus.Paid,
        CommissionInvoiceStatus.Cancelled
    };

    public bool HasFilters => PeriodFrom.HasValue || PeriodTo.HasValue || (Statuses?.Count ?? 0) > 0 || !IncludeCreditNotes;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public async Task<IActionResult> OnGetAsync()
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

        NormalizeFilters();

        var sellerName = ResolveSellerName(user);
        await _invoiceService.EnsurePreviousMonthInvoiceAsync(user.Id, sellerName);

        var result = await _invoiceService.GetInvoicesAsync(user.Id, BuildQuery());

        Invoices = result.Invoices;
        TotalCount = result.TotalCount;
        TotalPages = result.TotalCount == 0 ? 1 : (int)Math.Ceiling(result.TotalCount / (double)result.PageSize);
        Page = result.Page;

        return Page();
    }

    public string StatusLabel(string status) => status switch
    {
        CommissionInvoiceStatus.Paid => "Paid",
        CommissionInvoiceStatus.Cancelled => "Cancelled",
        _ => "Issued"
    };

    public string InvoiceType(CommissionInvoice invoice) => invoice.IsCreditNote ? "Credit note" : "Invoice";

    public string BuildPageUrl(int pageNumber)
    {
        var routeValues = new
        {
            Page = pageNumber,
            PeriodFrom = PeriodFrom?.ToString("yyyy-MM-dd"),
            PeriodTo = PeriodTo?.ToString("yyyy-MM-dd"),
            Statuses = Statuses.ToArray(),
            IncludeCreditNotes
        };

        return Url.Page("/Seller/Invoices/Index", routeValues) ?? string.Empty;
    }

    private CommissionInvoiceQuery BuildQuery()
    {
        var from = PeriodFrom.HasValue
            ? DateTime.SpecifyKind(PeriodFrom.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var to = PeriodTo.HasValue
            ? DateTime.SpecifyKind(PeriodTo.Value, DateTimeKind.Utc).Date.AddDays(1).AddTicks(-1)
            : (DateTime?)null;

        return new CommissionInvoiceQuery
        {
            PeriodFrom = from.HasValue ? new DateTimeOffset(from.Value) : null,
            PeriodTo = to.HasValue ? new DateTimeOffset(to.Value) : null,
            Statuses = Statuses,
            IncludeCreditNotes = IncludeCreditNotes,
            Page = Page,
            PageSize = PageSize
        };
    }

    private void NormalizeFilters()
    {
        Statuses ??= new List<string>();
        Statuses = Statuses
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .ToList();
        Page = Page < 1 ? 1 : Page;
    }

    private static string ResolveSellerName(ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.StoreName))
        {
            return user.StoreName;
        }

        if (!string.IsNullOrWhiteSpace(user.CompanyName))
        {
            return user.CompanyName;
        }

        return $"{user.FirstName} {user.LastName}".Trim();
    }
}
