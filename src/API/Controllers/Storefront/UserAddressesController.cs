using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;
using System.Security.Claims;

namespace PBL3.API.Controllers.Storefront
{
    [ApiController]
    [Route("api/storefront/user-addresses")]
    [Produces("application/json")]
    [Authorize]
    public class UserAddressesController : ControllerBase
    {
        private readonly IUserAddressRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public UserAddressesController(IUserAddressRepository repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Lấy danh sách địa chỉ giao hàng của người dùng hiện tại
        /// </summary>
        [HttpGet("my-addresses")]
        [ProducesResponseType(typeof(ApiResult<List<UserAddressDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyAddresses()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(ApiResult<List<UserAddressDto>>.Fail("Người dùng chưa đăng nhập."));
            }

            var addresses = await _repository.GetByUserIdAsync(userId);

            var dtos = addresses.Select(a => new UserAddressDto
            {
                Id = a.Id,
                UserId = a.UserId,
                ReceiverName = a.ReceiverName,
                PhoneNumber = a.PhoneNumber,
                AddressLine = a.AddressLine,
                City = a.City,
                IsDefault = a.IsDefault
            }).ToList();

            return Ok(ApiResult<List<UserAddressDto>>.Ok(dtos, "Lấy danh sách địa chỉ thành công."));
        }

        /// <summary>
        /// Thêm mới một địa chỉ giao hàng
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<int>), StatusCodes.Status201Created)]
        public async Task<IActionResult> AddAddress([FromBody] UserAddressDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(ApiResult<int>.Fail("Người dùng chưa đăng nhập."));
            }

            var entity = new PBL3.Core.Entities.UserAddress
            {
                UserId = userId,
                ReceiverName = request.ReceiverName,
                PhoneNumber = request.PhoneNumber,
                AddressLine = request.AddressLine,
                City = request.City,
                IsDefault = request.IsDefault
            };

            await _repository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(); // <-- Quan trọng: Lưu xuống DB để có Id thực

            return Ok(ApiResult<int>.Ok(entity.Id, "Thêm địa chỉ thành công."));
        }
    }
}
