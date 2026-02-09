using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller.Payouts;

[Authorize(Roles = IdentityRoles.Seller)]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICartRepository _cartRepository;
    private const int PageSize = 10;

    public IndexModel(UserManager<ApplicationUser> userManager, ICartRepository cartRepository)
    {
        _userManager = userManager;
        _cartRepository = cartRepository;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? ScheduledFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ScheduledTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<string> Statuses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public new int Page { get; set; } = 1;

    public List<PayoutSchedule> Payouts { get; private set; } = new();
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }

    public IReadOnlyCollection<string> StatusOptions { get; } = new[]
    {
        PayoutStatus.Scheduled,
        PayoutStatus.Processing,
        PayoutStatus.Paid,
        PayoutStatus.Failed
    };

    public bool HasFilters => ScheduledFrom.HasValue || ScheduledTo.HasValue || (Statuses?.Count ?? 0) > 0;
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

        var result = await _cartRepository.GetPayoutSchedulesForSellerAsync(user.Id, BuildQuery());

        Payouts = result.Schedules;
        TotalCount = result.TotalCount;
        TotalPages = result.TotalCount == 0 ? 1 : (int)Math.Ceiling(result.TotalCount / (double)result.PageSize);
        Page = result.Page;

        return Page();
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

    private PayoutScheduleQuery BuildQuery()
    {
        var from = ScheduledFrom.HasValue
            ? DateTime.SpecifyKind(ScheduledFrom.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var to = ScheduledTo.HasValue
            ? DateTime.SpecifyKind(ScheduledTo.Value, DateTimeKind.Utc).Date.AddDays(1).AddTicks(-1)
            : (DateTime?)null;

        return new PayoutScheduleQuery
        {
            ScheduledFrom = from.HasValue ? new DateTimeOffset(from.Value) : null,
            ScheduledTo = to.HasValue ? new DateTimeOffset(to.Value) : null,
            Statuses = Statuses,
            Page = Page,
            PageSize = PageSize
        };
    }

    public string StatusLabel(string status) => status switch
    {
        PayoutStatus.Paid => "Paid",
        PayoutStatus.Processing => "Processing",
        PayoutStatus.Failed => "Failed",
        _ => "Scheduled"
    };

    public string BuildPageUrl(int pageNumber)
    {
        var routeValues = new
        {
            Page = pageNumber,
            ScheduledFrom = ScheduledFrom?.ToString("yyyy-MM-dd"),
            ScheduledTo = ScheduledTo?.ToString("yyyy-MM-dd"),
            Statuses = Statuses.ToArray()
        };

        return Url.Page("/Seller/Payouts/Index", routeValues) ?? string.Empty;
    }
}
