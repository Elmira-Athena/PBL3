using Microsoft.AspNetCore.Mvc.Filters;

namespace PBL3.API.Filters
{
    /// <summary>
    /// Chặn trần <c>pageSize</c> cho MỌI endpoint, kể cả endpoint viết sau này.
    /// </summary>
    /// <remarks>
    /// VẤN ĐỀ: 25/26 controller nhận <c>pageSize</c> thẳng từ query string mà không
    /// chặn trần. Đường tấn công rẻ nhất là tìm kiếm sản phẩm — anonymous, chưa có
    /// rate limit, và <c>?pageSize=1000000</c> ép server vật hoá một triệu dòng.
    ///
    /// VÌ SAO LÀ ACTION FILTER: sửa 26 chữ ký hàm là việc một lần, còn filter phủ luôn
    /// cả những endpoint chưa ai viết. Đây là lớp cắt ngang; lớp quy ước (<c>PagedRequest</c>
    /// ở tầng Shared, client dùng chung) làm sau và bổ sung chứ không thay thế —
    /// quy ước đặt tên trong repo KHÔNG nhất quán (có chỗ <c>PageIndex</c>, có chỗ
    /// <c>PageNumber</c>), nên filter dựa trên quy ước sẽ âm thầm bỏ sót đúng những
    /// chỗ lệch chuẩn nếu đứng một mình.
    /// </remarks>
    public class ClampPageSizeFilter : IAsyncActionFilter
    {
        /// <summary>Trần số bản ghi mỗi trang.</summary>
        public const int MaxPageSize = 100;

        private static readonly string[] SizeNames =
            { "pagesize", "take", "limit", "pagesizes", "size" };

        private static readonly string[] PageNames =
            { "page", "pagenumber", "pageindex", "pageno" };

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Tham số rời (VD: [FromQuery] int pageSize = 20)
            foreach (var key in context.ActionArguments.Keys.ToList())
            {
                var value = context.ActionArguments[key];
                if (value is int intValue)
                {
                    var clamped = Clamp(key, intValue);
                    if (clamped != intValue)
                        context.ActionArguments[key] = clamped;
                }
                else if (value is not null && !value.GetType().IsPrimitive && value is not string)
                {
                    ClampProperties(value);
                }
            }

            await next();
        }

        private static void ClampProperties(object model)
        {
            foreach (var prop in model.GetType().GetProperties())
            {
                if (prop.PropertyType != typeof(int) || !prop.CanRead || !prop.CanWrite)
                    continue;

                var current = (int)(prop.GetValue(model) ?? 0);
                var clamped = Clamp(prop.Name, current);
                if (clamped != current)
                    prop.SetValue(model, clamped);
            }
        }

        private static int Clamp(string name, int value)
        {
            var key = name.ToLowerInvariant();

            if (SizeNames.Contains(key))
                return value < 1 ? 1 : (value > MaxPageSize ? MaxPageSize : value);

            // Trang âm hoặc 0 làm Skip() sinh số âm — SQL Server ném lỗi.
            if (PageNames.Contains(key))
                return value < 1 ? 1 : value;

            return value;
        }
    }
}
