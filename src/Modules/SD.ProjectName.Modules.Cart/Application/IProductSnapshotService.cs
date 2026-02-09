namespace SD.ProjectName.Modules.Cart.Application;

public interface IProductSnapshotService
{
    Task<ProductSnapshot?> GetSnapshotAsync(int productId);
}

public record ProductSnapshot(int ProductId, decimal Price, int Stock);
