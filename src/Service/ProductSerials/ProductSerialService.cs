using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;

namespace PBL3.Service.ProductSerials
{
    public class ProductSerialService : IProductSerialService
    {
        private readonly IProductSerialRepository _productSerialRepository;

        public ProductSerialService(IProductSerialRepository productSerialRepository)
        {
            _productSerialRepository = productSerialRepository;
        }

        public async Task<ApiResult<bool>> CheckExistAsync(string serialNumber, int variantId)
        {
            var exists = await _productSerialRepository.ExistsAsync(serialNumber, variantId);
            return new ApiResult<bool>
            {
                Success = true,
                Message = exists
                    ? "Mã Serial đã tồn tại trong hệ thống."
                    : "Mã Serial hợp lệ, chưa có trong hệ thống.",
                Data = exists
            };
        }
    }
}
