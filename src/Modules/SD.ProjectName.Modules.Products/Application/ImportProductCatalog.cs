using System.Globalization;
using Microsoft.EntityFrameworkCore;
using System.Text;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;
using SD.ProjectName.Modules.Products.Infrastructure;

namespace SD.ProjectName.Modules.Products.Application
{
    public class ImportProductCatalog
    {
        private static readonly string[] RequiredColumns = ["sku", "title", "price", "stock", "category"];
        private readonly IProductRepository _repository;
        private readonly ProductDbContext _dbContext;

        public ImportProductCatalog(IProductRepository repository, ProductDbContext dbContext)
        {
            _repository = repository;
            _dbContext = dbContext;
        }

        public async Task<ImportPreviewResult> PreviewAsync(Stream fileStream, string fileName, string sellerId, CancellationToken cancellationToken = default)
        {
            var parseResult = await ParseAsync(fileStream, fileName, sellerId, cancellationToken);
            return BuildPreview(parseResult);
        }

        public async Task<ProductImportJob> ImportAsync(Stream fileStream, string fileName, string sellerId, CancellationToken cancellationToken = default)
        {
            var job = new ProductImportJob
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                SellerId = sellerId,
                Status = ProductImportStatuses.Running,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.ImportJobs.Add(job);
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var parseResult = await ParseAsync(fileStream, fileName, sellerId, cancellationToken);

                if (parseResult.HasFatalErrors)
                {
                    job.Status = ProductImportStatuses.Failed;
                    job.TotalRows = 0;
                    job.FailedCount = parseResult.Errors.Count;
                    job.ErrorReport = BuildErrorReport(parseResult.Errors);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return job;
                }

                var preview = BuildPreview(parseResult);

                var validRows = parseResult.Rows.Where(r => !r.Errors.Any()).ToList();
                var toCreate = new List<ProductModel>();
                var toUpdate = new List<ProductModel>();

                foreach (var row in validRows)
                {
                    if (parseResult.ExistingBySku.TryGetValue(row.Sku, out var existing))
                    {
                        ApplyRow(existing, row);
                        toUpdate.Add(existing);
                        continue;
                    }

                    toCreate.Add(new ProductModel
                    {
                        Sku = row.Sku,
                        Name = row.Title,
                        Description = row.Description ?? string.Empty,
                        Category = row.Category,
                        Price = row.Price ?? 0,
                        Stock = row.Stock ?? 0,
                        ImageUrls = row.ImageUrls ?? string.Empty,
                        ShippingMethods = row.ShippingMethods ?? string.Empty,
                        WeightKg = row.WeightKg ?? 0,
                        LengthCm = row.LengthCm ?? 0,
                        WidthCm = row.WidthCm ?? 0,
                        HeightCm = row.HeightCm ?? 0,
                        SellerId = sellerId,
                        Status = ProductStatuses.Draft
                    });
                }

                if (toUpdate.Any())
                {
                    await _repository.UpdateRange(toUpdate);
                }

                if (toCreate.Any())
                {
                    await _repository.AddRange(toCreate);
                }

                job.TotalRows = preview.TotalRows;
                job.CreatedCount = toCreate.Count;
                job.UpdatedCount = toUpdate.Count;
                job.FailedCount = preview.Errors.Count;
                job.ErrorReport = BuildErrorReport(preview.Errors);
                job.Status = ProductImportStatuses.Completed;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return job;
            }
            catch (Exception ex)
            {
                job.Status = ProductImportStatuses.Failed;
                job.ErrorReport = ex.Message;
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw;
            }
        }

