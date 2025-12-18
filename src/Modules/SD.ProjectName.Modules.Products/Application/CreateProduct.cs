using System.ComponentModel.DataAnnotations;
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
                Description = request.Description?.Trim() ?? string.Empty,
                Price = request.Price,
                Stock = request.Stock,
                Category = request.Category.Trim(),
                SellerId = sellerId,
                Status = "draft"
            };

            return await _repository.Add(product);
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
        }
    }
}
