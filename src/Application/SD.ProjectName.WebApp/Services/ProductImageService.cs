using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace SD.ProjectName.WebApp.Services;

public enum ImageVariant
{
    Large,
    Medium,
    Thumbnail
}

public record ProductImageView(string LargeUrl, string MediumUrl, string ThumbnailUrl, bool IsMain);

public class ProductImageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment;

    public ProductImageService(IWebHostEnvironment environment)
    {
        _environment = environment;

        if (string.IsNullOrWhiteSpace(_environment.WebRootPath))
        {
            _environment.WebRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
        }
    }

    public IReadOnlyList<string> Parse(string? imageUrls)
    {
        if (string.IsNullOrWhiteSpace(imageUrls))
        {
            return Array.Empty<string>();
        }

        var normalized = new List<string>();
        foreach (var line in imageUrls.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (normalized.Any(i => string.Equals(i, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            normalized.Add(trimmed);
        }

        return normalized;
    }

    public IReadOnlyList<ProductImageView> BuildViews(string? imageUrls, string? preferredMain = null)
    {
        var items = Parse(imageUrls);
        if (string.IsNullOrWhiteSpace(preferredMain))
        {
            preferredMain = items.FirstOrDefault();
        }

        return items
            .Select(url => new ProductImageView(
                url,
                GetVariant(url, ImageVariant.Medium),
                GetVariant(url, ImageVariant.Thumbnail),
                string.Equals(url, preferredMain, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public void ValidateUploads(IEnumerable<IFormFile> uploads, ModelStateDictionary modelState, string fieldName)
    {
        foreach (var upload in uploads ?? Array.Empty<IFormFile>())
        {
            if (upload.Length == 0)
            {
                modelState.AddModelError(fieldName, $"{upload.FileName} is empty. Upload a valid image file.");
                continue;
            }

            if (upload.Length > MaxFileSizeBytes)
            {
                modelState.AddModelError(fieldName, $"{upload.FileName} exceeds the {MaxFileSizeBytes / (1024 * 1024)} MB limit.");
            }

            var extension = Path.GetExtension(upload.FileName);
            if (!AllowedExtensions.Contains(extension))
            {
                modelState.AddModelError(fieldName, $"{upload.FileName} is not supported. Allowed formats: JPG, PNG, WebP.");
            }
        }
    }

    public async Task<List<string>> SaveUploadsAsync(int productId, IEnumerable<IFormFile> uploads, CancellationToken cancellationToken = default)
    {
        var saved = new List<string>();
        var uploadList = uploads?.ToList() ?? new List<IFormFile>();
        if (!uploadList.Any())
        {
            return saved;
        }

        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var productFolder = Path.Combine(webRoot, "uploads", "products", productId.ToString());
        Directory.CreateDirectory(productFolder);

        foreach (var upload in uploadList)
        {
            await using var stream = upload.OpenReadStream();
            using var image = await Image.LoadAsync(stream, cancellationToken);

            var baseName = $"{Sanitize(Path.GetFileNameWithoutExtension(upload.FileName))}-{Guid.NewGuid():N}";

            var largePath = Path.Combine(productFolder, $"{baseName}-lg.webp");
            var mediumPath = Path.Combine(productFolder, $"{baseName}-md.webp");
            var thumbnailPath = Path.Combine(productFolder, $"{baseName}-sm.webp");

            await SaveVariantAsync(image, largePath, 1600, 1600, cancellationToken);
            await SaveVariantAsync(image, mediumPath, 1000, 1000, cancellationToken);
            await SaveVariantAsync(image, thumbnailPath, 320, 320, cancellationToken);

            var relativeLarge = "/" + Path.GetRelativePath(webRoot, largePath).Replace("\\", "/");
            saved.Add(relativeLarge);
        }

        return saved;
    }

    public string MergeAndOrderImages(IEnumerable<string> existingImages, IEnumerable<string> newUploadUrls, string? requestedMain)
    {
        var uploadList = newUploadUrls?.ToList() ?? new List<string>();
        var combined = NormalizeImages(existingImages.Concat(uploadList));
        if (!combined.Any())
        {
            return string.Empty;
        }

        string? main = null;
        if (!string.IsNullOrWhiteSpace(requestedMain))
        {
            main = combined.FirstOrDefault(i => string.Equals(i, requestedMain, StringComparison.OrdinalIgnoreCase));
        }

        if (main is null)
        {
            main = uploadList.FirstOrDefault();
        }

        return BuildMultiline(combined, main ?? combined.First());
    }

    public string BuildMultiline(IEnumerable<string> images, string? preferredMain = null)
    {
        var normalized = NormalizeImages(images, preferredMain);
        return string.Join(Environment.NewLine, normalized);
    }

    public string GetMainImage(string? imageUrls)
    {
        var parsed = Parse(imageUrls);
        return parsed.FirstOrDefault() ?? string.Empty;
    }

    public string GetVariant(string url, ImageVariant variant)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var extension = Path.GetExtension(url);
        var fileName = Path.GetFileNameWithoutExtension(url);
        var directory = url[..(url.Length - Path.GetFileName(url).Length)];

        var baseName = StripVariant(fileName);
        if (string.Equals(baseName, fileName, StringComparison.OrdinalIgnoreCase) &&
            !url.Contains("/uploads/products/", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        if (string.IsNullOrWhiteSpace(baseName))
        {
            return url;
        }

        var suffix = variant switch
        {
            ImageVariant.Medium => "-md",
            ImageVariant.Thumbnail => "-sm",
            _ => "-lg"
        };

        return $"{directory}{baseName}{suffix}{extension}";
    }

    private static async Task SaveVariantAsync(Image image, string path, int maxWidth, int maxHeight, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var clone = image.Clone(ctx =>
        {
            ctx.AutoOrient();
            ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxWidth, maxHeight)
            });
        });

        await clone.SaveAsync(path, new WebpEncoder
        {
            Quality = 80
        }, cancellationToken);
    }

    private static string StripVariant(string fileName)
    {
        foreach (var suffix in new[] { "-lg", "-md", "-sm" })
        {
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^suffix.Length];
            }
        }

        return fileName;
    }

    private static string Sanitize(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        return cleaned.Length == 0 ? "image" : cleaned;
    }

    private static List<string> NormalizeImages(IEnumerable<string> images, string? preferredMain = null)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var image in images ?? Array.Empty<string>())
        {
            var trimmed = image?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredMain))
        {
            var main = normalized.FirstOrDefault(i => string.Equals(i, preferredMain, StringComparison.OrdinalIgnoreCase));
            if (main is not null)
            {
                normalized.Remove(main);
                normalized.Insert(0, main);
            }
        }

        return normalized;
    }
}
