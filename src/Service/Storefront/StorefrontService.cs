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
                    p.Slug,
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
                    Slug = p.Slug, 
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

        public async Task<ApiResult<ProductDetailResponse>> GetProductDetailAsync(string slug)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Manufacturer)
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Images)
                .Where(p => p.Slug == slug && p.Status == 1 && !p.IsDeleted)
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return ApiResult<ProductDetailResponse>.Fail("Không tìm thấy sản phẩm yêu cầu.");
            }

            var activeVariants = product.Variants.ToList();

            var images = activeVariants
                .SelectMany(v => v.Images)
                .OrderByDescending(i => i.IsMain)
                .ThenBy(i => i.SortOrder)
                .Select(i => i.ImageUrl)
                .Distinct()
                .ToList();

            var variantResponses = activeVariants.Select(v => new StorefrontVariantResponse
            {
                Id = v.Id,
                VariantName = v.VariantName,
                Price = v.Price,
                IsAvailable = v.StockQuantity > 0,
                Specifications = v.Specifications ?? new()
            }).ToList();

            var defaultVariant = activeVariants.OrderBy(v => v.Price).FirstOrDefault();
            string? specsJson = null;
            List<string> shortFeatures = new();

            if (defaultVariant?.Specifications != null && defaultVariant.Specifications.Any())
            {
                specsJson = System.Text.Json.JsonSerializer.Serialize(defaultVariant.Specifications);
                shortFeatures = defaultVariant.Specifications.Select(kvp => $"{kvp.Key}: {kvp.Value}").ToList();
            }

            var response = new ProductDetailResponse
            {
                Id = product.Id,
                Name = product.Name,
                Slug = product.Slug,
                ManufacturerName = product.Manufacturer?.Name ?? string.Empty,
                Description = product.Description,
                Specifications = specsJson,
                Images = images,
                Variants = variantResponses,
                ShortFeatures = shortFeatures,
                Rating = 5.0,
                ReviewCount = 13
            };

            return ApiResult<ProductDetailResponse>.Ok(response);
        }

        public async Task<ApiResult<List<ProductCardResponse>>> GetRelatedProductsAsync(string slug)
        {
            var currentProduct = await _context.Products
                .AsNoTracking()
                .Where(p => p.Slug == slug && p.Status == 1 && !p.IsDeleted)
                .Select(p => new { p.Id, p.CategoryId })
                .FirstOrDefaultAsync();

            if (currentProduct == null)
            {
                return ApiResult<List<ProductCardResponse>>.Ok(new List<ProductCardResponse>());
            }

            var relatedProductsQuery = _context.Products
                .AsNoTracking()
                .Where(p => p.CategoryId == currentProduct.CategoryId && p.Id != currentProduct.Id && p.Status == 1 && !p.IsDeleted)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Slug,
                    ActiveVariants = p.Variants.Where(v => !v.IsDeleted),
                    MainImage = p.Variants.Where(v => !v.IsDeleted)
                        .SelectMany(v => v.Images)
                        .OrderByDescending(i => i.IsMain)
                        .ThenBy(i => i.SortOrder)
                        .FirstOrDefault()
                })
                .Take(5);

            var products = await relatedProductsQuery.ToListAsync();

            var result = products.Select(p =>
            {
                var variants = p.ActiveVariants.ToList();
                decimal currentPrice = 0;
                decimal oldPrice = 0;

                if (variants.Any())
                {
                    currentPrice = variants.Min(v => v.Price);
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
                    Slug = p.Slug,
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

        public async Task<ApiResult<CategoryDetailResponse>> GetCategoryBySlugAsync(string slug)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Slug == slug && c.IsVisible && !c.IsDeleted)
                .Select(c => new CategoryDetailResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    IconUrl = c.ImageUrl
                })
                .FirstOrDefaultAsync();

            if (category == null)
            {
                return ApiResult<CategoryDetailResponse>.Fail("Không tìm thấy danh mục yêu cầu.");
            }

            return ApiResult<CategoryDetailResponse>.Ok(category);
        }

        public async Task<ApiResult<PagedResult<ProductCardResponse>>> GetProductsByCategoryAsync(
            string categorySlug, int page, int pageSize)
        {
            // Bước 1: Tìm danh mục gốc theo slug
            var rootCategory = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Slug == categorySlug && c.IsVisible && !c.IsDeleted)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync();

            if (rootCategory == null)
            {
                return ApiResult<PagedResult<ProductCardResponse>>.Fail("Không tìm thấy danh mục yêu cầu.");
            }

            // Bước 2: Load tất cả categories vào RAM để BFS collect descendant IDs
            // Categories ít record nên safe khi load vào RAM
            var allCategories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.IsVisible && !c.IsDeleted)
                .Select(c => new { c.Id, c.ParentId })
                .ToListAsync();

            // BFS để collect tất cả descendant IDs (bao gồm cả rootCategory)
            var categoryIds = new HashSet<int> { rootCategory.Id };
            var queue = new Queue<int>();
            queue.Enqueue(rootCategory.Id);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var children = allCategories.Where(c => c.ParentId == currentId).ToList();
                foreach (var child in children)
                {
                    if (categoryIds.Add(child.Id))
                    {
                        queue.Enqueue(child.Id);
                    }
                }
            }

            // Bước 3: Query products WHERE CategoryId IN (categoryIds) với phân trang
            var baseQuery = _context.Products
                .AsNoTracking()
                .Where(p => categoryIds.Contains(p.CategoryId) && p.Status == 1 && !p.IsDeleted);

            var totalCount = await baseQuery.CountAsync();

            var products = await baseQuery
                .OrderByDescending(p => p.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Slug,
                    ManufacturerName = p.Manufacturer != null ? p.Manufacturer.Name : string.Empty,
                    ActiveVariants = p.Variants.Where(v => !v.IsDeleted),
                    MainImage = p.Variants.Where(v => !v.IsDeleted)
                        .SelectMany(v => v.Images)
                        .OrderByDescending(i => i.IsMain)
                        .ThenBy(i => i.SortOrder)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var result = products.Select(p =>
            {
                var variants = p.ActiveVariants.ToList();
                decimal currentPrice = 0;
                decimal oldPrice = 0;

                if (variants.Any())
                {
                    currentPrice = variants.Min(v => v.Price);
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
                    Slug = p.Slug,
                    ThumbnailUrl = p.MainImage?.ImageUrl,
                    ManufacturerName = p.ManufacturerName,
                    CurrentPrice = currentPrice,
                    OldPrice = oldPrice,
                    DiscountPercent = discountPercent,
                    Rating = 5.0,
                    ReviewCount = 0
                };
            }).ToList();

            var pagedResult = new PagedResult<ProductCardResponse>
            {
                Items = result,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };

            return ApiResult<PagedResult<ProductCardResponse>>.Ok(pagedResult);
        }
    }
}
