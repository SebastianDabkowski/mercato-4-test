using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Buyer.Cases;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ICartIdentityService _cartIdentityService;
    private readonly ICartRepository _cartRepository;

    public IndexModel(ICartIdentityService cartIdentityService, ICartRepository cartRepository)
    {
        _cartIdentityService = cartIdentityService;
        _cartRepository = cartRepository;
    }

    private const int PageSize = 10;

    [BindProperty(SupportsGet = true)]
    public List<string> Statuses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public DateTime? CreatedFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? CreatedTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    public List<ReturnRequestModel> Cases { get; private set; } = new();
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public IReadOnlyCollection<string> StatusOptions { get; } = new[]
    {
        ReturnRequestStatus.Requested,
        ReturnRequestStatus.Approved,
        ReturnRequestStatus.PartialProposed,
        ReturnRequestStatus.InfoRequested,
        ReturnRequestStatus.Rejected,
        ReturnRequestStatus.Completed
    };

    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public async Task<IActionResult> OnGetAsync()
    {
        Statuses ??= new List<string>();
        var normalizedPage = Page < 1 ? 1 : Page;
        Page = normalizedPage;

        var fromDate = CreatedFrom.HasValue
            ? DateTime.SpecifyKind(CreatedFrom.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var toDate = CreatedTo.HasValue
            ? DateTime.SpecifyKind(CreatedTo.Value, DateTimeKind.Utc).Date.AddDays(1).AddTicks(-1)
            : (DateTime?)null;

        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var result = await _cartRepository.GetReturnRequestsForBuyerAsync(
            buyerId,
            new BuyerReturnRequestsQuery
            {
                Statuses = Statuses,
                CreatedFrom = fromDate.HasValue ? new DateTimeOffset(fromDate.Value) : null,
                CreatedTo = toDate.HasValue ? new DateTimeOffset(toDate.Value) : null,
                Page = normalizedPage,
                PageSize = PageSize
            });

        Cases = result.Requests;
        TotalCount = result.TotalCount;
        Page = result.Page;
        TotalPages = result.TotalCount == 0 ? 1 : (int)Math.Ceiling(result.TotalCount / (double)result.PageSize);

        return Page();
    }

    public string BuildPageUrl(int pageNumber)
    {
        var routeValues = new
        {
            Page = pageNumber,
            CreatedFrom = CreatedFrom?.ToString("yyyy-MM-dd"),
            CreatedTo = CreatedTo?.ToString("yyyy-MM-dd"),
            Statuses = Statuses.ToArray()
        };

        return Url.Page("/Buyer/Cases/Index", routeValues) ?? string.Empty;
    }

    public string GetCaseStatusLabel(string status, string? resolution = null) =>
        status?.ToLowerInvariant() switch
        {
            ReturnRequestStatus.Completed when !string.IsNullOrWhiteSpace(resolution) =>
                $"Resolved: {ReturnRequestWorkflow.GetResolutionLabel(resolution)}",
            ReturnRequestStatus.Requested => "Pending seller review",
            ReturnRequestStatus.Approved => "Approved",
            ReturnRequestStatus.PartialProposed => "Partial solution proposed",
            ReturnRequestStatus.InfoRequested => "More information requested",
            ReturnRequestStatus.Rejected => "Rejected",
            ReturnRequestStatus.Completed => "Completed",
            _ => "Pending seller review"
        };

    public string GetCaseBadgeClass(string status) =>
        status?.ToLowerInvariant() switch
        {
            ReturnRequestStatus.Approved => "bg-success",
            ReturnRequestStatus.Rejected => "bg-danger",
            ReturnRequestStatus.Completed => "bg-secondary",
            ReturnRequestStatus.PartialProposed => "bg-info text-dark",
            _ => "bg-warning text-dark"
        };

    public string GetCaseTypeLabel(string requestType) =>
        string.Equals(requestType, ReturnRequestType.Complaint, StringComparison.OrdinalIgnoreCase)
            ? "Complaint"
            : "Return";
}
