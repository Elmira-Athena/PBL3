using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;

namespace PBL3.Infrastructure.Repositories
{
    public class ProductVariantRepository : IProductVariantRepository
    {
        private readonly HushStoreDbContext _context;

        public ProductVariantRepository(HushStoreDbContext context)
        {
            _context = context;
        }

        public async Task<ProductVariant?> GetByIdAsync(int variantId)
        {
            return await _context.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted);
        }

        public async Task<ProductVariant?> GetByIdWithImagesAsync(int variantId)
        {
            return await _context.ProductVariants
                .Include(v => v.Images)
                .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted);
        }

        public async Task<int> CountActiveByProductAsync(int productId)
        {
            return await _context.ProductVariants
                .CountAsync(v => v.ProductId == productId && !v.IsDeleted);
        }

        public async Task ReplaceImagesAsync(int variantId, List<ProductImage> newImages)
        {
            var oldImages = await _context.ProductImages
                .Where(i => i.VariantId == variantId)
                .ToListAsync();

            _context.ProductImages.RemoveRange(oldImages);

            foreach (var img in newImages)
            {
                await _context.ProductImages.AddAsync(new ProductImage
                {
                    VariantId = variantId,
                    ImageUrl = img.ImageUrl,
                    IsMain = img.IsMain,
                    SortOrder = img.SortOrder
                });
            }
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
