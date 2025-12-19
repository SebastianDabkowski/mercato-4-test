using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.Modules.Products.Application
{
    public class BulkUpdateProducts
    {
        private readonly IProductRepository _repository;
        private readonly ILogger<BulkUpdateProducts> _logger;

        public BulkUpdateProducts(IProductRepository repository, ILogger<BulkUpdateProducts> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<Response> ApplyAsync(Request request, string sellerId)
        {
            var response = new Response
            {
                RequestedCount = request.ProductIds?.Distinct().Count() ?? 0
            };

            if (!request.HasAnyChange())
            {
                response.Failed.Add(new ItemFailure { Reason = "Select at least one price or stock change." });
                return response;
            }

            var candidateIds = request.ProductIds?.Distinct().ToList() ?? new List<int>();
            if (!candidateIds.Any())
            {
                response.Failed.Add(new ItemFailure { Reason = "Select at least one product to update." });
                return response;
            }

            var products = await _repository.GetBySeller(sellerId, includeDrafts: true);
            var selected = products.Where(p => candidateIds.Contains(p.Id)).ToList();

            var missingIds = candidateIds.Except(selected.Select(p => p.Id)).ToList();
            foreach (var missing in missingIds)
            {
                response.Failed.Add(new ItemFailure
                {
                    ProductId = missing,
                    Reason = "Product not found or not owned by seller."
                });
            }

            var updates = new List<ProductModel>();

            foreach (var product in selected)
            {
                var (newPrice, priceError) = ApplyPriceChange(product.Price, request);
                var (newStock, stockError) = ApplyStockChange(product.Stock, request);

                if (!string.IsNullOrEmpty(priceError) || !string.IsNullOrEmpty(stockError))
                {
                    response.Failed.Add(new ItemFailure
                    {
                        ProductId = product.Id,
                        Name = product.Name,
                        Reason = string.Join(" ", new[] { priceError, stockError }.Where(r => !string.IsNullOrWhiteSpace(r)))
                    });
                    continue;
                }

                response.Updated.Add(new ItemResult
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    OldPrice = product.Price,
                    NewPrice = newPrice,
                    OldStock = product.Stock,
                    NewStock = newStock
                });

                if (request.ApplyChanges)
                {
                    product.Price = newPrice;
                    product.Stock = newStock;
                    updates.Add(product);
                }
            }

            if (request.ApplyChanges && updates.Any())
            {
                await _repository.UpdateRange(updates);
                _logger.LogInformation("Bulk update applied for seller {SellerId}. Updated {UpdatedCount} items, {FailedCount} failed.", sellerId, response.Updated.Count, response.Failed.Count);
                response.ChangesApplied = true;
            }

            return response;
        }

        private static (decimal NewPrice, string? Error) ApplyPriceChange(decimal currentPrice, Request request)
        {
            if (request.PriceOperation == PriceOperation.None)
            {
                return (currentPrice, null);
            }

            var value = request.PriceValue;
            decimal newPrice = currentPrice;

            switch (request.PriceOperation)
            {
                case PriceOperation.SetTo:
                    newPrice = value;
                    break;
                case PriceOperation.IncreaseByAmount:
                    newPrice = currentPrice + value;
                    break;
                case PriceOperation.DecreaseByAmount:
                    newPrice = currentPrice - value;
                    break;
                case PriceOperation.IncreaseByPercentage:
                    newPrice = currentPrice * (1 + value / 100);
                    break;
                case PriceOperation.DecreaseByPercentage:
                    newPrice = currentPrice * (1 - value / 100);
                    break;
                default:
                    return (currentPrice, "Unsupported price operation.");
            }

            if (newPrice <= 0)
            {
                return (currentPrice, "Price cannot be negative or zero.");
            }

            return (Math.Round(newPrice, 2, MidpointRounding.AwayFromZero), null);
        }

        private static (int NewStock, string? Error) ApplyStockChange(int currentStock, Request request)
        {
            if (request.StockOperation == StockOperation.None)
            {
                return (currentStock, null);
            }

            var value = request.StockValue;
            int newStock = currentStock;

            switch (request.StockOperation)
            {
                case StockOperation.SetTo:
                    newStock = value;
                    break;
                case StockOperation.IncreaseByAmount:
                    newStock = currentStock + value;
                    break;
                case StockOperation.DecreaseByAmount:
                    newStock = currentStock - value;
                    break;
                default:
                    return (currentStock, "Unsupported stock operation.");
            }

            if (newStock < 0)
            {
                return (currentStock, "Stock cannot be negative.");
            }

            return (newStock, null);
        }

        public class Request
        {
            public const decimal MaxPriceValue = 1_000_000;
            public const int MaxStockValue = 1_000_000;

            [Required]
            public List<int> ProductIds { get; set; } = new();

            public PriceOperation PriceOperation { get; set; } = PriceOperation.None;

            [Range(0, (double)MaxPriceValue)]
            public decimal PriceValue { get; set; }

            public StockOperation StockOperation { get; set; } = StockOperation.None;

            [Range(0, MaxStockValue)]
            public int StockValue { get; set; }

            public bool ApplyChanges { get; set; } = true;

            public bool HasAnyChange()
            {
                return PriceOperation != PriceOperation.None || StockOperation != StockOperation.None;
            }
        }

        public class Response
        {
            public int RequestedCount { get; set; }
            public bool ChangesApplied { get; set; }
            public List<ItemResult> Updated { get; } = new();
            public List<ItemFailure> Failed { get; } = new();
        }

        public class ItemResult
        {
            public int ProductId { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal OldPrice { get; set; }
            public decimal NewPrice { get; set; }
            public int OldStock { get; set; }
            public int NewStock { get; set; }
        }

        public class ItemFailure
        {
            public int ProductId { get; set; }
            public string? Name { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        public enum PriceOperation
        {
            None = 0,
            [Display(Name = "Set to amount")]
            SetTo = 1,
            [Display(Name = "Increase by amount")]
            IncreaseByAmount = 2,
            [Display(Name = "Decrease by amount")]
            DecreaseByAmount = 3,
            [Display(Name = "Increase by %")]
            IncreaseByPercentage = 4,
            [Display(Name = "Decrease by %")]
            DecreaseByPercentage = 5
        }

        public enum StockOperation
        {
            None = 0,
            [Display(Name = "Set to amount")]
            SetTo = 1,
            [Display(Name = "Increase by amount")]
            IncreaseByAmount = 2,
            [Display(Name = "Decrease by amount")]
            DecreaseByAmount = 3
        }
    }
}
