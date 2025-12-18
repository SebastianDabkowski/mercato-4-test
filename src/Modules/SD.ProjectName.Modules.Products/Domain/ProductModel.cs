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
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = ProductStatuses.Draft;
        public string SellerId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public static class ProductStatuses
    {
        public const string Draft = "draft";
        public const string Active = "active";
    }
}
