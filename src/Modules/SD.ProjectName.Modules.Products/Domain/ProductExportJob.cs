using System.ComponentModel.DataAnnotations;

namespace SD.ProjectName.Modules.Products.Domain
{
    public class ProductExportJob
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Format { get; set; } = string.Empty;

        [MaxLength(255)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        public string SellerId { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? CompletedAt { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = ProductExportStatuses.Running;

        public int TotalRows { get; set; }

        public byte[]? FileContent { get; set; }

        public string Error { get; set; } = string.Empty;
    }

    public static class ProductExportStatuses
    {
        public const string Running = "running";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }
}