        public async Task<IReadOnlyList<ProductImportJob>> GetHistoryAsync(string sellerId, int take = 20, CancellationToken cancellationToken = default)
        {
            return await _dbContext.ImportJobs
                .Where(j => j.SellerId == sellerId)
                .OrderByDescending(j => j.CreatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<ProductImportJob?> GetJobAsync(Guid jobId, string sellerId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.ImportJobs
                .FirstOrDefaultAsync(j => j.Id == jobId && j.SellerId == sellerId, cancellationToken);
        }

        private static ImportPreviewResult BuildPreview(ImportParseResult parseResult)
        {
            var validRows = parseResult.Rows.Where(r => !r.Errors.Any()).ToList();
            var rowsToCreate = validRows.Count(r => !r.IsUpdate);
            var rowsToUpdate = validRows.Count(r => r.IsUpdate);

            return new ImportPreviewResult(
                parseResult.TotalRows,
                rowsToCreate,
                rowsToUpdate,
                parseResult.Errors.Count,
                parseResult.Rows.Select(r => new ImportPreviewRow(r.RowNumber, r.Sku, r.Title, r.IsUpdate, r.Errors.ToList())).ToList(),
                parseResult.Errors.ToList(),
                parseResult.HasFatalErrors);
        }

        private static void ApplyRow(ProductModel existing, ImportRow row)
        {
            existing.Name = row.Title;
            existing.Description = row.Description ?? string.Empty;
            existing.Category = row.Category;
            existing.Price = row.Price ?? existing.Price;
            existing.Stock = row.Stock ?? existing.Stock;
            existing.ImageUrls = row.ImageUrls ?? string.Empty;
            existing.ShippingMethods = row.ShippingMethods ?? string.Empty;
            existing.WeightKg = row.WeightKg ?? 0;
            existing.LengthCm = row.LengthCm ?? 0;
            existing.WidthCm = row.WidthCm ?? 0;
            existing.HeightCm = row.HeightCm ?? 0;
            existing.Sku = row.Sku;
        }

        private async Task<ImportParseResult> ParseAsync(Stream fileStream, string fileName, string sellerId, CancellationToken cancellationToken)
        {
            if (fileStream.CanSeek)
            {
                fileStream.Seek(0, SeekOrigin.Begin);
            }

            using var reader = new StreamReader(fileStream, Encoding.UTF8, true, leaveOpen: true);
            var content = await reader.ReadToEndAsync(cancellationToken);
            return await ParseContentAsync(content, fileName, sellerId, cancellationToken);
        }

        private async Task<ImportParseResult> ParseContentAsync(string content, string fileName, string sellerId, CancellationToken cancellationToken)
        {
            var errors = new List<ImportError>();
            var rows = new List<ImportRow>();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var totalRows = Math.Max(0, lines.Length - 1);

            if (lines.Length == 0)
            {
                errors.Add(new ImportError(0, "The uploaded file is empty."));
                return new ImportParseResult(rows, errors, new Dictionary<string, ProductModel>(StringComparer.OrdinalIgnoreCase), true, totalRows);
            }

            var header = SplitCsvLine(lines[0]);
            var columnPositions = BuildColumnMap(header);
            var missingColumns = RequiredColumns.Where(rc => !columnPositions.ContainsKey(rc)).ToList();
            if (missingColumns.Any())
            {
                errors.Add(new ImportError(0, $"Missing required columns: {string.Join(", ", missingColumns)}."));
                return new ImportParseResult(rows, errors, new Dictionary<string, ProductModel>(StringComparer.OrdinalIgnoreCase), true, totalRows);
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var values = SplitCsvLine(lines[i]);
                var row = BuildRow(values, columnPositions, i + 1);
                ValidateRow(row, errors);
                rows.Add(row);
            }

            var validSkus = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Sku))
                .Select(r => r.Sku)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existing = await _repository.GetBySellerAndSkus(sellerId, validSkus);
            var existingLookup = existing.ToDictionary(p => p.Sku, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Where(r => !string.IsNullOrWhiteSpace(r.Sku)))
            {
                if (existingLookup.TryGetValue(row.Sku, out var existingProduct))
                {
                    row.IsUpdate = true;
                    if (string.Equals(existingProduct.Status, ProductStatuses.Archived, StringComparison.OrdinalIgnoreCase))
                    {
                        var error = $"Product with SKU {row.Sku} is archived and cannot be updated.";
                        row.Errors.Add(error);
                        errors.Add(new ImportError(row.RowNumber, error));
                    }
                }
            }

            return new ImportParseResult(rows, errors, existingLookup, false, totalRows);
        }

        private static ImportRow BuildRow(IReadOnlyList<string> values, Dictionary<string, int> map, int rowNumber)
        {
            string Get(string key)
            {
                if (map.TryGetValue(key, out var idx) && idx < values.Count)
                {
                    return values[idx].Trim().Trim('"');
                }

                return string.Empty;
            }

            return new ImportRow
            {
                RowNumber = rowNumber,
                Sku = Get("sku"),
                Title = Get("title"),
                Description = Get("description"),
                Category = Get("category"),
                PriceRaw = Get("price"),
                StockRaw = Get("stock"),
                WeightRaw = Get("weightkg"),
                LengthRaw = Get("lengthcm"),
                WidthRaw = Get("widthcm"),
                HeightRaw = Get("heightcm"),
                ShippingMethods = NormalizeMultiline(Get("shippingmethods")),
                ImageUrls = NormalizeMultiline(Get("imageurls"))
            };
        }

