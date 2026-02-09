using System.Collections.Generic;
using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.Modules.Cart.Application;

public static class DeliveryAddressRules
{
    private static readonly HashSet<string> AllowedCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "PL",
        "DE",
        "US",
        "GB",
        "FR"
    };

    public static List<string> Validate(DeliveryAddressInput input)
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

    public static bool IsSupportedRegion(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        return AllowedCountries.Contains(countryCode.Trim());
    }

    public static DeliveryAddressInput Normalize(DeliveryAddressInput input) =>
        new(
            input.RecipientName.Trim(),
            input.Line1.Trim(),
            string.IsNullOrWhiteSpace(input.Line2) ? null : input.Line2.Trim(),
            input.City.Trim(),
            input.Region.Trim(),
            input.PostalCode.Trim(),
            input.CountryCode.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim());

    public static string AllowedRegionsLabel => string.Join(", ", AllowedCountries);
}
