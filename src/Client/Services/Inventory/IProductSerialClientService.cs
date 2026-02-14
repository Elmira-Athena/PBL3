using PBL3.Shared.DTOs.Common;

namespace Client.Services.Inventory
{
    public interface IProductSerialClientService
    {
        /// <summary>
        /// Kiểm tra mã Serial đã tồn tại trong DB chưa.
        /// </summary>
        /// <param name="serialNumber">Mã Serial cần kiểm tra.</param>
        /// <param name="variantId">Id của ProductVariant.</param>
        /// <returns>ApiResult chứa true nếu đã tồn tại, false nếu mã mới.</returns>
        Task<ApiResult<bool>> CheckExistAsync(string serialNumber, int variantId);
    }
}
