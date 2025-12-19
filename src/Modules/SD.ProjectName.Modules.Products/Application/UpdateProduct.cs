using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Collections.Generic;
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
            if (existing is null
                || existing.Status == ProductStatuses.Archived
                || !string.Equals(existing.SellerId, sellerId, StringComparison.OrdinalIgnoreCase))
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

            var targetStatus = DetermineTargetStatus(existing.Status, request.Publish);
            if (targetStatus == ProductStatuses.Active)
            {
                var validationErrors = ValidateActivation(existing);
                if (validationErrors.Any())
                {
                    throw new ProductActivationException(validationErrors);
                }
            }

            existing.Status = targetStatus;

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

        private static string DetermineTargetStatus(string currentStatus, bool publishRequested)
        {
            if (publishRequested)
            {
                return ProductStatuses.Active;
            }

            if (string.Equals(currentStatus, ProductStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                return ProductStatuses.Suspended;
            }

            return currentStatus;
        }

        private static List<string> ValidateActivation(ProductModel product)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(product.Description))
            {
                errors.Add("Description is required to activate the product.");
            }

            var images = (product.ImageUrls ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(i => i.Trim())
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .ToList();

            if (!images.Any())
            {
                errors.Add("At least one product image is required to activate the product.");
            }

            if (string.IsNullOrWhiteSpace(product.Category))
            {
                errors.Add("Category is required to activate the product.");
            }

            if (product.Price <= 0)
            {
                errors.Add("Price must be greater than zero to activate the product.");
            }

            if (product.Stock <= 0)
            {
                errors.Add("Stock must be greater than zero to activate the product.");
            }

            return errors;
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

        public class ProductActivationException : Exception
        {
            public ProductActivationException(IReadOnlyCollection<string> errors)
                : base(errors.Any()
                    ? $"Product cannot be activated. Please fix: {string.Join("; ", errors)}"
                    : "Product cannot be activated due to validation errors.")
            {
                Errors = errors;
            }

            public IReadOnlyCollection<string> Errors { get; }
        }
    }
}
