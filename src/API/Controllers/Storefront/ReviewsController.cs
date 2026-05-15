using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Service.Reviews;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Reviews;

namespace PBL3.API.Controllers.Storefront
{
    [ApiController]
    [Route("api/reviews")]
    [Produces("application/json")]
    public class ReviewsController : ControllerBase
    {
        private readonly IProductReviewService _reviewService;
        private readonly IValidator<CreateReviewRequest> _createValidator;

        public ReviewsController(
            IProductReviewService reviewService,
            IValidator<CreateReviewRequest> createValidator)
        {
            _reviewService = reviewService;
            _createValidator = createValidator;
        }

        /// <summary>
        /// Lấy danh sách đánh giá của một sản phẩm (phân trang, công khai).
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResult<PagedResult<ReviewDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReviews(
            [FromQuery] int productId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _reviewService.GetReviewsAsync(productId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Tạo đánh giá mới. Yêu cầu đăng nhập, mỗi khách chỉ được đánh giá 1 lần / sản phẩm.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(ApiResult<ReviewDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResult<ReviewDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
        {
            var validation = await _createValidator.ValidateAsync(request);
            if (!validation.IsValid)
            {
                var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
                return BadRequest(ApiResult<ReviewDto>.Fail(errors));
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResult<ReviewDto>.Fail("Người dùng chưa đăng nhập."));

            var result = await _reviewService.CreateReviewAsync(request, userId);
            if (!result.Success)
                return BadRequest(result);

            return CreatedAtAction(nameof(GetReviews), new { productId = request.ProductId }, result);
        }

        /// <summary>
        /// Xóa đánh giá. Chỉ chủ sở hữu mới được xóa.
        /// </summary>
        [HttpDelete("{reviewId:int}")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult<bool>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResult<bool>.Fail("Người dùng chưa đăng nhập."));

            var result = await _reviewService.DeleteReviewAsync(reviewId, userId);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
    }
}
