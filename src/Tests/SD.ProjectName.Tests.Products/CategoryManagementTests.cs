using Moq;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.Tests.Products
{
    public class CategoryManagementTests
    {
        [Fact]
        public async Task Create_ShouldAssignDisplayOrderAndNormalizeName()
        {
            var categoryRepo = new Mock<ICategoryRepository>();
            var productRepo = new Mock<IProductRepository>();
            categoryRepo.Setup(r => r.ExistsByName(It.IsAny<string>(), null)).ReturnsAsync(false);
            categoryRepo.Setup(r => r.GetNextDisplayOrder(null)).ReturnsAsync(2);
            CategoryModel? persisted = null;
            categoryRepo.Setup(r => r.Add(It.IsAny<CategoryModel>()))
                .ReturnsAsync((CategoryModel c) =>
                {
                    persisted = c;
                    c.Id = 7;
                    return c;
                });

            var management = new CategoryManagement(categoryRepo.Object, productRepo.Object);

            var created = await management.Create(new CreateCategoryRequest { Name = " Electronics " });

            Assert.NotNull(created);
            Assert.Equal(7, created.Id);
            Assert.Equal("Electronics", created.Name);
            Assert.Equal("ELECTRONICS", created.NormalizedName);
            Assert.Equal(2, created.DisplayOrder);
            Assert.True(created.IsActive);
            categoryRepo.Verify(r => r.Add(It.IsAny<CategoryModel>()), Times.Once);
        }

        [Fact]
        public async Task Rename_ShouldUpdateProductsWithOldCategoryName()
        {
            var categoryRepo = new Mock<ICategoryRepository>();
            var productRepo = new Mock<IProductRepository>();
            var existing = new CategoryModel { Id = 5, Name = "Old", NormalizedName = "OLD" };
            categoryRepo.Setup(r => r.GetById(existing.Id)).ReturnsAsync(existing);
            categoryRepo.Setup(r => r.ExistsByName("NEW", existing.Id)).ReturnsAsync(false);

            var management = new CategoryManagement(categoryRepo.Object, productRepo.Object);

            await management.Rename(existing.Id, "New");

            Assert.Equal("New", existing.Name);
            Assert.Equal("NEW", existing.NormalizedName);
            categoryRepo.Verify(r => r.Update(existing), Times.Once);
            productRepo.Verify(r => r.UpdateCategoryName("Old", "New"), Times.Once);
        }

        [Fact]
        public async Task Delete_ShouldFail_WhenProductsAreAssigned()
        {
            var categoryRepo = new Mock<ICategoryRepository>();
            var productRepo = new Mock<IProductRepository>();
            var existing = new CategoryModel { Id = 9, Name = "Cameras" };
            categoryRepo.Setup(r => r.GetById(existing.Id)).ReturnsAsync(existing);
            categoryRepo.Setup(r => r.HasChildren(existing.Id)).ReturnsAsync(false);
            productRepo.Setup(r => r.AnyWithCategory(existing.Name)).ReturnsAsync(true);

            var management = new CategoryManagement(categoryRepo.Object, productRepo.Object);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => management.Delete(existing.Id));
            Assert.Contains("assigned", ex.Message, StringComparison.OrdinalIgnoreCase);
            categoryRepo.Verify(r => r.Delete(It.IsAny<CategoryModel>()), Times.Never);
        }
    }
}
