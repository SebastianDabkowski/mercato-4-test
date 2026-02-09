using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public record AddressCommandResult(bool Success, List<string> Errors)
{
    public static AddressCommandResult Ok() => new(true, new List<string>());

    public static AddressCommandResult Fail(params string[] errors) =>
        new(false, errors.ToList());
}

public class DeliveryAddressBook
{
    private readonly ICartRepository _repository;
    private readonly TimeProvider _timeProvider;

    public DeliveryAddressBook(ICartRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<DeliveryAddressResult> SaveAsync(string buyerId, int? addressId, DeliveryAddressInput input, bool setAsDefault)
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

        var normalized = DeliveryAddressRules.Normalize(input);
        var now = _timeProvider.GetUtcNow();
        DeliveryAddressModel address = addressId.HasValue
            ? await _repository.GetAddressAsync(addressId.Value) ?? new DeliveryAddressModel()
            : new DeliveryAddressModel
            {
                BuyerId = buyerId,
                CreatedAt = now
            };

        if (addressId.HasValue && !string.Equals(address.BuyerId, buyerId, StringComparison.Ordinal))
        {
            return DeliveryAddressResult.Failure(new[] { "Address not found." });
        }

        address.RecipientName = normalized.RecipientName;
        address.Line1 = normalized.Line1;
        address.Line2 = normalized.Line2;
        address.City = normalized.City;
        address.Region = normalized.Region;
        address.PostalCode = normalized.PostalCode;
        address.CountryCode = normalized.CountryCode;
        address.PhoneNumber = normalized.PhoneNumber;
        address.SavedToProfile = true;
        address.UpdatedAt = now;

        if (setAsDefault)
        {
            await _repository.ClearSelectedAddressAsync(buyerId);
            address.IsSelectedForCheckout = true;
        }

        var saved = await _repository.AddOrUpdateAddressAsync(address);
        return DeliveryAddressResult.SuccessResult(saved);
    }

    public async Task<DeliveryAddressResult> SetDefaultAsync(string buyerId, int addressId)
    {
        var address = await _repository.GetAddressAsync(addressId);
        if (address is null || !string.Equals(address.BuyerId, buyerId, StringComparison.Ordinal))
        {
            return DeliveryAddressResult.Failure(new[] { "Address not found." });
        }

        if (!DeliveryAddressRules.IsSupportedRegion(address.CountryCode))
        {
            return DeliveryAddressResult.Failure(new[]
            {
                $"Items cannot be shipped to {address.CountryCode}. Supported regions: {DeliveryAddressRules.AllowedRegionsLabel}."
            });
        }

        await _repository.ClearSelectedAddressAsync(buyerId);
        address.IsSelectedForCheckout = true;
        address.SavedToProfile = true;
        address.UpdatedAt = _timeProvider.GetUtcNow();

        var saved = await _repository.AddOrUpdateAddressAsync(address);
        return DeliveryAddressResult.SuccessResult(saved);
    }

    public async Task<AddressCommandResult> DeleteAsync(string buyerId, int addressId)
    {
        var address = await _repository.GetAddressAsync(addressId);
        if (address is null || !string.Equals(address.BuyerId, buyerId, StringComparison.Ordinal))
        {
            return AddressCommandResult.Fail("Address not found.");
        }

        var usedInActiveOrder = await _repository.IsAddressUsedInActiveOrderAsync(buyerId, address);
        if (usedInActiveOrder)
        {
            return AddressCommandResult.Fail("Address is used in an active order and cannot be deleted.");
        }

        await _repository.DeleteAddressAsync(addressId);
        if (address.IsSelectedForCheckout)
        {
            await _repository.ClearSelectedAddressAsync(buyerId);
        }

        return AddressCommandResult.Ok();
    }
}
