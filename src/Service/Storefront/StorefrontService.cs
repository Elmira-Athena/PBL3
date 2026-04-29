using Microsoft.EntityFrameworkCore;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Storefront;

namespace PBL3.Service.Storefront
{
    public class StorefrontService : IStorefrontService
    {
        private readonly HushStoreDbContext _context;

        public StorefrontService(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<List<CategoryMenuResponse>>> GetActiveCategoriesAsync()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.IsVisible && !c.IsDeleted)
                .Select(c => new CategoryMenuResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    IconUrl = c.ImageUrl
                })
                .ToListAsync();

            return ApiResult<List<CategoryMenuResponse>>.Ok(categories);
        }

        public async Task<ApiResult<List<ProductCardResponse>>> GetFeaturedProductsAsync(int? categoryId, int take = 5)
        {
            var query = _context.Products
                .AsNoTracking()
                .Where(p => p.Status == 1 && !p.IsDeleted);

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            // Using Select directly to avoid loading entities into memory
            // We need to calculate prices based on variants
            var products = await query
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    // Get variants that are not deleted
                    ActiveVariants = p.Variants.Where(v => !v.IsDeleted),
                    // Get the main image of the first active variant
                    MainImage = p.Variants.Where(v => !v.IsDeleted)
                        .SelectMany(v => v.Images)
                        .OrderByDescending(i => i.IsMain)
                        .ThenBy(i => i.SortOrder)
                        .FirstOrDefault()
                })
                .Take(take)
                .ToListAsync();

            var result = products.Select(p => 
            {
                var variants = p.ActiveVariants.ToList();
                decimal currentPrice = 0;
                decimal oldPrice = 0;

                if (variants.Any())
                {
                    // CurrentPrice = lowest price among variants
                    currentPrice = variants.Min(v => v.Price);
                    
                    // OldPrice = max of OriginalPrice or currentPrice
                    oldPrice = variants.Max(v => v.OriginalPrice ?? v.Price);
                    if (oldPrice < currentPrice) oldPrice = currentPrice;
                }

                int discountPercent = 0;
                if (oldPrice > 0 && currentPrice < oldPrice)
                {
                    discountPercent = (int)Math.Round((oldPrice - currentPrice) / oldPrice * 100);
                }

                return new ProductCardResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    // Re-generate slug or just use a simple one for now. 
                    // Wait, Product doesn't have Slug, Variant has Slug. 
                    // We'll generate a basic slug from Product Name.
                    Slug = GenerateSlug(p.Name), 
                    ThumbnailUrl = p.MainImage?.ImageUrl,
                    CurrentPrice = currentPrice,
                    OldPrice = oldPrice,
                    DiscountPercent = discountPercent,
                    Rating = 5.0,
                    ReviewCount = 0
                };
            }).ToList();

            return ApiResult<List<ProductCardResponse>>.Ok(result);
        }

        private static string GenerateSlug(string name)
        {
            var normalized = name.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in normalized)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            var noDiacritics = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
            var slug = System.Text.RegularExpressions.Regex.Replace(noDiacritics.ToLower(), @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[\s-]+", "-").Trim('-');
            return slug;
        }
    }
}
