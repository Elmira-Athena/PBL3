using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Service.Products
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepo;
        private readonly ILogger<ProductService> _logger;

        public ProductService(IProductRepository productRepo, ILogger<ProductService> logger)
        {
            _productRepo = productRepo;
            _logger = logger;
        }

        // ========================================================
        // GET LIST — Danh sách sản phẩm (phân trang + filter)
        // ========================================================
        public async Task<ApiResult<PagedResult<ProductListDto>>> GetListAsync(ProductFilterRequest request)
        {
            // Nếu filter theo CategoryId → lấy cả category con (đệ quy)
            List<int>? categoryIds = null;
            if (request.CategoryId.HasValue)
            {
                categoryIds = await _productRepo.GetCategoryChildIdsAsync(request.CategoryId.Value);
            }

            var (items, totalCount) = await _productRepo.GetPagedListAsync(
                request.Keyword,
                categoryIds,
                request.ManufacturerId,
                request.PriceMin,
                request.PriceMax,
                request.Status.HasValue ? (int)request.Status.Value : null,
                request.PageNumber,
                request.PageSize,
                request.SortBy,
                request.SortDescending);

            var dtos = items.Select(MapToListDto).ToList();

            var pagedResult = new PagedResult<ProductListDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };

            return ApiResult<PagedResult<ProductListDto>>.Ok(pagedResult);
        }

        // ========================================================
        // GET BY ID — Chi tiết sản phẩm
        // ========================================================
        public async Task<ApiResult<ProductDetailDto>> GetByIdAsync(int id)
        {
            var product = await _productRepo.GetByIdWithDetailsAsync(id);

            if (product == null)
                return ApiResult<ProductDetailDto>.Fail("Không tìm thấy sản phẩm yêu cầu.");

            var dto = MapToDetailDto(product);
            return ApiResult<ProductDetailDto>.Ok(dto);
        }

        // ========================================================
        // CREATE — Tạo mới sản phẩm (Transaction)
        // ========================================================
        public async Task<ApiResult<ProductDetailDto>> CreateAsync(CreateProductRequest request)
        {
            // Validate: Manufacturer tồn tại
            if (!await _productRepo.ManufacturerExistsAsync(request.ManufacturerId))
                return ApiResult<ProductDetailDto>.Fail("Nhà sản xuất không tồn tại.");

            // Validate: Category tồn tại
            if (!await _productRepo.CategoryExistsAsync(request.CategoryId))
                return ApiResult<ProductDetailDto>.Fail("Danh mục không tồn tại.");

            // Validate: SKU unique cho tất cả Variants
            foreach (var variant in request.Variants)
            {
                if (await _productRepo.IsSkuExistsAsync(variant.SKU))
                    return ApiResult<ProductDetailDto>.Fail($"Mã SKU '{variant.SKU}' đã tồn tại trong hệ thống.");
            }

            // Check duplicate SKU trong cùng request
            var skus = request.Variants.Select(v => v.SKU.ToUpper()).ToList();
            if (skus.Distinct().Count() != skus.Count)
                return ApiResult<ProductDetailDto>.Fail("Các phiên bản trong cùng sản phẩm không được trùng mã SKU.");

            // Build Entity
            var product = new Product
            {
                Name = request.Name,
                ShortDescription = request.ShortDescription,
                Description = request.Description,
                ManufacturerId = request.ManufacturerId,
                CategoryId = request.CategoryId,
                Status = 1, // Active
                CreatedDate = DateTime.UtcNow
            };

            // Build Variants
            foreach (var variantReq in request.Variants)
            {
                var variant = new ProductVariant
                {
                    SKU = variantReq.SKU,
                    VariantName = variantReq.VariantName,
                    Slug = GenerateSlug(request.Name, variantReq.SKU),
                    Price = variantReq.Price,
                    OriginalPrice = variantReq.OriginalPrice,
                    WarrantyMonth = variantReq.WarrantyMonth,
                    Specifications = variantReq.Specifications ?? new(),
                    CreatedDate = DateTime.UtcNow
                };

                // Images
                foreach (var imgReq in variantReq.Images)
                {
                    variant.Images.Add(new ProductImage
                    {
                        ImageUrl = imgReq.ImageUrl,
                        IsMain = imgReq.IsMain,
                        SortOrder = imgReq.SortOrder
                    });
                }

                product.Variants.Add(variant);
            }

            await _productRepo.AddAsync(product);
            await _productRepo.SaveChangesAsync();

            _logger.LogInformation("Tạo sản phẩm mới: {ProductName} (Id: {ProductId}) với {VariantCount} phiên bản.",
                product.Name, product.Id, product.Variants.Count);

            // Reload with details
            var created = await _productRepo.GetByIdWithDetailsAsync(product.Id);
            var dto = MapToDetailDto(created!);

            return ApiResult<ProductDetailDto>.Ok(dto, "Tạo sản phẩm thành công.");
        }

        // ========================================================
        // UPDATE — Cập nhật thông tin chung sản phẩm
        // ========================================================
        public async Task<ApiResult<ProductDetailDto>> UpdateAsync(int id, UpdateProductRequest request)
        {
            var product = await _productRepo.GetByIdAsync(id);

            if (product == null)
                return ApiResult<ProductDetailDto>.Fail("Không tìm thấy sản phẩm yêu cầu.");

            // Validate: Manufacturer tồn tại
            if (!await _productRepo.ManufacturerExistsAsync(request.ManufacturerId))
                return ApiResult<ProductDetailDto>.Fail("Nhà sản xuất không tồn tại.");

            // Validate: Category tồn tại
            if (!await _productRepo.CategoryExistsAsync(request.CategoryId))
                return ApiResult<ProductDetailDto>.Fail("Danh mục không tồn tại.");

            // Update fields
            product.Name = request.Name;
            product.ShortDescription = request.ShortDescription;
            product.Description = request.Description;
            product.ManufacturerId = request.ManufacturerId;
            product.CategoryId = request.CategoryId;
            product.Status = (byte)request.Status;
            product.ModifiedDate = DateTime.UtcNow;

            await _productRepo.SaveChangesAsync();

            _logger.LogInformation("Cập nhật sản phẩm: {ProductName} (Id: {ProductId})",
                product.Name, product.Id);

            var updated = await _productRepo.GetByIdWithDetailsAsync(product.Id);
            var dto = MapToDetailDto(updated!);

            return ApiResult<ProductDetailDto>.Ok(dto, "Cập nhật sản phẩm thành công.");
        }

        // ========================================================
        // ADD VARIANT — Thêm phiên bản cho sản phẩm đã có
        // ========================================================
        public async Task<ApiResult<ProductVariantDto>> AddVariantAsync(int productId, SaveVariantRequest request)
        {
            var product = await _productRepo.GetByIdAsync(productId);

            if (product == null)
                return ApiResult<ProductVariantDto>.Fail("Không tìm thấy sản phẩm yêu cầu.");

            // Check SKU unique
            if (await _productRepo.IsSkuExistsAsync(request.SKU))
                return ApiResult<ProductVariantDto>.Fail($"Mã SKU '{request.SKU}' đã tồn tại trong hệ thống.");

            var variant = new ProductVariant
            {
                ProductId = productId,
                SKU = request.SKU,
                VariantName = request.VariantName,
                Slug = GenerateSlug(product.Name, request.SKU),
                Price = request.Price,
                OriginalPrice = request.OriginalPrice,
                WarrantyMonth = request.WarrantyMonth,
                Specifications = request.Specifications ?? new(),
                CreatedDate = DateTime.UtcNow
            };

            // Images
            foreach (var imgReq in request.Images)
            {
                variant.Images.Add(new ProductImage
                {
                    ImageUrl = imgReq.ImageUrl,
                    IsMain = imgReq.IsMain,
                    SortOrder = imgReq.SortOrder
                });
            }


            await _productRepo.AddVariantAsync(variant);
            await _productRepo.SaveChangesAsync();

            _logger.LogInformation("Thêm phiên bản '{VariantName}' (SKU: {SKU}) cho sản phẩm Id: {ProductId}",
                variant.VariantName, variant.SKU, productId);

            var dto = MapToVariantDto(variant);
            return ApiResult<ProductVariantDto>.Ok(dto, "Thêm phiên bản thành công.");
        }

        // ========================================================
        // DELETE — Soft Delete sản phẩm
        // ========================================================
        public async Task<ApiResult<bool>> DeleteAsync(int id)
        {
            var product = await _productRepo.GetByIdAsync(id);

            if (product == null)
                return ApiResult<bool>.Fail("Không tìm thấy sản phẩm yêu cầu.");

            // Soft Delete
            product.IsDeleted = true;
            product.DeletedDate = DateTime.UtcNow;
            product.Status = (byte)ProductStatus.StopBusiness;

            await _productRepo.SaveChangesAsync();

            _logger.LogInformation("Xóa mềm sản phẩm: {ProductName} (Id: {ProductId})",
                product.Name, product.Id);

            return ApiResult<bool>.Ok(true, "Xóa sản phẩm thành công.");
        }

        // ========================================================
        // PRIVATE HELPERS — Mapping & Utility
        // ========================================================

        private static ProductDetailDto MapToDetailDto(Product entity)
        {
            return new ProductDetailDto
            {
                Id = entity.Id,
                Name = entity.Name,
                ShortDescription = entity.ShortDescription,
                Description = entity.Description,
                ManufacturerId = entity.ManufacturerId,
                ManufacturerName = entity.Manufacturer?.Name ?? string.Empty,
                CategoryId = entity.CategoryId,
                CategoryName = entity.Category?.Name ?? string.Empty,
                Status = (ProductStatus)entity.Status,
                CreatedDate = entity.CreatedDate,
                Variants = entity.Variants
                    .Where(v => !v.IsDeleted)
                    .Select(MapToVariantDto)
                    .ToList()
            };
        }

        private static ProductVariantDto MapToVariantDto(ProductVariant v)
        {
            return new ProductVariantDto
            {
                Id = v.Id,
                SKU = v.SKU,
                VariantName = v.VariantName,
                Slug = v.Slug,
                Price = v.Price,
                OriginalPrice = v.OriginalPrice,
                StockQuantity = v.StockQuantity,
                WarrantyMonth = v.WarrantyMonth,
                Specifications = v.Specifications,
                Images = v.Images?.OrderBy(i => i.SortOrder).Select(i => new ProductImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    IsMain = i.IsMain,
                    SortOrder = i.SortOrder
                }).ToList() ?? new()
            };
        }

        private static ProductListDto MapToListDto(Product entity)
        {
            var activeVariants = entity.Variants?.Where(v => !v.IsDeleted).ToList() ?? new();
            var minPrice = activeVariants.Any() ? activeVariants.Min(v => v.Price) : 0;
            var maxPrice = activeVariants.Any() ? activeVariants.Max(v => v.Price) : 0;

            // Lấy ảnh chính (IsMain) của variant đầu tiên
            string? thumbnailUrl = null;
            if (activeVariants.Any())
            {
                var firstVariant = activeVariants.First();
                thumbnailUrl = firstVariant.Images?
                    .OrderByDescending(i => i.IsMain)
                    .ThenBy(i => i.SortOrder)
                    .FirstOrDefault()?.ImageUrl;
            }

            return new ProductListDto
            {
                Id = entity.Id,
                Name = entity.Name,
                ShortDescription = entity.ShortDescription,
                ManufacturerName = entity.Manufacturer?.Name ?? string.Empty,
                CategoryName = entity.Category?.Name ?? string.Empty,
                Status = (ProductStatus)entity.Status,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                PriceRange = FormatPriceRange(minPrice, maxPrice),
                ThumbnailUrl = thumbnailUrl,
                TotalStock = activeVariants.Sum(v => v.StockQuantity),
                VariantCount = activeVariants.Count,
                CreatedDate = entity.CreatedDate
            };
        }

        private static string FormatPriceRange(decimal min, decimal max)
        {
            var culture = new CultureInfo("vi-VN");
            if (min == max)
                return min.ToString("N0", culture) + "đ";

            return $"{min.ToString("N0", culture)}đ - {max.ToString("N0", culture)}đ";
        }

        /// <summary>
        /// Sinh slug từ tên sản phẩm + SKU.
        /// VD: "Dell XPS 15" + "DELL-XPS-16GB" → "dell-xps-15-dell-xps-16gb"
        /// </summary>
        private static string GenerateSlug(string productName, string sku)
        {
            var combined = $"{productName} {sku}";
            // Remove diacritics (dấu tiếng Việt)
            var normalized = combined.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            var noDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);

            // Convert to lowercase, replace spaces and special chars with dashes
            var slug = Regex.Replace(noDiacritics.ToLower(), @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"[\s-]+", "-").Trim('-');

            return slug;
        }
    }
}
