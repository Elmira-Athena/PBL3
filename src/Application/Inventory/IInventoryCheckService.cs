using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Inventory;

namespace PBL3.Application.Inventory
{
    public interface IInventoryCheckService
    {
        /// <summary>Tạo phiếu kiểm kê mới và chốt snapshot.</summary>
        Task<ApiResult<InventoryCheckDto>> CreateAsync(CreateInventoryCheckRequest request, Guid employeeId);

        /// <summary>Danh sách phiếu kiểm kê phân trang.</summary>
        Task<ApiResult<PagedResult<InventoryCheckListItemDto>>> GetPagedListAsync(InventoryCheckFilterRequest filter);

        /// <summary>Chi tiết phiếu kiểm kê.</summary>
        Task<ApiResult<InventoryCheckDto>> GetByIdAsync(int id);

        /// <summary>Dashboard đối chiếu real-time.</summary>
        Task<ApiResult<InventoryCheckDashboardDto>> GetDashboardAsync(int id);

        /// <summary>Danh sách serial phân trang với filter ScanStatus.</summary>
        Task<ApiResult<PagedResult<InventoryCheckSerialDto>>> GetSerialsAsync(int checkId, InventoryCheckSerialFilterRequest filter);

        /// <summary>Quét 1 Serial vào phiếu kiểm kê.</summary>
        Task<ApiResult<ScanResultDto>> ScanSerialAsync(int checkId, ScanSerialRequest request, Guid employeeId);

        /// <summary>Đánh dấu Serial là hàng lỗi vật lý (A5).</summary>
        Task<ApiResult<bool>> MarkDefectiveAsync(int checkId, int detailSerialId, Guid employeeId);

        /// <summary>Cập nhật lý do chênh lệch và hướng xử lý đề xuất.</summary>
        Task<ApiResult<bool>> UpdateReasonAsync(int checkId, int detailSerialId, UpdateScanReasonRequest request, Guid employeeId);

        /// <summary>Gửi duyệt phiếu: chuyển Pending → Missing, Status → AwaitingApproval.</summary>
        Task<ApiResult<bool>> SubmitAsync(int checkId, Guid employeeId);

        /// <summary>Phê duyệt và cân bằng kho (Admin only).</summary>
        Task<ApiResult<bool>> ApproveAsync(int checkId, Guid adminId);

        /// <summary>Từ chối phiếu (Admin only).</summary>
        Task<ApiResult<bool>> RejectAsync(int checkId, RejectInventoryCheckRequest request, Guid adminId);

        /// <summary>Hủy phiếu nháp.</summary>
        Task<ApiResult<bool>> CancelAsync(int checkId, Guid employeeId, bool isAdmin);
    }
}
