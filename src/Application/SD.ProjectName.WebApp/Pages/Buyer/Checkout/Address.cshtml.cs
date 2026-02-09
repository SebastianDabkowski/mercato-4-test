using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Buyer.Checkout;

[AllowAnonymous]
public class AddressModel : PageModel
{
    private readonly ICartIdentityService _cartIdentityService;
    private readonly GetDeliveryAddresses _getDeliveryAddresses;
    private readonly SetDeliveryAddressForCheckout _setDeliveryAddressForCheckout;

    public AddressModel(
        ICartIdentityService cartIdentityService,
        GetDeliveryAddresses getDeliveryAddresses,
        SetDeliveryAddressForCheckout setDeliveryAddressForCheckout)
    {
        _cartIdentityService = cartIdentityService;
        _getDeliveryAddresses = getDeliveryAddresses;
        _setDeliveryAddressForCheckout = setDeliveryAddressForCheckout;
    }

    public List<DeliveryAddressModel> SavedAddresses { get; private set; } = new();
    public DeliveryAddressModel? SelectedAddress { get; private set; }

    [BindProperty]
    public DeliveryAddressForm Input { get; set; } = new();

    [BindProperty]
    public bool SaveToProfile { get; set; }

    [BindProperty]
    public int? SelectedAddressId { get; set; }

    public string AllowedRegionsLabel => SetDeliveryAddressForCheckout.AllowedRegionsLabel;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public async Task OnGetAsync()
    {
        await LoadAddressesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();

        if (SelectedAddressId.HasValue && SelectedAddressId.Value > 0)
        {
            var selectResult = await _setDeliveryAddressForCheckout.SelectExistingAsync(buyerId, SelectedAddressId.Value);
            if (!selectResult.Success)
            {
                ModelState.Clear();
                foreach (var error in selectResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                await LoadAddressesAsync();
                return Page();
            }

            TempData["AddressSaved"] = "Delivery address selected for checkout.";
            return RedirectToPage();
        }

        if (!ModelState.IsValid)
        {
            await LoadAddressesAsync();
            return Page();
        }

        var result = await _setDeliveryAddressForCheckout.SaveNewAsync(
            buyerId,
            Input.ToInput(),
            IsAuthenticated && SaveToProfile);

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await LoadAddressesAsync();
            return Page();
        }

        TempData["AddressSaved"] = "Delivery address saved for this checkout.";
        return RedirectToPage();
    }

    private async Task LoadAddressesAsync()
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        SavedAddresses = await _getDeliveryAddresses.ExecuteAsync(buyerId);
        SelectedAddress = SavedAddresses.FirstOrDefault(a => a.IsSelectedForCheckout);
    }
}

public class DeliveryAddressForm
{
    [Required]
    [StringLength(200)]
    public string RecipientName { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string Line1 { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Line2 { get; set; }

    [Required]
    [StringLength(150)]
    public string City { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Region { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    [StringLength(3, MinimumLength = 2)]
    public string CountryCode { get; set; } = "PL";

    [StringLength(50)]
    public string? PhoneNumber { get; set; }

    public DeliveryAddressInput ToInput() =>
        new(RecipientName, Line1, Line2, City, Region, PostalCode, CountryCode, PhoneNumber);
}
