using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Vouchers;

namespace Client.Services.Voucher
{
    public interface IVoucherClientService
    {
        Task<ApiResult<PagedResult<VoucherDto>>> GetListAsync(VoucherFilterRequest request);
        Task<ApiResult<VoucherDto>> GetByIdAsync(int id);
        Task<ApiResult<VoucherDto>> CreateAsync(CreateVoucherRequest request);
        Task<ApiResult<VoucherDto>> UpdateAsync(int id, UpdateVoucherRequest request);
        Task<ApiResult<bool>> DeleteAsync(int id);
        Task<ApiResult<VoucherDto>> ToggleStatusAsync(int id);
    }
}
