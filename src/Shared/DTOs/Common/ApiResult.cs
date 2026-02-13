namespace PBL3.Shared.DTOs.Common
{
    /// <summary>
    /// Standard API response wrapper. Tất cả API trả về format này.
    /// </summary>
    public class ApiResult<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResult<T> Ok(T data, string message = "Thao tác thành công.")
        {
            return new ApiResult<T> { Success = true, Message = message, Data = data };
        }

        public static ApiResult<T> Ok(string message = "Thao tác thành công.")
        {
            return new ApiResult<T> { Success = true, Message = message };
        }

        public static ApiResult<T> Fail(string message)
        {
            return new ApiResult<T> { Success = false, Message = message };
        }
    }
}