        private static void ValidateRow(ImportRow row, List<ImportError> errors)
        {
            if (string.IsNullOrWhiteSpace(row.Sku))
            {
                AddError("SKU is required.", row, errors);
            }

            if (string.IsNullOrWhiteSpace(row.Title))
            {
                AddError("Title is required.", row, errors);
            }

            if (string.IsNullOrWhiteSpace(row.Category))
            {
                AddError("Category is required.", row, errors);
            }

            if (!TryParseDecimal(row.PriceRaw, out var price) || price <= 0)
            {
                AddError("Price must be a positive number.", row, errors);
            }
            else
            {
                row.Price = price;
            }

            if (!TryParseInt(row.StockRaw, out var stock) || stock < 0)
            {
                AddError("Stock must be zero or a positive whole number.", row, errors);
            }
            else
            {
                row.Stock = stock;
            }

            if (TryParseDecimal(row.WeightRaw, out var weight))
            {
                row.WeightKg = weight;
            }

            if (TryParseDecimal(row.LengthRaw, out var length))
            {
                row.LengthCm = length;
            }

            if (TryParseDecimal(row.WidthRaw, out var width))
            {
                row.WidthCm = width;
            }

            if (TryParseDecimal(row.HeightRaw, out var height))
            {
                row.HeightCm = height;
            }
        }

        private static void AddError(string message, ImportRow row, List<ImportError> errors)
        {
            row.Errors.Add(message);
            errors.Add(new ImportError(row.RowNumber, message));
        }

        private static string BuildErrorReport(IEnumerable<ImportError> errors)
        {
            var lines = errors.Select(e => $"Row {e.RowNumber}: {e.Message}");
            return string.Join(Environment.NewLine, lines);
        }

        private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> headers)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                var normalized = headers[i].Trim().Trim('"').Replace(" ", string.Empty).ToLowerInvariant();
                if (!map.ContainsKey(normalized))
                {
                    map[normalized] = i;
                }
            }

            return map;
        }

        private static IReadOnlyList<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(line))
            {
                return result;
            }

            var current = new StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"' && (i == 0 || line[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            result.Add(current.ToString());
            return result;
        }

        private static bool TryParseDecimal(string? value, out decimal number)
        {
            var normalized = (value ?? string.Empty).Replace(" ", string.Empty).Replace(",", ".");
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
        }

        private static bool TryParseInt(string? value, out int number)
        {
            var normalized = (value ?? string.Empty).Replace(" ", string.Empty);
            return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }

        private static string NormalizeMultiline(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var lines = value.Split(new[] { '\r', '\n', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l));

            return string.Join(Environment.NewLine, lines);
        }

        internal record ImportParseResult(
            List<ImportRow> Rows,
            List<ImportError> Errors,
            Dictionary<string, ProductModel> ExistingBySku,
            bool HasFatalErrors,
            int TotalRows);

        public record ImportError(int RowNumber, string Message);

        public record ImportPreviewRow(int RowNumber, string Sku, string Title, bool WillUpdate, IReadOnlyList<string> Errors);

        public record ImportPreviewResult(
            int TotalRows,
            int CreateCount,
            int UpdateCount,
            int ErrorCount,
            IReadOnlyList<ImportPreviewRow> Rows,
            IReadOnlyList<ImportError> Errors,
            bool HasFatalErrors);

        internal class ImportRow
        {
            public int RowNumber { get; set; }
            public string Sku { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string Category { get; set; } = string.Empty;
            public string? ImageUrls { get; set; }
            public string? ShippingMethods { get; set; }
            public string? PriceRaw { get; set; }
            public string? StockRaw { get; set; }
            public string? WeightRaw { get; set; }
            public string? LengthRaw { get; set; }
            public string? WidthRaw { get; set; }
            public string? HeightRaw { get; set; }
            public decimal? Price { get; set; }
            public int? Stock { get; set; }
            public decimal? WeightKg { get; set; }
            public decimal? LengthCm { get; set; }
            public decimal? WidthCm { get; set; }
            public decimal? HeightCm { get; set; }
            public bool IsUpdate { get; set; }
            public List<string> Errors { get; } = new();
        }
    }
}
