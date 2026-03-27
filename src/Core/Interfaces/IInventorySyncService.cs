using System.Collections.Generic;
using System.Threading.Tasks;

namespace PBL3.Core.Interfaces
{
    /// <summary>
    /// Service đồng bộ StockQuantity vật lý trong ProductVariants.
    /// Được gọi SAU MỌI thao tác thay đổi trạng thái Serial.
    /// Luôn luôn tuân thủ nguyên tắc CQRS: Tách biệt Read/Write để tối ưu.
    /// </summary>
    public interface IInventorySyncService
    {
        /// <summary>
        /// Đồng bộ StockQuantity cho 1 VariantId cụ thể.
        /// Count Serials WHERE Status = 0 (Available) rồi UPDATE vào cột StockQuantity.
        /// </summary>
        Task SyncStockAsync(int variantId);

        /// <summary>
        /// Đồng bộ StockQuantity cho nhiều VariantId cùng lúc (batch).
        /// Dùng khi nhập kho nhiều dòng hoặc xử lý đơn hàng nhiều sản phẩm.
        /// Sử dụng bulk update để giảm queries.
        /// </summary>
        Task SyncStockBatchAsync(IEnumerable<int> variantIds);
    }
}
