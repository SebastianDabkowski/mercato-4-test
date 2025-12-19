using System.ComponentModel.DataAnnotations;

namespace SD.ProjectName.Modules.Products.Domain
{
    public class CategoryModel
    {
        public int Id { get; set; }

        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string NormalizedName { get; set; } = string.Empty;

        public int? ParentId { get; set; }

        public CategoryModel? Parent { get; set; }

        public List<CategoryModel> Children { get; set; } = new();

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public void Rename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException("Category name is required.", nameof(newName));
            }

            Name = newName.Trim();
            NormalizedName = Name.ToUpperInvariant();
        }
    }
}
