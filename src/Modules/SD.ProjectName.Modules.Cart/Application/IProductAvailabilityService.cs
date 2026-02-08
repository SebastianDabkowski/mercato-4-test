namespace SD.ProjectName.Modules.Cart.Application
{
    public interface IProductAvailabilityService
    {
        Task<int?> GetAvailableStockAsync(int productId);
    }
}
