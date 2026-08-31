using System.Net.Http;

namespace PBL3.Tools.LoadProbe.Infra;

public static class ConcurrentFire
{
    /// <summary>
    /// Bắn <paramref name="count"/> request và thả CÙNG MỘT LÚC.
    ///
    /// Vòng lặp `for { await Send() }` không đo được gì cả — nó tuần tự. Ngay cả
    /// `Select(...).ToArray()` rồi `WhenAll` cũng lệch nhau vì mỗi task bắt đầu ở
    /// thời điểm nó được lên lịch. Cổng TaskCompletionSource dưới đây giữ tất cả
    /// lại rồi mở một lượt, nên cửa sổ tranh chấp hẹp nhất có thể từ phía client.
    /// </summary>
    public static async Task<FireReport> FireAsync(
        int count,
        Func<int, Task<HttpResponseMessage>> send)
    {
        var report = new FireReport();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, count).Select(async i =>
        {
            await gate.Task;
            try
            {
                using var response = await send(i);
                report.Record(response.StatusCode);
                if (!response.IsSuccessStatusCode)
                {
                    report.RecordFailureSample(await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                report.RecordTransportError(ex.GetBaseException().Message);
            }
        }).ToArray();

        gate.SetResult();
        await Task.WhenAll(tasks);
        return report;
    }

    /// <summary>
    /// Bắn hai loạt request KHÁC NHAU xen kẽ nhau (kịch bản 7: POS bán serial S
    /// trong khi phê duyệt kiểm kê đánh S là thất thoát).
    /// </summary>
    public static async Task<(FireReport Left, FireReport Right)> FireInterleavedAsync(
        int leftCount, Func<int, Task<HttpResponseMessage>> left,
        int rightCount, Func<int, Task<HttpResponseMessage>> right)
    {
        var leftReport = new FireReport();
        var rightReport = new FireReport();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task Run(int i, Func<int, Task<HttpResponseMessage>> send, FireReport report)
        {
            await gate.Task;
            try
            {
                using var response = await send(i);
                report.Record(response.StatusCode);
                if (!response.IsSuccessStatusCode)
                {
                    report.RecordFailureSample(await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                report.RecordTransportError(ex.GetBaseException().Message);
            }
        }

        var tasks = Enumerable.Range(0, leftCount).Select(i => Run(i, left, leftReport))
            .Concat(Enumerable.Range(0, rightCount).Select(i => Run(i, right, rightReport)))
            .ToArray();

        gate.SetResult();
        await Task.WhenAll(tasks);
        return (leftReport, rightReport);
    }
}
