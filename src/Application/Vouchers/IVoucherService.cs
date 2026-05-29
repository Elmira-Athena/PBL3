using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Vouchers;

namespace PBL3.Application.Vouchers
{
    public interface IVoucherService
    {
        /// <summary>
        /// Lấy danh sách voucher có phân trang, hỗ trợ tìm kiếm và lọc.
        /// </summary>
        Task<ApiResult<PagedResult<VoucherDto>>> GetPagedListAsync(VoucherFilterRequest filter);

        /// <summary>
        /// Lấy chi tiết voucher theo Id.
        /// </summary>
        Task<ApiResult<VoucherDto>> GetByIdAsync(int id);

        /// <summary>
        /// Tạo mới voucher. Kiểm tra trùng mã trước khi tạo.
        /// </summary>
        Task<ApiResult<VoucherDto>> CreateAsync(CreateVoucherRequest request);

        /// <summary>
        /// Cập nhật voucher. Code bất biến sau khi tạo.
        /// Cập nhật lại toàn bộ danh sách CategoryIds.
        /// </summary>
        Task<ApiResult<VoucherDto>> UpdateAsync(int id, UpdateVoucherRequest request);

        /// <summary>
        /// Xóa mềm voucher (Soft Delete).
        /// Không cho xóa nếu voucher đang được dùng trong đơn hàng chờ xử lý.
        /// </summary>
        Task<ApiResult<bool>> DeleteAsync(int id);

        /// <summary>
        /// Bật/tắt trạng thái IsActive của voucher.
        /// </summary>
        Task<ApiResult<VoucherDto>> ToggleStatusAsync(int id);

        /// <summary>
        /// Kiểm tra tính hợp lệ của một mã voucher và tính toán discount preview.
        /// Dùng cho trang checkout để preview giảm giá trước khi đặt hàng.
        /// </summary>
        Task<ApiResult<ValidateVoucherResponse>> ValidateVoucherCodeAsync(ValidateVoucherRequest request, Guid? userId);

        /// <summary>
        /// Lấy tất cả voucher active kèm thông tin có thể áp dụng cho đơn hàng hiện tại.
        /// Dùng cho popup chọn voucher ở trang Checkout.
        /// </summary>
        Task<ApiResult<List<VoucherAvailabilityDto>>> GetAvailableForOrderAsync(
            GetAvailableVouchersRequest request, Guid? userId);
    }
}
