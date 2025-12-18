using System.ComponentModel.DataAnnotations;
using System.Linq;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.Modules.Products.Application
{
    public class UpdateProduct
    {
        private readonly IProductRepository _repository;

        public UpdateProduct(IProductRepository repository)
        {
            _repository = repository;
        }

        public async Task<ProductModel?> UpdateAsync(int productId, Request request, string sellerId)
        {
            var existing = await _repository.GetById(productId);
            if (existing is null || !string.Equals(existing.SellerId, sellerId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            existing.Name = request.Title.Trim();
            existing.Description = request.Description?.Trim() ?? string.Empty;
            existing.Price = request.Price;
            existing.Stock = request.Stock;
            existing.Category = request.Category.Trim();
            existing.ImageUrls = NormalizeMultiline(request.ImageUrls);
            existing.WeightKg = request.WeightKg ?? 0;
            existing.LengthCm = request.LengthCm ?? 0;
            existing.WidthCm = request.WidthCm ?? 0;
            existing.HeightCm = request.HeightCm ?? 0;
            existing.ShippingMethods = NormalizeMultiline(request.ShippingMethods);
            if (request.Publish)
            {
                existing.Status = ProductStatuses.Active;
            }

            await _repository.Update(existing);
            return existing;
        }

        private static string NormalizeMultiline(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var lines = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l));

            return string.Join(Environment.NewLine, lines);
        }

        public class Request
        {
            [Required]
            [StringLength(200, MinimumLength = 3)]
            public string Title { get; set; } = string.Empty;

            [StringLength(2000)]
            public string? Description { get; set; }

            [Range(0.01, 1_000_000)]
            public decimal Price { get; set; }

            [Range(0, 1_000_000)]
            public int Stock { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 2)]
            public string Category { get; set; } = string.Empty;

            [StringLength(4000)]
            public string? ImageUrls { get; set; }

            [Range(0.0, 1000.0)]
            public decimal? WeightKg { get; set; }

            [Range(0.0, 500.0)]
            public decimal? LengthCm { get; set; }

            [Range(0.0, 500.0)]
            public decimal? WidthCm { get; set; }

            [Range(0.0, 500.0)]
            public decimal? HeightCm { get; set; }

            [StringLength(1000)]
            public string? ShippingMethods { get; set; }

            public bool Publish { get; set; }
        }
    }
}
