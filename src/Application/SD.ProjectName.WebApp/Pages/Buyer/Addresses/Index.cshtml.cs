using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.WebApp.Pages.Buyer.Checkout;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Buyer.Addresses;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ICartIdentityService _cartIdentityService;
    private readonly GetDeliveryAddresses _getDeliveryAddresses;
    private readonly DeliveryAddressBook _deliveryAddressBook;

    public IndexModel(
        ICartIdentityService cartIdentityService,
        GetDeliveryAddresses getDeliveryAddresses,
        DeliveryAddressBook deliveryAddressBook)
    {
        _cartIdentityService = cartIdentityService;
        _getDeliveryAddresses = getDeliveryAddresses;
        _deliveryAddressBook = deliveryAddressBook;
    }

    public List<DeliveryAddressModel> Addresses { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? AddressId { get; set; }

    [BindProperty]
    public DeliveryAddressForm Input { get; set; } = new();

    [BindProperty]
    public bool SetAsDefault { get; set; } = true;

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public string AllowedRegionsLabel => DeliveryAddressRules.AllowedRegionsLabel;
    public string FormTitle => AddressId.HasValue ? "Edit address" : "Add new address";
    public bool IsEditing => AddressId.HasValue;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAddressesAsync();
        if (IsEditing)
        {
            var selected = Addresses.FirstOrDefault(a => a.Id == AddressId);
            if (selected is null)
            {
                ErrorMessage = "Address not found.";
                return RedirectToPage();
            }

            Input = DeliveryAddressForm.FromAddress(selected);
            SetAsDefault = selected.IsSelectedForCheckout;
        }
        else
        {
            SetAsDefault = !Addresses.Any(a => a.IsSelectedForCheckout);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAddressesAsync();
            return Page();
        }

        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var result = await _deliveryAddressBook.SaveAsync(buyerId, AddressId, Input.ToInput(), SetAsDefault);
        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await LoadAddressesAsync();
            return Page();
        }

        StatusMessage = AddressId.HasValue ? "Address updated." : "Address added.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMakeDefaultAsync(int addressId)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var result = await _deliveryAddressBook.SetDefaultAsync(buyerId, addressId);
        if (!result.Success)
        {
            ErrorMessage = string.Join(" ", result.Errors);
            return RedirectToPage();
        }

        StatusMessage = "Default address updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int addressId)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var result = await _deliveryAddressBook.DeleteAsync(buyerId, addressId);
        if (!result.Success)
        {
            ErrorMessage = string.Join(" ", result.Errors);
        }
        else
        {
            StatusMessage = "Address removed.";
        }

        return RedirectToPage();
    }

    private async Task LoadAddressesAsync()
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        Addresses = (await _getDeliveryAddresses.ExecuteAsync(buyerId))
            .Where(a => a.SavedToProfile)
            .OrderByDescending(a => a.IsSelectedForCheckout)
            .ThenByDescending(a => a.UpdatedAt)
            .ToList();
    }
}
