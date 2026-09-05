using System.Text.Json;
using System.Text.Json.Serialization;

namespace PBL3.API.Json
{
    /// <summary>
    /// Chuẩn hoá <see cref="DateTimeKind"/> của mọi <see cref="DateTime"/> đi qua JSON về
    /// <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    /// <remarks>
    /// 🚨 <b>Không phải chuyện thẩm mỹ — thiếu lớp này thì mọi form có ngày tháng trả 500.</b>
    /// Đã đo trực tiếp trên PostgreSQL 17 + Npgsql 10 (2026-09-05): cả 57 cột ngày giờ của schema
    /// là <c>timestamp with time zone</c>, và Npgsql <b>NÉM</b> khi ghi <c>DateTime</c> có
    /// <c>Kind</c> khác <c>Utc</c>:
    /// <code>
    /// Kind=Unspecified -> ArgumentException: Cannot write DateTime with Kind=Unspecified
    ///                     to PostgreSQL type 'timestamp with time zone', only UTC is supported.
    /// Kind=Local       -> ArgumentException (cùng câu)
    /// Kind=Utc         -> OK
    /// </code>
    /// Mà <c>System.Text.Json</c> sinh ra đúng <c>Kind=Unspecified</c> cho chuỗi <b>không có hậu
    /// tố <c>Z</c></b> — tức là dạng mà <c>MudDatePicker</c> của Blazor gửi lên
    /// (<c>"2026-01-01T00:00:00"</c>). Nói cách khác: không có converter này, admin
    /// <b>không tạo nổi voucher</b> và <b>không sửa nổi ngày sinh</b>.
    ///
    /// ⚠️ <b>Ghi lại một điều bản kế hoạch đợt 7 nói SAI</b>, để phiên sau không đi tìm nhầm
    /// triệu chứng: kế hoạch dự đoán sai <c>Kind</c> sẽ làm <i>"cửa sổ hiệu lực voucher lệch 7
    /// giờ"</i> — tức hỏng <b>âm thầm</b>. Thực tế đo được là hỏng <b>ồn ào</b>: exception ngay lúc
    /// ghi. Đây là hướng hỏng dễ chịu hơn nhiều, nhưng nó cũng có nghĩa đây là lỗi <b>chặn đường</b>,
    /// không phải lỗi tinh vi để dành sau.
    ///
    /// 🎯 <b>Vì sao coi chuỗi KHÔNG có múi giờ là UTC, chứ không phải giờ Việt Nam.</b> Đây là
    /// lựa chọn <b>giữ nguyên hành vi cũ</b>, không phải lựa chọn "đúng nhất về nghiệp vụ". Trên
    /// SQL Server các cột này là <c>datetime2</c> (không mang múi giờ): thứ admin gõ được lưu
    /// nguyên văn rồi đem so thẳng với <c>DateTime.UtcNow</c> ở
    /// <c>VoucherRepository</c> — nghĩa là hệ thống <b>vốn đã</b> diễn giải giờ admin nhập là giờ
    /// UTC. <c>SpecifyKind(Utc)</c> tái lập chính xác điều đó.
    ///
    /// Nếu muốn "00:00 admin gõ" nghĩa là 00:00 <b>giờ Việt Nam</b> thì đó là một <b>thay đổi
    /// nghiệp vụ</b> (cửa sổ hiệu lực dịch 7 giờ so với hiện nay) — phải quyết riêng và đo riêng,
    /// tuyệt đối không đính kèm vào một đợt chuyển engine, nơi nó sẽ bị nhầm là hồi quy.
    /// </remarks>
    public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetDateTime();
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                // Chuỗi CÓ nêu offset (vd "+07:00") — người gửi đã nói rõ mốc, tôn trọng và quy đổi.
                DateTimeKind.Local => value.ToUniversalTime(),
                // Chuỗi KHÔNG nêu gì — diễn giải là UTC, xem phần lý lẽ ở trên.
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
