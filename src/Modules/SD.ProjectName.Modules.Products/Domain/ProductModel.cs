using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SD.ProjectName.Modules.Products.Domain
{
    public class ProductModel
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = ProductStatuses.Draft;
        public string SellerId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrls { get; set; } = string.Empty;
        public decimal WeightKg { get; set; }
        public decimal LengthCm { get; set; }
        public decimal WidthCm { get; set; }
        public decimal HeightCm { get; set; }
        public string ShippingMethods { get; set; } = string.Empty;
    }

    public static class ProductStatuses
    {
        public const string Draft = "draft";
        public const string Active = "active";
        public const string Suspended = "suspended";
        public const string Archived = "archived";
    }
}
