using System.ComponentModel.DataAnnotations;
using System.Linq;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.Modules.Products.Application
{
    public class CreateProduct
    {
        private readonly IProductRepository _repository;

        public CreateProduct(IProductRepository repository)
        {
            _repository = repository;
        }

        public async Task<ProductModel> CreateAsync(Request request, string sellerId)
        {
            var product = new ProductModel
            {
                Name = request.Title.Trim(),
                Sku = request.Sku?.Trim() ?? string.Empty,
                Description = request.Description?.Trim() ?? string.Empty,
                Price = request.Price,
                Stock = request.Stock,
                Category = request.Category.Trim(),
                ImageUrls = NormalizeMultiline(request.ImageUrls),
                WeightKg = request.WeightKg ?? 0,
                LengthCm = request.LengthCm ?? 0,
                WidthCm = request.WidthCm ?? 0,
                HeightCm = request.HeightCm ?? 0,
                ShippingMethods = NormalizeMultiline(request.ShippingMethods),
                SellerId = sellerId,
                Status = ProductStatuses.Draft
            };

            return await _repository.Add(product);
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

            [StringLength(100)]
            public string? Sku { get; set; }

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
        }
    }
}
