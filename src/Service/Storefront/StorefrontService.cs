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

        /// <summary>
        /// NGHIỆP VỤ PHỤ TRỢ: Lập bản đồ đánh giá trung bình (Rating Map) tối ưu hóa hiệu năng.
        /// Sử dụng kỹ thuật GroupBy và Aggregate trên Database thông qua EF Core để:
        /// 1. Gom nhóm toàn bộ bình luận (ProductReviews) theo mã sản phẩm (ProductId).
        /// 2. Tính toán đồng thời Rating trung bình và Tổng số lượt đánh giá trong một câu lệnh đơn lẻ.
        /// 3. Trả về dưới dạng Dictionary để tra cứu O(1) trong RAM, triệt tiêu lỗi N+1 Query.
        /// </summary>
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

        /// <summary>
        /// NGHIỆP VỤ: Truy vấn danh mục sản phẩm đang hiển thị (Active Categories).
        /// Lọc bỏ các danh mục bị ẩn (IsVisible == false) hoặc đã bị xóa logic (IsDeleted == true).
        /// </summary>
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

        /// <summary>
        /// NGHIỆP VỤ: Truy vấn sản phẩm nổi bật (Featured Products) theo danh mục.
        /// ── CƠ CHẾ TÍNH TOÁN KHOẢNG GIÁ & CHIẾT KHẤU ĐỘNG ──
        /// 1. Lọc sản phẩm đang hiển thị (Status == 1) và không bị xóa logic.
        /// 2. Với mỗi sản phẩm, nạp các biến thể đang hoạt động (ActiveVariants).
        /// 3. Xác định Giá bán hiện hành (CurrentPrice) = Giá nhỏ nhất trong tất cả biến thể (Min Price).
        ///    - Giúp hiển thị mức giá hấp dẫn nhất "Chỉ từ X.000đ" trên giao diện Storefront.
        /// 4. Xác định Giá cũ/gốc (OldPrice) = Giá niêm yết lớn nhất trong các biến thể (Max Price).
        /// 5. Tính toán Tỷ lệ giảm giá tương đối (DiscountPercent) của cả dòng sản phẩm dựa trên khoảng chênh lệch Min Price và Max Original Price.
        /// 6. Nạp thông tin Rating trung bình từ Dictionary Rating Map đã tối ưu.
        /// </summary>
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
                    p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : string.Empty,
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
                    CategoryId = p.CategoryId,
                    CategoryName = p.CategoryName,
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

        /// <summary>
        /// NGHIỆP VỤ: Truy vấn chi tiết sản phẩm dành cho trang bán lẻ (Product Detail).
        /// 1. Tìm sản phẩm dựa trên định danh SEO-Friendly (Slug) đang hoạt động.
        /// 2. Nạp cấu trúc đa tầng dữ liệu: Nhà sản xuất, Danh mục, Biến thể đang bán và bộ ảnh thực tế.
        /// 3. Tổng hợp danh sách ảnh (Distinct) từ toàn bộ ảnh của các biến thể để làm Slider ảnh lớn.
        /// 4. Trích xuất thông số kỹ thuật (Specifications) mặc định từ biến thể có giá thấp nhất để hiển thị nhanh.
        /// 5. Tính toán các điểm số đánh giá và thống kê lượt review phục vụ hiển thị uy tín sản phẩm.
        /// </summary>
        public async Task<ApiResult<ProductDetailResponse>> GetProductDetailAsync(string slug)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Manufacturer)
                .Include(p => p.Category)
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
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name ?? string.Empty,
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

        /// <summary>
        /// NGHIỆP VỤ: Lấy danh sách sản phẩm liên quan (Related Products) hiển thị ở cuối trang chi tiết.
        /// Hệ thống đề xuất các sản phẩm thuộc cùng Danh mục (CategoryId), loại trừ chính nó và giới hạn tối đa 5 sản phẩm.
        /// Áp dụng cơ chế ánh xạ giá động (Min/Max) tương đương trang chủ để đảm bảo tính đồng nhất giao diện.
        /// </summary>
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
                    p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : string.Empty,
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
                    CategoryId = p.CategoryId,
                    CategoryName = p.CategoryName,
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

        /// <summary>
        /// NGHIỆP VỤ: Lấy thông tin chi tiết của danh mục phục vụ bộ lọc tìm kiếm sản phẩm.
        /// </summary>
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

        /// <summary>
        /// NGHIỆP VỤ: Tìm kiếm nâng cao và lọc sản phẩm linh hoạt trên giao diện Storefront.
        /// Hỗ trợ tìm kiếm từ khóa không dấu/có dấu, lọc phân cấp theo danh mục và lọc theo khoảng giá bán:
        /// - Lọc PriceMin/PriceMax: Chỉ trả về các sản phẩm chứa ít nhất một biến thể (Variant) có giá nằm trong tầm lọc của người tiêu dùng.
        /// - Áp dụng phân trang hiệu năng cao ở tầng Database để giảm tải lượng dữ liệu vận chuyển qua Network.
        /// </summary>
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
                    p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : string.Empty,
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
                    CategoryId = p.CategoryId,
                    CategoryName = p.CategoryName,
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

        /// <summary>
        /// NGHIỆP VỤ CỰC KỲ QUAN TRỌNG: Truy vấn toàn bộ sản phẩm thuộc một danh mục cụ thể và mọi danh mục con đệ quy của nó.
        /// ── GIẢI PHÁP TỐI ƯU HÓA TRUY VẤN CÂY PHÂN CẤP (BFS ON RAM) ──
        /// Điển hình, để lấy sản phẩm thuộc danh mục cha (Ví dụ: "Điện thoại") gồm cả danh mục con ("iPhone", "Samsung"), ta phải truy vấn đệ quy DB.
        /// Việc dùng đệ quy SQL (Common Table Expressions - CTE) rất tốn kém tài nguyên và không tương thích linh động giữa các DBMS (SQL Server, MySQL, PostgreSQL).
        /// Giải pháp thay thế xuất sắc:
        /// Step 1: Tìm ID danh mục gốc theo slug.
        /// Step 2: Nạp toàn bộ danh sách danh mục phẳng đang hoạt động (chỉ cột Id và ParentId cực kỳ nhẹ) vào bộ nhớ RAM một lần duy nhất.
        /// Step 3: Thực thi giải thuật Duyệt theo chiều rộng (Breadth-First Search - BFS) sử dụng cấu trúc hàng đợi Queue trong RAM.
        ///        - Thu gom toàn bộ ID danh mục con cháu trong vòng vài micro giây mà không phát sinh thêm bất kỳ câu truy vấn cơ sở dữ liệu đệ quy nào.
        /// Step 4: Sử dụng toán tử 'IN' trên SQL (WHERE CategoryId IN (categoryIds)) để truy xuất phân trang toàn bộ sản phẩm tương ứng.
        /// Step 5: Thực hiện gộp giá bán, chiết khấu và liên kết bản đồ đánh giá xếp hạng tương tự các bộ lọc khác.
        /// </summary>
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
                    p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : string.Empty,
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
                    CategoryId = p.CategoryId,
                    CategoryName = p.CategoryName,
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
