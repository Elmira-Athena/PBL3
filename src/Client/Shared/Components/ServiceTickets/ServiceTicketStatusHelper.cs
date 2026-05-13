using MudBlazor;

namespace Client.Shared.Components.ServiceTickets
{
    public static class ServiceTicketStatusHelper
    {
        public static string GetLabel(byte status) => status switch
        {
            0 => "Đã tiếp nhận",
            1 => "Đang chẩn đoán",
            2 => "Đã gửi báo giá",
            3 => "Khách từ chối báo giá",
            4 => "Chờ phụ tùng",
            5 => "Đang sửa chữa",
            6 => "Đã gửi hãng (RMA)",
            7 => "Đã nhận lại từ hãng",
            8 => "Đã đổi 1-1",
            9 => "Hoàn tất",
            10 => "Đã hủy",
            _ => "Không xác định"
        };

        public static Color GetColor(byte status) => status switch
        {
            0 => Color.Info,       // Received - Blue
            1 => Color.Warning,    // Diagnosing - Orange
            2 => Color.Primary,    // QuoteSent - Purple
            3 => Color.Error,      // QuoteRejected - Red
            4 => Color.Dark,       // WaitingParts - Gray
            5 => Color.Warning,    // InRepair - Orange
            6 => Color.Primary,    // SentToManufacturer - Purple
            7 => Color.Info,       // ReceivedFromManufacturer - Blue
            8 => Color.Secondary,  // Swapped - Light Blue
            9 => Color.Success,    // Completed - Green
            10 => Color.Error,     // Cancelled - Red
            _ => Color.Default
        };

        public static string GetResolutionLabel(byte resolutionType) => resolutionType switch
        {
            0 => "Chưa xác định",
            1 => "Sửa nội bộ",
            2 => "Gửi hãng (RMA)",
            3 => "Đổi 1-1",
            4 => "Sửa tính phí",
            5 => "Từ chối",
            6 => "Hủy",
            _ => "Không xác định"
        };

        public static string GetManufacturerResolutionLabel(byte resolution) => resolution switch
        {
            0 => "Chưa xác định",
            1 => "Đã sửa",
            2 => "Đã thay thế",
            3 => "Từ chối",
            _ => "Không xác định"
        };

        public static string GetQuotationStatusLabel(byte status) => status switch
        {
            0 => "Chờ duyệt",
            1 => "Đã duyệt",
            2 => "Từ chối",
            3 => "Thay thế",
            _ => "Không xác định"
        };

        public static string GetWarrantySourceLabel(byte source) => source switch
        {
            0 => "Bản ghi bảo hành",
            1 => "Tính toán từ ngày bán",
            2 => "Không còn bảo hành",
            _ => "Không xác định"
        };
    }
}
