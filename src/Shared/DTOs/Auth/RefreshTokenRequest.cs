using FluentValidation;

namespace PBL3.Shared.DTOs.Auth
{
    /// <summary>
    /// DTO gửi lên để xin cấp lại cặp Token mới khi Access Token hết hạn.
    /// </summary>
    public class RefreshTokenRequest
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
    {
        public RefreshTokenRequestValidator()
        {
            RuleFor(x => x.AccessToken)
                .NotEmpty().WithMessage("Access Token không được để trống.");

            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Refresh Token không được để trống.");
        }
    }
}
