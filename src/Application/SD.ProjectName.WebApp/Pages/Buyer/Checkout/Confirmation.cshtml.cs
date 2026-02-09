using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Services;
using CartDomainModel = SD.ProjectName.Modules.Cart.Domain.CartModel;

namespace SD.ProjectName.WebApp.Pages.Buyer.Checkout;

[AllowAnonymous]
public class ConfirmationModel : PageModel
{
    private readonly ICartIdentityService _cartIdentityService;
    private readonly GetCartItems _getCartItems;
    private readonly ICartRepository _cartRepository;
    private readonly CartCalculationService _cartCalculationService;
    private readonly PlaceOrder _placeOrder;
    private readonly OrderConfirmationEmailService _orderConfirmationEmailService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ConfirmationModel(
        ICartIdentityService cartIdentityService,
        GetCartItems getCartItems,
        ICartRepository cartRepository,
        CartCalculationService cartCalculationService,
        PlaceOrder placeOrder,
        OrderConfirmationEmailService orderConfirmationEmailService,
        UserManager<ApplicationUser> userManager)
    {
        _cartIdentityService = cartIdentityService;
        _getCartItems = getCartItems;
        _cartRepository = cartRepository;
        _cartCalculationService = cartCalculationService;
        _placeOrder = placeOrder;
        _orderConfirmationEmailService = orderConfirmationEmailService;
        _userManager = userManager;
    }

    public CartTotals Totals { get; private set; } = new();
    public DeliveryAddressModel? SelectedAddress { get; private set; }
    public PaymentSelectionModel? PaymentSelection { get; private set; }
    public List<ShippingSelectionModel> ShippingSelections { get; private set; } = new();
    public List<CheckoutValidationIssue> ValidationIssues { get; private set; } = new();
    public OrderModel? CreatedOrder { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        return await LoadCheckoutStateAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var result = await _placeOrder.ExecuteAsync(buyerId);
        if (!result.Success)
        {
            ValidationIssues = result.Issues;
            return await LoadCheckoutStateAsync();
        }

        var order = result.Order!;
        CreatedOrder = order;
        PaymentSelection = result.PaymentSelection;
        SelectedAddress = result.DeliveryAddress;

        TempData["OrderSuccess"] = $"Order {order.Id} placed successfully.";
        await SendConfirmationEmailAsync(buyerId, order);

        return RedirectToPage("/Buyer/Orders/Details", new { orderId = order.Id });
    }

    private async Task<IActionResult> LoadCheckoutStateAsync()
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var cartItems = await _getCartItems.ExecuteAsync(buyerId);
        if (!cartItems.Any())
        {
            return RedirectToPage("/Buyer/Cart");
        }

        PaymentSelection = await _cartRepository.GetPaymentSelectionAsync(buyerId);
        if (PaymentSelection is null || PaymentSelection.Status != PaymentStatus.Authorized)
        {
            TempData["CheckoutError"] = "Complete payment before viewing the confirmation page.";
            return RedirectToPage("/Buyer/Checkout/Payment");
        }

        SelectedAddress = await _cartRepository.GetSelectedAddressAsync(buyerId);
        if (SelectedAddress is null)
        {
            TempData["CheckoutError"] = "Select a delivery address before completing checkout.";
            return RedirectToPage("/Buyer/Checkout/Address");
        }

        ShippingSelections = await _cartRepository.GetShippingSelectionsAsync(buyerId);
        if (!HasSelectionsForAllSellers(cartItems, ShippingSelections))
        {
            TempData["CheckoutError"] = "Choose shipping methods for all sellers before completing checkout.";
            return RedirectToPage("/Buyer/Checkout/Shipping");
        }

        var shippingRules = await _cartRepository.GetShippingRulesAsync();
        Totals = BuildTotals(cartItems, shippingRules, ShippingSelections);

        return Page();
    }

    private async Task SendConfirmationEmailAsync(string buyerId, OrderModel order)
    {
        var user = await _userManager.FindByIdAsync(buyerId);
        if (user?.Email is null)
        {
            return;
        }

        await _orderConfirmationEmailService.SendAsync(user.Email, order);
    }

    private CartTotals BuildTotals(
        List<CartItemModel> cartItems,
        List<ShippingRuleModel> shippingRules,
        List<ShippingSelectionModel> shippingSelections)
    {
        var selectionMap = shippingSelections.ToDictionary(
            s => s.SellerId,
            s => s.ShippingMethod,
            StringComparer.OrdinalIgnoreCase);

        var cart = new CartDomainModel { Items = cartItems };
        return _cartCalculationService.CalculateTotals(
            cart,
            shippingRules,
            selectedShippingMethods: selectionMap);
    }

    private static bool HasSelectionsForAllSellers(IEnumerable<CartItemModel> cartItems, IEnumerable<ShippingSelectionModel> selections)
    {
        var sellerIds = cartItems.Select(i => i.SellerId).Distinct().ToList();
        return sellerIds.All(id => selections.Any(s => s.SellerId == id));
    }
}
