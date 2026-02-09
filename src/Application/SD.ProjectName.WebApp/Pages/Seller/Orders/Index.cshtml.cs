using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Seller.Orders;

[Authorize(Roles = IdentityRoles.Seller)]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICartRepository _cartRepository;
    private readonly OrderStatusService _orderStatusService;
    private readonly ShippingNotificationEmailService _shippingNotificationEmailService;
    private const int PageSize = 10;
    private const int ExportRowLimit = 5000;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        ICartRepository cartRepository,
        OrderStatusService orderStatusService,
        ShippingNotificationEmailService shippingNotificationEmailService)
    {
        _userManager = userManager;
        _cartRepository = cartRepository;
        _orderStatusService = orderStatusService;
        _shippingNotificationEmailService = shippingNotificationEmailService;
    }

    [BindProperty(SupportsGet = true)]
    public List<string> Statuses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public DateTime? CreatedFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? CreatedTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? BuyerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool WithoutTracking { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    public List<SellerOrderModel> Orders { get; private set; } = new();
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
        OrderStatus.Failed,
        OrderStatus.Refunded
    };

    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    [TempData]
    public string? StatusMessage { get; set; }
    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        NormalizeFilters();
        await LoadOrders(user.Id);

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        NormalizeFilters();
        var result = await _cartRepository.GetSellerOrdersAsync(user.Id, BuildQuery(fetchAll: true));

        if (result.TotalCount == 0)
        {
            ErrorMessage = "No orders match the selected filters for export.";
            await LoadOrders(user.Id);
            return Page();
        }

        if (result.TotalCount > ExportRowLimit)
        {
            ErrorMessage = $"Too many orders to export at once. Please narrow your filters to {ExportRowLimit} or fewer sub-orders.";
            await LoadOrders(user.Id);
            return Page();
        }

        var csv = BuildCsv(result.Orders);
        var fileName = $"seller-orders-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

        return File(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv), "text/csv", fileName);
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(
        int sellerOrderId,
        string status,
        string? trackingNumber,
        string? trackingCarrier,
        string? trackingUrl)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        NormalizeFilters();

        var sellerOrder = await _cartRepository.GetSellerOrderAsync(sellerOrderId, user.Id);
        if (sellerOrder is null)
        {
            var existing = await _cartRepository.GetSellerOrderByIdAsync(sellerOrderId);
            if (existing is not null)
            {
                return Forbid();
            }

            return NotFound();
        }

        var previousStatus = OrderStatusFlow.NormalizeStatus(sellerOrder.Status);
        var result = await _orderStatusService.UpdateSellerOrderStatusAsync(
            sellerOrderId,
            user.Id,
            status,
            trackingNumber,
            trackingCarrier,
            trackingUrl);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error;
        }
        else
        {
            StatusMessage = "Status updated.";
            if (IsNewlyShipped(previousStatus, result))
            {
                await SendShippingNotificationAsync(sellerOrder);
            }
        }

        await LoadOrders(user.Id);
        return Page();
    }

    private void NormalizeFilters()
    {
        Statuses ??= new List<string>();
        Page = Page < 1 ? 1 : Page;
        BuyerId = string.IsNullOrWhiteSpace(BuyerId) ? null : BuyerId.Trim();
    }

    private async Task LoadOrders(string sellerId)
    {
        var result = await _cartRepository.GetSellerOrdersAsync(sellerId, BuildQuery());
        Orders = result.Orders;
        TotalCount = result.TotalCount;
        TotalPages = result.TotalCount == 0 ? 1 : (int)Math.Ceiling(result.TotalCount / (double)result.PageSize);
        Page = result.Page;
    }

    private SellerOrdersQuery BuildQuery(bool fetchAll = false)
    {
        var fromDate = CreatedFrom.HasValue
            ? DateTime.SpecifyKind(CreatedFrom.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var toDate = CreatedTo.HasValue
            ? DateTime.SpecifyKind(CreatedTo.Value, DateTimeKind.Utc).Date.AddDays(1).AddTicks(-1)
            : (DateTime?)null;
        var pageSize = fetchAll ? ExportRowLimit : PageSize;

        return new SellerOrdersQuery
        {
            Statuses = Statuses,
            CreatedFrom = fromDate.HasValue ? new DateTimeOffset(fromDate.Value) : null,
            CreatedTo = toDate.HasValue ? new DateTimeOffset(toDate.Value) : null,
            BuyerId = BuyerId,
            WithoutTracking = WithoutTracking,
            Page = fetchAll ? 1 : Page,
            PageSize = pageSize
        };
    }

    public string BuildPageUrl(int pageNumber)
    {
        var routeValues = new
        {
            Page = pageNumber,
            BuyerId,
            CreatedFrom = CreatedFrom?.ToString("yyyy-MM-dd"),
            CreatedTo = CreatedTo?.ToString("yyyy-MM-dd"),
            WithoutTracking,
            Statuses = Statuses.ToArray()
        };

        return Url.Page("/Seller/Orders/Index", routeValues) ?? string.Empty;
    }

    private static string BuildCsv(IEnumerable<SellerOrderModel> orders)
    {
        var builder = new StringBuilder();
        builder.AppendLine("OrderId,SubOrderId,SellerId,OrderCreatedAt,Status,BuyerId,BuyerName,DeliveryLine1,DeliveryLine2,DeliveryCity,DeliveryRegion,DeliveryPostalCode,DeliveryCountryCode,DeliveryPhone,ShippingMethod,ShippingCost,ItemsSubtotal,OrderTotal,TrackingNumber,TrackingCarrier,TrackingUrl,Items");

        foreach (var order in orders)
        {
            var orderInfo = order.Order;
            var createdAt = orderInfo?.CreatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") ?? string.Empty;
            var status = OrderStatusFlow.NormalizeStatus(order.Status);
            var shippingMethod = order.ShippingSelection?.ShippingMethod ?? string.Empty;
            var shippingCost = (order.ShippingSelection?.Cost ?? order.ShippingTotal).ToString("0.00", CultureInfo.InvariantCulture);
            var itemsSubtotal = order.ItemsSubtotal.ToString("0.00", CultureInfo.InvariantCulture);
            var orderTotal = order.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture);
            var itemsValue = string.Join("; ", (order.Items ?? Enumerable.Empty<OrderItemModel>())
                .Select(i => $"{i.ProductName} (SKU: {i.ProductSku}) x{i.Quantity}"));

            builder.AppendLine(string.Join(",",
                order.OrderId,
                order.Id,
                Quote(order.SellerId),
                Quote(createdAt),
                Quote(status),
                Quote(orderInfo?.BuyerId ?? string.Empty),
                Quote(orderInfo?.DeliveryRecipientName ?? string.Empty),
                Quote(orderInfo?.DeliveryLine1 ?? string.Empty),
                Quote(orderInfo?.DeliveryLine2 ?? string.Empty),
                Quote(orderInfo?.DeliveryCity ?? string.Empty),
                Quote(orderInfo?.DeliveryRegion ?? string.Empty),
                Quote(orderInfo?.DeliveryPostalCode ?? string.Empty),
                Quote(orderInfo?.DeliveryCountryCode ?? string.Empty),
                Quote(orderInfo?.DeliveryPhoneNumber ?? string.Empty),
                Quote(shippingMethod),
                shippingCost,
                itemsSubtotal,
                orderTotal,
                Quote(order.TrackingNumber ?? string.Empty),
                Quote(order.TrackingCarrier ?? string.Empty),
                Quote(order.TrackingUrl ?? string.Empty),
                Quote(itemsValue)));
        }

        return builder.ToString();
    }

    private static string Quote(string? value)
    {
        var sanitized = value ?? string.Empty;
        return $"\"{sanitized.Replace("\"", "\"\"")}\"";
    }

    private async Task SendShippingNotificationAsync(SellerOrderModel sellerOrder)
    {
        var buyerId = sellerOrder.Order?.BuyerId;
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            return;
        }

        var buyer = await _userManager.FindByIdAsync(buyerId);
        if (buyer?.Email is null)
        {
            return;
        }

        await _shippingNotificationEmailService.SendShippedAsync(buyer.Email, sellerOrder);
    }

    private static bool IsNewlyShipped(string previousStatus, OrderStatusResult result)
    {
        var newStatus = OrderStatusFlow.NormalizeStatus(result.SubOrderStatus ?? string.Empty);
        return !OrderStatusFlow.IsShippedOrBeyond(previousStatus) &&
               string.Equals(newStatus, OrderStatus.Shipped, StringComparison.OrdinalIgnoreCase);
    }
}
