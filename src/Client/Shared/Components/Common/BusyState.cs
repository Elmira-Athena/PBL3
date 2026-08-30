namespace Client.Shared.Components.Common;

/// <summary>
/// Cờ "đang chạy một hành động" dùng chung, kèm sự kiện đổi trạng thái.
///
/// ═══ VÌ SAO CẦN MỘT LỚP RIÊNG CHO MỘT BIẾN bool ═══
///
/// Vì cách viết thủ công đã SAI Ở BỐN CHỖ trong repo này, và sai theo cùng một
/// kiểu rất khó thấy. Ví dụ thật ở InventoryCheckDetailPage.HandleMarkDefective:
///
///     bool? confirm = await DialogService.ShowMessageBox(...);   // await #1
///     if (confirm != true) return;
///     _isActioning = true;                 // <-- ĐẶT CỜ SAU await
///     var result = await Service.Xxx(...); // await #2
///     _isActioning = false;                // <-- và KHÔNG có StateHasChanged()
///
/// Trang đó CÓ cờ, CÓ bind Disabled="_isActioning", và vẫn không chống được
/// double-submit. Lý do: ComponentBase.HandleEventAsync chỉ tự gọi
/// StateHasChanged() sau phần ĐỒNG BỘ của handler. Phần đồng bộ ở đây kết thúc
/// ngay tại await #1, lúc đó cờ vẫn là false — nút vẫn bật. Sau khi dialog đóng,
/// cờ được đặt true nhưng KHÔNG có StateHasChanged() nào nữa, nên UI không bao
/// giờ vẽ lại và nút vẫn bật suốt thời gian gọi API.
///
/// Bốn người đã viết đúng ý định và vẫn sai. Đó là lý do việc này thuộc về một
/// lớp dùng chung chứ không phải kỷ luật cá nhân ở từng trang.
///
/// Hai quy tắc lớp này bảo đảm:
///   1. Cờ được đặt TRƯỚC MỌI await.
///   2. Mỗi lần đổi đều raise sự kiện để component vẽ lại.
/// </summary>
public sealed class BusyState
{
    public bool IsBusy { get; private set; }

    /// <summary>Phát mỗi khi IsBusy đổi. Component subscribe rồi gọi StateHasChanged.</summary>
    public event Action? Changed;

    /// <summary>
    /// Giành quyền chạy. Trả false nếu đã có hành động đang chạy — caller phải
    /// return ngay, KHÔNG được chạy tiếp.
    ///
    /// Đây là chốt thật sự chống double-submit: nó nguyên tử trong ngữ cảnh một
    /// tab WASM (đơn luồng), nên hai lần click liên tiếp thì lần thứ hai chắc
    /// chắn thấy IsBusy == true.
    /// </summary>
    public bool TryBegin()
    {
        if (IsBusy) return false;

        IsBusy = true;
        Changed?.Invoke();
        return true;
    }

    public void End()
    {
        if (!IsBusy) return;

        IsBusy = false;
        Changed?.Invoke();
    }
}
