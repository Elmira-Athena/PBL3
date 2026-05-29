using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PBL3.Service.Products
{
    /// <summary>
    /// Sinh URL slug chuẩn SEO cho Product / Variant (loại bỏ dấu tiếng Việt).
    /// </summary>
    internal static class ProductSlugHelper
    {
        /// <summary>
        /// Sinh slug cho 1 variant: kết hợp tên sản phẩm + SKU để đảm bảo unique.
        /// </summary>
        public static string GenerateVariantSlug(string productName, string sku)
            => Slugify($"{productName} {sku}");

        /// <summary>
        /// Sinh slug cho 1 product từ tên sản phẩm.
        /// </summary>
        public static string GenerateProductSlug(string productName)
            => Slugify(productName);

        private static string Slugify(string input)
        {
            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            var noDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);
            var slug = Regex.Replace(noDiacritics.ToLower(), @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"[\s-]+", "-").Trim('-');
            return slug;
        }
    }
}
