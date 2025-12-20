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
        private static readonly string[] ExportHeaders =
        {
            "Sku","Title","Description","Category","Price","Stock","WeightKg","LengthCm","WidthCm","HeightCm","ShippingMethods","ImageUrls","Status"
        };
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
            var fileName = $"product-catalog-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.{GetExtension(request.Format)}";
            var contentType = ResolveContentType(request.Format);

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

            var contentType = string.IsNullOrWhiteSpace(job.ContentType)
                ? ResolveContentType(ParseFormat(job.Format))
                : job.ContentType;

            return new ExportDownload(job.FileName, contentType, job.FileContent);
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
                    (p.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (!request.IncludeDrafts)
            {
                filtered = filtered.Where(p => p.Status == ProductStatuses.Active);
            }

            return filtered.ToList();
        }

        private static byte[] BuildPayload(IReadOnlyList<ProductModel> products, ExportFormat format)
        {
            if (format == ExportFormat.Xls)
            {
                var xml = BuildSpreadsheetMlDocument(products);
                return Encoding.UTF8.GetBytes(xml);
            }

            var csv = BuildCsvDocument(products);
            return Encoding.UTF8.GetBytes(csv);
        }

        private static string BuildCsvDocument(IReadOnlyList<ProductModel> products)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", ExportHeaders));

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

            return builder.ToString();
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
                .Split(new[] { '\r', '\n', '|', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return string.Join(Environment.NewLine, lines);
        }

        private static string GetExtension(ExportFormat format)
        {
            return format == ExportFormat.Xls ? "xls" : "csv";
        }

        private static string BuildSpreadsheetMlDocument(IReadOnlyList<ProductModel> products)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<?xml version=\"1.0\"?>");
            builder.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            builder.AppendLine("<Worksheet ss:Name=\"Products\">");
            builder.AppendLine("<Table>");

            builder.AppendLine("<Row>");
            foreach (var header in ExportHeaders)
            {
                builder.AppendLine($"<Cell><Data ss:Type=\"String\">{EscapeXml(header)}</Data></Cell>");
            }
            builder.AppendLine("</Row>");

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

                builder.AppendLine("<Row>");
                foreach (var value in values)
                {
                    builder.AppendLine($"<Cell><Data ss:Type=\"String\">{EscapeXml(value)}</Data></Cell>");
                }
                builder.AppendLine("</Row>");
            }

            builder.AppendLine("</Table>");
            builder.AppendLine("</Worksheet>");
            builder.AppendLine("</Workbook>");
            return builder.ToString();
        }

        private static string ResolveContentType(ExportFormat format)
        {
            return format == ExportFormat.Xls ? "application/vnd.ms-excel" : "text/csv";
        }

        private static ExportFormat ParseFormat(string format)
        {
            return Enum.TryParse<ExportFormat>(format, true, out var parsed) ? parsed : ExportFormat.Csv;
        }

        private static string EscapeXml(string value)
        {
            return System.Security.SecurityElement.Escape(value) ?? string.Empty;
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
