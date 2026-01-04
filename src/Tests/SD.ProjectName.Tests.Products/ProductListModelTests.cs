using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;
using SD.ProjectName.WebApp.Pages.Products;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.Tests.Products
{
    public class ProductListModelTests
    {
        [Fact]
        public async Task OnGet_ShouldAggregateProductsFromSubcategories_WhenParentCategorySelected()
        {
            var categories = new List<CategoryModel>
            {
                new() { Id = 1, Name = "Books", NormalizedName = "BOOKS", DisplayOrder = 0, IsActive = true },
                new() { Id = 2, Name = "SciFi", NormalizedName = "SCIFI", ParentId = 1, DisplayOrder = 0, IsActive = true }
            };

            var categoryRepo = new Mock<ICategoryRepository>();
            categoryRepo.Setup(r => r.GetAll(false)).ReturnsAsync(categories);

            var productRepo = new Mock<IProductRepository>();
            productRepo.Setup(r => r.GetList("Books")).ReturnsAsync(new List<ProductModel>());
            var sciFi = new ProductModel
            {
                Id = 42,
                Name = "Dune",
                Category = "SciFi",
                Status = ProductStatuses.Active,
                Price = 10,
                Stock = 5
            };
            productRepo.Setup(r => r.GetList("SciFi")).ReturnsAsync(new List<ProductModel> { sciFi });

            var categoryManagement = new CategoryManagement(categoryRepo.Object, productRepo.Object);
            var getProducts = new GetProducts(productRepo.Object);

            var env = new Mock<IWebHostEnvironment>();
            env.SetupProperty(e => e.WebRootPath, Path.GetTempPath());
            env.SetupProperty(e => e.ContentRootPath, Path.GetTempPath());
            var imageService = new ProductImageService(env.Object);

            var model = new ListModel(NullLogger<ListModel>.Instance, getProducts, imageService, categoryManagement)
            {
                Category = "Books"
            };

            await model.OnGetAsync();

            Assert.Equal("Books", model.SelectedCategory?.Name);
            Assert.True(model.AggregatedFromSubcategories);
            Assert.Single(model.Products);
            Assert.Equal(sciFi.Id, model.Products.Single().Product.Id);
            productRepo.Verify(r => r.GetList("SciFi"), Times.Once);
            productRepo.Verify(r => r.GetList("Books"), Times.Once);
        }
    }
}
