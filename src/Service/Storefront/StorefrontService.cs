using Microsoft.EntityFrameworkCore;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Storefront;

namespace PBL3.Service.Storefront
{
    public class StorefrontService : IStorefrontService
    {
        private readonly HushStoreDbContext _context;
        private readonly IProductReviewRepository _reviewRepo;

        public StorefrontService(HushStoreDbContext context, IProductReviewRepository reviewRepo)
        {
            _context = context;
            _reviewRepo = reviewRepo;
        }

        private async Task<Dictionary<int, (double Avg, int Count)>> GetRatingMapAsync(List<int> productIds)
        {
            if (productIds.Count == 0) return new();
            return await _context.ProductReviews
                .AsNoTracking()
                .Where(r => productIds.Contains(r.ProductId))
                .GroupBy(r => r.ProductId)
                .Select(g => new { ProductId = g.Key, Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
                .ToDictionaryAsync(x => x.ProductId, x => (x.Avg, x.Count));
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
                    IconUrl = c.ImageUrl,
                    Level = c.Level,
                    ParentId = c.ParentId
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

            var ratingMapFeatured = await GetRatingMapAsync(products.Select(p => p.Id).ToList());

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

                var ratings = ratingMapFeatured.GetValueOrDefault(p.Id, (0.0, 0));
                return new ProductCardResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Slug = p.Slug,
                    ThumbnailUrl = p.MainImage?.ImageUrl,
                    CurrentPrice = currentPrice,
                    OldPrice = oldPrice,
                    DiscountPercent = discountPercent,
                    IsAvailable = variants.Any(v => v.StockQuantity > 0),
                    Rating = ratings.Item1,
                    ReviewCount = ratings.Item2
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
                OriginalPrice = v.OriginalPrice > v.Price ? v.OriginalPrice : null,
                IsAvailable = v.StockQuantity > 0,
                StockQuantity = v.StockQuantity,
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

            var reviewStats = await _context.ProductReviews
                .AsNoTracking()
                .Where(r => r.ProductId == product.Id)
                .GroupBy(r => r.ProductId)
                .Select(g => new { Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
                .FirstOrDefaultAsync();

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
                Rating = reviewStats?.Avg ?? 0.0,
                ReviewCount = reviewStats?.Count ?? 0
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

            var ratingMapRelated = await GetRatingMapAsync(products.Select(p => p.Id).ToList());

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

                var ratings = ratingMapRelated.GetValueOrDefault(p.Id, (0.0, 0));
                return new ProductCardResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Slug = p.Slug,
                    ThumbnailUrl = p.MainImage?.ImageUrl,
                    CurrentPrice = currentPrice,
                    OldPrice = oldPrice,
                    DiscountPercent = discountPercent,
                    IsAvailable = variants.Any(v => v.StockQuantity > 0),
                    Rating = ratings.Item1,
                    ReviewCount = ratings.Item2
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

        public async Task<ApiResult<PagedResult<ProductCardResponse>>> SearchProductsAsync(
            string? keyword, int? categoryId, decimal? priceMin, decimal? priceMax,
            int page, int pageSize)
        {
            var query = _context.Products
                .AsNoTracking()
                .Where(p => p.Status == 1 && !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(kw) ||
                    (p.ShortDescription != null && p.ShortDescription.ToLower().Contains(kw)));
            }

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (priceMin.HasValue)
                query = query.Where(p => p.Variants.Any(v => !v.IsDeleted && v.Price >= priceMin.Value));

            if (priceMax.HasValue)
                query = query.Where(p => p.Variants.Any(v => !v.IsDeleted && v.Price <= priceMax.Value));

            var totalCount = await query.CountAsync();

            var products = await query
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

            var ratingMapSearch = await GetRatingMapAsync(products.Select(p => p.Id).ToList());

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
                    discountPercent = (int)Math.Round((oldPrice - currentPrice) / oldPrice * 100);

                var ratings = ratingMapSearch.GetValueOrDefault(p.Id, (0.0, 0));
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
                    IsAvailable = variants.Any(v => v.StockQuantity > 0),
                    Rating = ratings.Item1,
                    ReviewCount = ratings.Item2
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

            var ratingMapCategory = await GetRatingMapAsync(products.Select(p => p.Id).ToList());

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

                var ratings = ratingMapCategory.GetValueOrDefault(p.Id, (0.0, 0));
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
                    IsAvailable = variants.Any(v => v.StockQuantity > 0),
                    Rating = ratings.Item1,
                    ReviewCount = ratings.Item2
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
