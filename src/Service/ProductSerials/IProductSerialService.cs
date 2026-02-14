using PBL3.Shared.DTOs.Common;

namespace PBL3.Service.ProductSerials
{
    public interface IProductSerialService
    {
        /// <summary>
        /// Kiểm tra Serial Number đã tồn tại trong DB chưa (theo VariantId).
        /// Trả về ApiResult chứa bool: true = đã tồn tại, false = mã mới.
        /// </summary>
        Task<ApiResult<bool>> CheckExistAsync(string serialNumber, int variantId);
    }
}
