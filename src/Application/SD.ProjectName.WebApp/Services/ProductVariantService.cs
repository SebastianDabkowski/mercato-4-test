using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SD.ProjectName.WebApp.Services
{
    public class ProductVariantService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public IReadOnlyList<ProductVariant> Parse(string? variantsJson)
        {
            if (string.IsNullOrWhiteSpace(variantsJson))
            {
                return Array.Empty<ProductVariant>();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<ProductVariant>>(variantsJson, SerializerOptions);
                return parsed?.Where(v => v?.Attributes?.Any() == true).ToList() ?? Array.Empty<ProductVariant>();
            }
            catch
            {
                return Array.Empty<ProductVariant>();
            }
        }

        public Dictionary<string, List<string>> BuildAttributeOptions(IReadOnlyList<ProductVariant> variants)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var variant in variants)
            {
                foreach (var kvp in variant.Attributes)
                {
                    if (!result.TryGetValue(kvp.Key, out var values))
                    {
                        values = new List<string>();
                        result[kvp.Key] = values;
                    }

                    if (!values.Contains(kvp.Value, StringComparer.OrdinalIgnoreCase))
                    {
                        values.Add(kvp.Value);
                    }
                }
            }

            return result;
        }

        public ProductVariant? SelectVariant(IReadOnlyList<ProductVariant> variants, IDictionary<string, string> selection)
        {
            if (variants is null || selection is null)
            {
                return null;
            }

            return variants.FirstOrDefault(v =>
                selection.All(sel => v.Attributes.TryGetValue(sel.Key, out var val) &&
                                     string.Equals(val, sel.Value, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public class ProductVariant
    {
        public string Sku { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public decimal? Price { get; set; }
        public int Stock { get; set; }
        public string? ImageUrl { get; set; }
    }
}
