using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class GetDeliveryAddresses
{
    private readonly ICartRepository _repository;

    public GetDeliveryAddresses(ICartRepository repository)
    {
        _repository = repository;
    }

    public Task<List<DeliveryAddressModel>> ExecuteAsync(string buyerId)
    {
        return _repository.GetAddressesAsync(buyerId);
    }
}
