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
    private static readonly HashSet<string> AllowedCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "PL",
        "DE",
        "US",
        "GB",
        "FR"
    };

    private readonly ICartRepository _repository;
    private readonly TimeProvider _timeProvider;

    public SetDeliveryAddressForCheckout(ICartRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<DeliveryAddressResult> SaveNewAsync(string buyerId, DeliveryAddressInput input, bool saveToProfile)
    {
        var errors = Validate(input);
        if (errors.Count > 0)
        {
            return DeliveryAddressResult.Failure(errors);
        }

        if (!IsSupportedRegion(input.CountryCode))
        {
            errors.Add($"Items cannot be shipped to {input.CountryCode}. Supported regions: {string.Join(", ", AllowedCountries)}.");
            return DeliveryAddressResult.Failure(errors);
        }

        await _repository.ClearSelectedAddressAsync(buyerId);

        var now = _timeProvider.GetUtcNow();
        var address = new DeliveryAddressModel
        {
            BuyerId = buyerId,
            RecipientName = input.RecipientName.Trim(),
            Line1 = input.Line1.Trim(),
            Line2 = string.IsNullOrWhiteSpace(input.Line2) ? null : input.Line2.Trim(),
            City = input.City.Trim(),
            Region = input.Region.Trim(),
            PostalCode = input.PostalCode.Trim(),
            CountryCode = input.CountryCode.Trim().ToUpperInvariant(),
            PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim(),
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

        if (!IsSupportedRegion(existing.CountryCode))
        {
            return DeliveryAddressResult.Failure(new[] { $"Items cannot be shipped to {existing.CountryCode}. Supported regions: {string.Join(", ", AllowedCountries)}." });
        }

        await _repository.ClearSelectedAddressAsync(buyerId);

        existing.IsSelectedForCheckout = true;
        existing.UpdatedAt = _timeProvider.GetUtcNow();
        var saved = await _repository.AddOrUpdateAddressAsync(existing);

        return DeliveryAddressResult.SuccessResult(saved);
    }

    private static List<string> Validate(DeliveryAddressInput input)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.RecipientName))
        {
            errors.Add("Recipient name is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Line1))
        {
            errors.Add("Address line 1 is required.");
        }

        if (string.IsNullOrWhiteSpace(input.City))
        {
            errors.Add("City is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Region))
        {
            errors.Add("State/region is required.");
        }

        if (string.IsNullOrWhiteSpace(input.PostalCode))
        {
            errors.Add("Postal code is required.");
        }

        if (string.IsNullOrWhiteSpace(input.CountryCode))
        {
            errors.Add("Country is required.");
        }
        else if (input.CountryCode.Trim().Length is < 2 or > 3)
        {
            errors.Add("Country code must be 2–3 characters (ISO code).");
        }

        return errors;
    }

    private static bool IsSupportedRegion(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        return AllowedCountries.Contains(countryCode.Trim());
    }

    public static string AllowedRegionsLabel => string.Join(", ", AllowedCountries);
}
