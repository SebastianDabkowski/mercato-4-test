using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;
using SD.ProjectName.Modules.Products.Infrastructure;

namespace SD.ProjectName.Modules.Products.Application
{
    public class ExportProductCatalog
    {
        private const int DefaultBackgroundThreshold = 500;
        private readonly IProductRepository _repository;
        private readonly ProductDbContext _dbContext;
        private readonly int _backgroundThreshold;

        public ExportProductCatalog(IProductRepository repository, ProductDbContext dbContext, int backgroundThreshold = DefaultBackgroundThreshold)
        {
            _repository = repository;
            _dbContext = dbContext;
            _backgroundThreshold = backgroundThreshold > 0 ? backgroundThreshold : DefaultBackgroundThreshold;
        }

        public async Task<ExportResult> ExportAsync(ExportRequest request, string sellerId, CancellationToken cancellationToken = default)
        {
            var products = await _repository.GetBySeller(sellerId, request.IncludeDrafts);
            var filtered = request.ApplyFilters ? ApplyFilters(products, request) : products.ToList();
            var fileName = $"product-catalog-{DateTime.UtcNow:yyyyMMddHHmmss}.{GetExtension(request.Format)}";
            var contentType = request.Format == ExportFormat.Xls ? "application/vnd.ms-excel" : "text/csv";

            if (filtered.Count >= _backgroundThreshold)
            {
                var payload = BuildPayload(filtered, request.Format);
                var job = new ProductExportJob
                {
                    Id = Guid.NewGuid(),
                    SellerId = sellerId,
                    Status = ProductExportStatuses.Completed,
                    FileName = fileName,
                    Format = request.Format.ToString().ToLowerInvariant(),
                    ContentType = contentType,
                    TotalRows = filtered.Count,
                    FileContent = payload,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow
                };

                _dbContext.ExportJobs.Add(job);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return ExportResult.Background(job.Id, BuildDownloadLink(job.Id), filtered.Count);
            }

            var fileContent = BuildPayload(filtered, request.Format);
            return ExportResult.Inline(fileName, contentType, fileContent, filtered.Count);
        }

        public async Task<ExportDownload?> DownloadAsync(Guid jobId, string sellerId, CancellationToken cancellationToken = default)
        {
            var job = await _dbContext.ExportJobs
                .FirstOrDefaultAsync(j => j.Id == jobId && j.SellerId == sellerId, cancellationToken);

            if (job is null || job.Status != ProductExportStatuses.Completed || job.FileContent is null)
            {
                return null;
            }

            return new ExportDownload(job.FileName, string.IsNullOrWhiteSpace(job.ContentType) ? "text/csv" : job.ContentType, job.FileContent);
        }

        public async Task<IReadOnlyList<ProductExportJob>> GetHistoryAsync(string sellerId, int take = 20, CancellationToken cancellationToken = default)
        {
            return await _dbContext.ExportJobs
                .Where(j => j.SellerId == sellerId)
                .OrderByDescending(j => j.CreatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        private static List<ProductModel> ApplyFilters(IEnumerable<ProductModel> products, ExportRequest request)
        {
            var filtered = products;

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                var normalizedCategory = request.Category.Trim();
                filtered = filtered.Where(p => p.Category.Equals(normalizedCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim();
                filtered = filtered.Where(p =>
                    p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Sku.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (!request.IncludeDrafts)
            {
                filtered = filtered.Where(p => p.Status == ProductStatuses.Active);
            }

            return filtered.ToList();
        }

        private static byte[] BuildPayload(IReadOnlyList<ProductModel> products, ExportFormat format)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Sku,Title,Description,Category,Price,Stock,WeightKg,LengthCm,WidthCm,HeightCm,ShippingMethods,ImageUrls,Status");

            foreach (var product in products.OrderBy(p => p.Name))
            {
                var values = new[]
                {
                    product.Sku,
                    product.Name,
                    product.Description,
                    product.Category,
                    product.Price.ToString(CultureInfo.InvariantCulture),
                    product.Stock.ToString(CultureInfo.InvariantCulture),
                    product.WeightKg.ToString(CultureInfo.InvariantCulture),
                    product.LengthCm.ToString(CultureInfo.InvariantCulture),
                    product.WidthCm.ToString(CultureInfo.InvariantCulture),
                    product.HeightCm.ToString(CultureInfo.InvariantCulture),
                    NormalizeMultiline(product.ShippingMethods),
                    NormalizeMultiline(product.ImageUrls),
                    product.Status
                };

                var line = string.Join(",", values.Select(Escape));
                builder.AppendLine(line);
            }

            var content = builder.ToString();
            return Encoding.UTF8.GetBytes(content);
        }

        private static string Escape(string? value)
        {
            var safeValue = value ?? string.Empty;
            if (safeValue.Contains('"'))
            {
                safeValue = safeValue.Replace("\"", "\"\"");
            }

            if (safeValue.Contains(',') || safeValue.Contains('\n') || safeValue.Contains('\r'))
            {
                return $"\"{safeValue}\"";
            }

            return safeValue;
        }

        private static string NormalizeMultiline(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var lines = value
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return string.Join(Environment.NewLine, lines);
        }

        private static string GetExtension(ExportFormat format)
        {
            return format == ExportFormat.Xls ? "xls" : "csv";
        }

        private static string BuildDownloadLink(Guid jobId)
        {
            return $"/products/exports/{jobId}";
        }

        public record ExportRequest
        {
            public ExportFormat Format { get; set; } = ExportFormat.Csv;
            public bool ApplyFilters { get; set; } = true;
            public string? Category { get; set; }
            public string? Search { get; set; }
            public bool IncludeDrafts { get; set; } = true;
        }

        public enum ExportFormat
        {
            Csv = 0,
            Xls = 1
        }

        public record ExportResult
        {
            private ExportResult()
            {
            }

            public string FileName { get; private init; } = string.Empty;
            public string ContentType { get; private init; } = string.Empty;
            public byte[]? FileContent { get; private init; }
            public bool QueuedAsJob { get; private init; }
            public Guid? JobId { get; private init; }
            public string? DownloadLink { get; private init; }
            public int ExportedCount { get; private init; }

            public static ExportResult Inline(string fileName, string contentType, byte[] fileContent, int exportedCount)
            {
                return new ExportResult
                {
                    FileName = fileName,
                    ContentType = contentType,
                    FileContent = fileContent,
                    ExportedCount = exportedCount
                };
            }

            public static ExportResult Background(Guid jobId, string downloadLink, int exportedCount)
            {
                return new ExportResult
                {
                    JobId = jobId,
                    DownloadLink = downloadLink,
                    QueuedAsJob = true,
                    ExportedCount = exportedCount
                };
            }
        }

        public record ExportDownload(string FileName, string ContentType, byte[] FileContent);
    }
}
