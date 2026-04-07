using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Pos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PBL3.Service.Pos
{
    public interface IPosService
    {
        Task<ApiResult<PosScanResponse>> ScanSerialAsync(string serialNumber);
        Task<ApiResult<PosCustomerDto>> LookupCustomerAsync(string phone);
        Task<ApiResult<VoucherValidationDto>> ValidateVoucherAsync(string code, decimal subTotal);
        Task<ApiResult<PosOrderDto>> CheckoutAsync(PosCheckoutRequest request, Guid employeeId);
        Task<ApiResult<PosDraftDto>> SaveDraftAsync(PosCheckoutRequest request, Guid employeeId);
        Task<ApiResult<List<PosDraftDto>>> GetDraftsAsync(Guid employeeId);
        Task<ApiResult<PosDraftDto>> GetDraftByIdAsync(int orderId, Guid employeeId);
        Task<ApiResult<bool>> DeleteDraftAsync(int orderId, Guid employeeId);
    }
}
