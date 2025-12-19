using System.ComponentModel.DataAnnotations;

namespace SD.ProjectName.Modules.Products.Domain
{
    public class ProductImportJob
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string SellerId { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = ProductImportStatuses.Running;

        public int TotalRows { get; set; }

        public int CreatedCount { get; set; }

        public int UpdatedCount { get; set; }

        public int FailedCount { get; set; }

        public string ErrorReport { get; set; } = string.Empty;
    }

    public static class ProductImportStatuses
    {
        public const string Running = "running";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }
}
