using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public record DeliveryAddressInput(
    string RecipientName,
    string Line1,
    string? Line2,
    string City,
    string Region,
    string PostalCode,
    string CountryCode,
    string? PhoneNumber);

public record DeliveryAddressResult(bool Success, List<string> Errors, DeliveryAddressModel? Address)
{
    public static DeliveryAddressResult Failure(IEnumerable<string> errors) =>
        new(false, errors.ToList(), null);

    public static DeliveryAddressResult SuccessResult(DeliveryAddressModel address) =>
        new(true, new List<string>(), address);
}

public class SetDeliveryAddressForCheckout
{
    private readonly ICartRepository _repository;
    private readonly TimeProvider _timeProvider;

    public SetDeliveryAddressForCheckout(ICartRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<DeliveryAddressResult> SaveNewAsync(string buyerId, DeliveryAddressInput input, bool saveToProfile)
    {
        var errors = DeliveryAddressRules.Validate(input);
        if (errors.Count > 0)
        {
            return DeliveryAddressResult.Failure(errors);
        }

        if (!DeliveryAddressRules.IsSupportedRegion(input.CountryCode))
        {
            errors.Add($"Items cannot be shipped to {input.CountryCode}. Supported regions: {DeliveryAddressRules.AllowedRegionsLabel}.");
            return DeliveryAddressResult.Failure(errors);
        }

        await _repository.ClearSelectedAddressAsync(buyerId);

        var normalized = DeliveryAddressRules.Normalize(input);
        var now = _timeProvider.GetUtcNow();
        var address = new DeliveryAddressModel
        {
            BuyerId = buyerId,
            RecipientName = normalized.RecipientName,
            Line1 = normalized.Line1,
            Line2 = normalized.Line2,
            City = normalized.City,
            Region = normalized.Region,
            PostalCode = normalized.PostalCode,
            CountryCode = normalized.CountryCode,
            PhoneNumber = normalized.PhoneNumber,
            SavedToProfile = saveToProfile,
            IsSelectedForCheckout = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var saved = await _repository.AddOrUpdateAddressAsync(address);
        return DeliveryAddressResult.SuccessResult(saved);
    }

    public async Task<DeliveryAddressResult> SelectExistingAsync(string buyerId, int addressId)
    {
        var existing = await _repository.GetAddressAsync(addressId);
        if (existing is null || !string.Equals(existing.BuyerId, buyerId, StringComparison.Ordinal))
        {
            return DeliveryAddressResult.Failure(new[] { "Selected address could not be found for your account." });
        }

        if (!DeliveryAddressRules.IsSupportedRegion(existing.CountryCode))
        {
            return DeliveryAddressResult.Failure(new[] { $"Items cannot be shipped to {existing.CountryCode}. Supported regions: {DeliveryAddressRules.AllowedRegionsLabel}." });
        }

        await _repository.ClearSelectedAddressAsync(buyerId);

        existing.IsSelectedForCheckout = true;
        existing.UpdatedAt = _timeProvider.GetUtcNow();
        var saved = await _repository.AddOrUpdateAddressAsync(existing);

        return DeliveryAddressResult.SuccessResult(saved);
    }

    public static string AllowedRegionsLabel => DeliveryAddressRules.AllowedRegionsLabel;
}
