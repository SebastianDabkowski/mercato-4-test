using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.Modules.Products.Application
{
    public class GetProducts
    {
        private readonly IProductRepository _repository;

        public GetProducts(IProductRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ProductModel>> GetList(string? category = null, ProductSort sort = ProductSort.Newest)
        {
            var products = await _repository.GetList(category);
            return ProductSorting.Apply(products, sort).ToList();
        }

        public async Task<IReadOnlyList<ProductModel>> Search(string? keyword, ProductSort sort = ProductSort.Relevance)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Array.Empty<ProductModel>();
            }

            var trimmed = keyword.Trim();
            if (trimmed.Length > 200)
            {
                trimmed = trimmed[..200];
            }

            var results = await _repository.Search(trimmed);
            return ProductSorting.Apply(results, sort, trimmed).ToList();
        }

        public async Task<List<ProductModel>> GetBySeller(string sellerId, bool includeDrafts = true)
        {
            return await _repository.GetBySeller(sellerId, includeDrafts);
        }

        public async Task<ProductModel?> GetById(int id, bool includeDrafts = true)
        {
            var product = await _repository.GetById(id);
            if (product is null)
            {
                return null;
            }

            if (!includeDrafts && product.Status != ProductStatuses.Active)
            {
                return null;
            }

            return product;
        }

    }
}
