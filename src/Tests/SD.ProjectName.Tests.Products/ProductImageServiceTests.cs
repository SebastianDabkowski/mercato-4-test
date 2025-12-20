using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.FileProviders;
using SD.ProjectName.WebApp.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace SD.ProjectName.Tests.Products;

public class ProductImageServiceTests
{
    [Fact]
    public void ValidateUploads_ShouldRejectUnsupportedExtension()
    {
        var env = new FakeWebHostEnvironment();
        var service = new ProductImageService(env);
        var modelState = new ModelStateDictionary();

        var file = CreateFormFile("bad.txt", new byte[10]);

        service.ValidateUploads(new[] { file }, modelState, "Uploads");

        Assert.True(modelState.ContainsKey("Uploads"));
        Assert.Contains(modelState["Uploads"]!.Errors, e => e.ErrorMessage.Contains("not supported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MergeAndOrderImages_ShouldPrioritizeRequestedMain()
    {
        var env = new FakeWebHostEnvironment();
        var service = new ProductImageService(env);

        var merged = service.MergeAndOrderImages(new[] { "one", "two" }, Array.Empty<string>(), "two");
        var lines = merged.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("two", lines[0]);
        Assert.Contains("one", lines);
    }

    [Fact]
    public async Task SaveUploadsAsync_ShouldPersistVariants()
    {
        var env = new FakeWebHostEnvironment();
        var service = new ProductImageService(env);

        var imageBytes = CreatePngBytes();
        var file = CreateFormFile("photo.png", imageBytes);

        var saved = await service.SaveUploadsAsync(42, new[] { file });

        try
        {
            Assert.Single(saved);
            var largeUrl = saved[0];
            Assert.Contains("/uploads/products/42/", largeUrl);

            var webRoot = env.WebRootPath!;
            var largePath = Path.Combine(webRoot, largeUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));

            Assert.True(File.Exists(largePath));
            Assert.True(File.Exists(largePath.Replace("-lg.webp", "-md.webp")));
            Assert.True(File.Exists(largePath.Replace("-lg.webp", "-sm.webp")));
        }
        finally
        {
            if (Directory.Exists(env.WebRootPath!))
            {
                Directory.Delete(env.WebRootPath!, true);
            }
        }
    }

    private static IFormFile CreateFormFile(string fileName, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName);
    }

    private static byte[] CreatePngBytes()
    {
        using var image = new Image<Rgba32>(64, 64, new Rgba32(200, 200, 200));
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment()
        {
            var root = Path.Combine(Path.GetTempPath(), "product-image-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            WebRootPath = root;
            ContentRootPath = root;
            WebRootFileProvider = new PhysicalFileProvider(root);
            ContentRootFileProvider = new PhysicalFileProvider(root);
        }

        public string ApplicationName { get; set; } = "TestApp";

        public IFileProvider WebRootFileProvider { get; set; } = null!;

        public string WebRootPath { get; set; }

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
