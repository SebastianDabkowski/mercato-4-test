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

namespace SD.ProjectName.WebApp.Pages.Buyer.Orders;

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
    public string? SellerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    public List<OrderModel> Orders { get; private set; } = new();
    public List<SellerSummary> SellerOptions { get; private set; } = new();
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public IReadOnlyCollection<string> StatusOptions { get; } = new[]
    {
        OrderStatus.New,
        OrderStatus.Paid,
        OrderStatus.Preparing,
        OrderStatus.Shipped,
        OrderStatus.Delivered,
        OrderStatus.Cancelled,
        OrderStatus.Refunded
    };

    public bool HasSellerFilter => !string.IsNullOrWhiteSpace(SellerId);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public async Task<IActionResult> OnGetAsync()
    {
        Statuses ??= new List<string>();
        var normalizedPage = Page < 1 ? 1 : Page;
        Page = normalizedPage;
        var sellerFilter = string.IsNullOrWhiteSpace(SellerId) ? null : SellerId.Trim();

        var fromDate = CreatedFrom.HasValue
            ? DateTime.SpecifyKind(CreatedFrom.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var toDate = CreatedTo.HasValue
            ? DateTime.SpecifyKind(CreatedTo.Value, DateTimeKind.Utc).Date.AddDays(1).AddTicks(-1)
            : (DateTime?)null;

        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var result = await _cartRepository.GetOrdersForBuyerAsync(
            buyerId,
            new BuyerOrdersQuery
            {
                Statuses = Statuses,
                CreatedFrom = fromDate.HasValue ? new DateTimeOffset(fromDate.Value) : null,
                CreatedTo = toDate.HasValue ? new DateTimeOffset(toDate.Value) : null,
                SellerId = sellerFilter,
                Page = normalizedPage,
                PageSize = PageSize
            });

        Orders = result.Orders;
        SellerOptions = result.Sellers;
        TotalCount = result.TotalCount;
        Page = result.Page;
        SellerId = sellerFilter;
        TotalPages = result.TotalCount == 0 ? 1 : (int)Math.Ceiling(result.TotalCount / (double)result.PageSize);

        return Page();
    }

    public string BuildPageUrl(int pageNumber)
    {
        var routeValues = new
        {
            Page = pageNumber,
            SellerId,
            CreatedFrom = CreatedFrom?.ToString("yyyy-MM-dd"),
            CreatedTo = CreatedTo?.ToString("yyyy-MM-dd"),
            Statuses = Statuses.ToArray()
        };

        return Url.Page("/Buyer/Orders/Index", routeValues) ?? string.Empty;
    }
}
