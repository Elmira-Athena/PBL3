using System.Text.Json.Serialization;

namespace Client.Services.Address
{
    // Hình dạng của Provinces API v2 (provinces.open-api.vn) — API bên ngoài,
    // KHÔNG bọc ApiResult<T>, nên không đặt trong Shared.
    public class ProvinceItem
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }

    public class ProvinceDetail
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("wards")] public List<WardItem> Wards { get; set; } = new();
    }

    public class WardItem
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }
}
