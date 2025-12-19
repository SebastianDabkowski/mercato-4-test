using System.ComponentModel.DataAnnotations;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.Modules.Products.Application
{
    public class CategoryManagement
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IProductRepository _productRepository;

        public CategoryManagement(ICategoryRepository categoryRepository, IProductRepository productRepository)
        {
            _categoryRepository = categoryRepository;
            _productRepository = productRepository;
        }

        public async Task<IReadOnlyList<CategoryTreeItem>> GetTree(bool includeInactive = true)
        {
            var categories = await _categoryRepository.GetAll(includeInactive);
            var lookup = categories.ToLookup(c => c.ParentId);

            List<CategoryTreeItem> Build(int? parentId)
            {
                return lookup[parentId]
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.Name)
                    .Select(c => new CategoryTreeItem(
                        c.Id,
                        c.Name,
                        c.IsActive,
                        c.DisplayOrder,
                        c.ParentId,
                        Build(c.Id)))
                    .ToList();
            }

            return Build(null);
        }

        public async Task<IReadOnlyList<CategoryOption>> GetActiveOptions()
        {
            var tree = await GetTree(includeInactive: false);
            var options = new List<CategoryOption>();

            void Flatten(IEnumerable<CategoryTreeItem> nodes, string prefix)
            {
                foreach (var node in nodes)
                {
                    var display = string.IsNullOrEmpty(prefix) ? node.Name : $"{prefix} > {node.Name}";
                    options.Add(new CategoryOption(node.Id, node.Name, display));
                    Flatten(node.Children, display);
                }
            }

            Flatten(tree, string.Empty);
            return options;
        }

        public async Task<CategoryModel> Create(CreateCategoryRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var normalizedName = Normalize(request.Name);
            if (await _categoryRepository.ExistsByName(normalizedName))
            {
                throw new InvalidOperationException("Category name must be unique.");
            }

            if (request.ParentId.HasValue)
            {
                var parent = await _categoryRepository.GetById(request.ParentId.Value);
                if (parent is null)
                {
                    throw new InvalidOperationException("Parent category does not exist.");
                }
            }

            var displayOrder = await _categoryRepository.GetNextDisplayOrder(request.ParentId);
            var category = new CategoryModel
            {
                Name = request.Name.Trim(),
                NormalizedName = normalizedName,
                ParentId = request.ParentId,
                DisplayOrder = displayOrder,
                IsActive = true
            };

            return await _categoryRepository.Add(category);
        }

        public async Task Rename(int categoryId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException("Name is required.", nameof(newName));
            }

            var category = await _categoryRepository.GetById(categoryId)
                           ?? throw new InvalidOperationException("Category not found.");

            var normalized = Normalize(newName);
            if (await _categoryRepository.ExistsByName(normalized, category.Id))
            {
                throw new InvalidOperationException("Category name must be unique.");
            }

            var oldName = category.Name;
            category.Rename(newName);
            await _categoryRepository.Update(category);

            await _productRepository.UpdateCategoryName(oldName, category.Name);
        }

        public async Task Move(int categoryId, bool moveUp)
        {
            var category = await _categoryRepository.GetById(categoryId)
                           ?? throw new InvalidOperationException("Category not found.");

            var siblings = await _categoryRepository.GetByParent(category.ParentId);
            siblings = siblings.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToList();
            var index = siblings.FindIndex(c => c.Id == categoryId);
            if (index < 0)
            {
                return;
            }

            var targetIndex = moveUp ? index - 1 : index + 1;
            if (targetIndex < 0 || targetIndex >= siblings.Count)
            {
                return;
            }

            var target = siblings[targetIndex];
            var originalOrder = category.DisplayOrder;
            category.DisplayOrder = target.DisplayOrder;
            target.DisplayOrder = originalOrder;

            await _categoryRepository.UpdateRange(new[] { category, target });
        }

        public async Task ToggleActive(int categoryId, bool isActive)
        {
            var category = await _categoryRepository.GetById(categoryId)
                           ?? throw new InvalidOperationException("Category not found.");

            category.IsActive = isActive;
            await _categoryRepository.Update(category);
        }

        public async Task Delete(int categoryId)
        {
            var category = await _categoryRepository.GetById(categoryId)
                           ?? throw new InvalidOperationException("Category not found.");

            if (await _categoryRepository.HasChildren(categoryId))
            {
                throw new InvalidOperationException("Remove or reassign child categories first.");
            }

            if (await _productRepository.AnyWithCategory(category.Name))
            {
                throw new InvalidOperationException("Category is assigned to existing products and cannot be deleted.");
            }

            await _categoryRepository.Delete(category);
        }

        private static string Normalize(string name) => name.Trim().ToUpperInvariant();

        public record CategoryTreeItem(int Id, string Name, bool IsActive, int DisplayOrder, int? ParentId, List<CategoryTreeItem> Children);

        public record CategoryOption(int Id, string Name, string DisplayName);
    }

    public class CreateCategoryRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        public int? ParentId { get; set; }
    }
}
