using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Products;

namespace PBL3.Service.Products
{
    public class ProductVariantService : IProductVariantService
    {
        private readonly IProductVariantRepository _variantRepo;
        private readonly IProductRepository _productRepo;
        private readonly ILogger<ProductVariantService> _logger;

        public ProductVariantService(
            IProductVariantRepository variantRepo,
            IProductRepository productRepo,
            ILogger<ProductVariantService> logger)
        {
            _variantRepo = variantRepo;
            _productRepo = productRepo;
            _logger = logger;
        }

        public async Task<ApiResult<ProductVariantDto>> GetByIdAsync(int variantId)
        {
            var variant = await _variantRepo.GetByIdWithImagesAsync(variantId);
            if (variant == null)
                return ApiResult<ProductVariantDto>.Fail("Không tìm thấy phiên bản yêu cầu.");

            return ApiResult<ProductVariantDto>.Ok(MapToDto(variant));
        }

        public async Task<ApiResult<ProductVariantDto>> CreateAsync(int productId, SaveVariantRequest request)
        {
            var product = await _productRepo.GetByIdAsync(productId);
            if (product == null)
                return ApiResult<ProductVariantDto>.Fail("Không tìm thấy sản phẩm yêu cầu.");

            if (await _productRepo.IsSkuExistsAsync(request.SKU))
                return ApiResult<ProductVariantDto>.Fail($"Mã SKU '{request.SKU}' đã tồn tại trong hệ thống.");

            var variant = new ProductVariant
            {
                ProductId = productId,
                SKU = request.SKU,
                VariantName = request.VariantName,
                Slug = ProductSlugHelper.GenerateVariantSlug(product.Name, request.SKU),
                Price = request.Price,
                OriginalPrice = request.OriginalPrice,
                WarrantyMonth = request.WarrantyMonth,
                Specifications = request.Specifications ?? new(),
                CreatedDate = DateTime.UtcNow
            };

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
            try
            {
                await _productRepo.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
                                             || ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
            {
                return ApiResult<ProductVariantDto>.Fail($"Mã SKU '{request.SKU}' đã tồn tại trong hệ thống.");
            }

            _logger.LogInformation("Thêm phiên bản '{VariantName}' (SKU: {SKU}) cho sản phẩm Id: {ProductId}",
                variant.VariantName, variant.SKU, productId);

            return ApiResult<ProductVariantDto>.Ok(MapToDto(variant), "Thêm phiên bản thành công.");
        }

        public async Task<ApiResult<ProductVariantDto>> UpdateAsync(int variantId, UpdateVariantRequest request)
        {
            var variant = await _variantRepo.GetByIdAsync(variantId);
            if (variant == null)
                return ApiResult<ProductVariantDto>.Fail("Không tìm thấy phiên bản yêu cầu.");

            // Nếu SKU đổi → kiểm tra trùng (bỏ qua chính nó)
            if (!string.Equals(variant.SKU, request.SKU, StringComparison.OrdinalIgnoreCase)
                && await _productRepo.IsSkuExistsAsync(request.SKU, variantId))
            {
                return ApiResult<ProductVariantDto>.Fail($"Mã SKU '{request.SKU}' đã tồn tại trong hệ thống.");
            }

            variant.SKU = request.SKU;
            variant.VariantName = request.VariantName;
            variant.Price = request.Price;
            variant.OriginalPrice = request.OriginalPrice;
            variant.WarrantyMonth = request.WarrantyMonth;
            variant.ModifiedDate = DateTime.UtcNow;

            try
            {
                await _variantRepo.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
                                             || ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
            {
                return ApiResult<ProductVariantDto>.Fail($"Mã SKU '{request.SKU}' đã tồn tại trong hệ thống.");
            }

            _logger.LogInformation("Cập nhật phiên bản Id: {VariantId} (SKU: {SKU})", variantId, variant.SKU);

            // Reload với ảnh để trả DTO đầy đủ
            var updated = await _variantRepo.GetByIdWithImagesAsync(variantId);
            return ApiResult<ProductVariantDto>.Ok(MapToDto(updated!), "Cập nhật phiên bản thành công.");
        }

        public async Task<ApiResult<bool>> ReplaceImagesAsync(int variantId, SaveVariantImagesRequest request)
        {
            var variant = await _variantRepo.GetByIdAsync(variantId);
            if (variant == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiên bản yêu cầu.");

            var newImages = request.Images.Select((img, idx) => new ProductImage
            {
                ImageUrl = img.ImageUrl,
                IsMain = idx == 0,
                SortOrder = idx
            }).ToList();

            await _variantRepo.ReplaceImagesAsync(variantId, newImages);
            await _variantRepo.SaveChangesAsync();

            _logger.LogInformation("Thay {Count} ảnh cho phiên bản Id: {VariantId}", newImages.Count, variantId);

            return ApiResult<bool>.Ok(true, "Cập nhật ảnh phiên bản thành công.");
        }

        public async Task<ApiResult<bool>> ReplaceSpecificationsAsync(int variantId, SaveVariantSpecificationsRequest request)
        {
            var variant = await _variantRepo.GetByIdAsync(variantId);
            if (variant == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiên bản yêu cầu.");

            variant.Specifications = request.Specifications ?? new();
            variant.ModifiedDate = DateTime.UtcNow;
            await _variantRepo.SaveChangesAsync();

            _logger.LogInformation("Cập nhật thông số kỹ thuật cho phiên bản Id: {VariantId} ({Count} mục)",
                variantId, variant.Specifications.Count);

            return ApiResult<bool>.Ok(true, "Cập nhật thông số kỹ thuật thành công.");
        }

        public async Task<ApiResult<bool>> DeleteAsync(int variantId)
        {
            var variant = await _variantRepo.GetByIdAsync(variantId);
            if (variant == null)
                return ApiResult<bool>.Fail("Không tìm thấy phiên bản yêu cầu.");

            var remaining = await _variantRepo.CountActiveByProductAsync(variant.ProductId);
            if (remaining <= 1)
                return ApiResult<bool>.Fail("Không thể xoá phiên bản duy nhất của sản phẩm. Hãy xoá sản phẩm thay thế.");

            variant.IsDeleted = true;
            variant.DeletedDate = DateTime.UtcNow;
            await _variantRepo.SaveChangesAsync();

            _logger.LogInformation("Xoá mềm phiên bản Id: {VariantId} (SKU: {SKU})", variantId, variant.SKU);

            return ApiResult<bool>.Ok(true, "Xoá phiên bản thành công.");
        }

        private static ProductVariantDto MapToDto(ProductVariant v)
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
                Images = v.Images?.OrderByDescending(i => i.IsMain).ThenBy(i => i.SortOrder)
                    .Select(i => new ProductImageDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        IsMain = i.IsMain,
                        SortOrder = i.SortOrder
                    }).ToList() ?? new()
            };
        }
    }
}
