using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Customers;
using PBL3.Shared.DTOs.Products;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3.Service.Customers
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepo;
        private readonly ICartRepository _cartRepo;
        private readonly UserManager<AppUser> _userManager;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(
            ICustomerRepository customerRepo,
            ICartRepository cartRepo,
            UserManager<AppUser> userManager,
            IMemoryCache cache,
            ILogger<CustomerService> logger)
        {
            _customerRepo = customerRepo;
            _cartRepo = cartRepo;
            _userManager = userManager;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ApiResult<PagedResult<CustomerDto>>> GetPagedListAsync(CustomerFilterRequest filter)
        {
            var (items, totalCount) = await _customerRepo.GetPagedListAsync(
                filter.Keyword,
                filter.IsActive,
                filter.Gender,
                filter.PageNumber,
                filter.PageSize,
                filter.SortBy,
                filter.SortDescending);

            var dtos = items.Select(MapToDto).ToList();

            var result = new PagedResult<CustomerDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };

            return ApiResult<PagedResult<CustomerDto>>.Ok(result);
        }

        public async Task<ApiResult<CustomerDetailDto>> GetByIdAsync(Guid id)
        {
            var user = await _customerRepo.GetByIdWithProfileAsync(id);
            if (user == null)
            {
                return ApiResult<CustomerDetailDto>.Fail("Không tìm thấy khách hàng yêu cầu.");
            }

            var dto = new CustomerDetailDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                FullName = user.Profile?.FullName ?? string.Empty,
                Gender = user.Profile?.Gender ?? 0,
                DateOfBirth = user.Profile?.DateOfBirth,
                AvatarUrl = user.Profile?.AvatarUrl,
                Address = user.Profile?.Address,
                City = user.Profile?.City,
                IsActive = user.IsActive,
                CreatedDate = user.CreatedDate
            };

            // Get recent orders (limit 10 based on clarification)
            var recentOrders = await _customerRepo.GetRecentOrdersAsync(id, 10);
            dto.RecentOrders = recentOrders.Select(o => new CustomerOrderHistoryDto
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                OrderDate = o.OrderDate,
                TotalAmount = o.TotalAmount,
                Status = o.Status
            }).ToList();

            // Get cart items
            var cartItems = await _cartRepo.GetCartItemsByUserAsync(id);
            dto.CartItems = cartItems.Select(c => new CustomerCartItemDto
            {
                Id = c.Id,
                VariantId = c.VariantId,
                ProductName = c.Variant?.Product?.Name ?? string.Empty,
                VariantName = c.Variant?.VariantName ?? string.Empty,
                Price = c.Variant?.Price ?? 0,
                Quantity = c.Quantity,
                ThumbnailUrl = c.Variant?.Images?.FirstOrDefault(i => i.IsMain)?.ImageUrl
            }).ToList();

            return ApiResult<CustomerDetailDto>.Ok(dto);
        }

        public async Task<ApiResult<CustomerDto>> CreateAsync(CreateCustomerRequest request)
        {
            // Kiểm tra trùng lặp email/phone (UserManager tự làm hoặc làm thủ công)
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var existingUserByEmail = await _userManager.FindByEmailAsync(request.Email);
                if (existingUserByEmail != null)
                    return ApiResult<CustomerDto>.Fail("Email đã được sử dụng.");
            }

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneExists = await _userManager.Users
                    .AnyAsync(u => u.PhoneNumber == request.PhoneNumber && !u.IsDeleted);
                if (phoneExists)
                    return ApiResult<CustomerDto>.Fail("Số điện thoại đã được sử dụng.");
            }

            // Create user
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = request.Email, // Email as username
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                IsActive = true,
                Type = 2, // 2: Customer
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false,
                Profile = new UserProfile
                {
                    FullName = request.FullName.Trim(),
                    Gender = request.Gender,
                    DateOfBirth = request.DateOfBirth,
                    Address = request.Address?.Trim(),
                    City = request.City?.Trim()
                }
            };

            // Sinh mật khẩu ngẫu nhiên cho user
            var generatedPassword = GenerateRandomPassword(12);

            var createResult = await _userManager.CreateAsync(user, generatedPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Tạo tài khoản thất bại: {Errors}", errors);
                return ApiResult<CustomerDto>.Fail("Khởi tạo tài khoản thất bại. " + errors);
            }

            // Assign role
            await _userManager.AddToRoleAsync(user, "Customer");

            _logger.LogInformation("Tạo tài khoản khách hàng thành công: {Email} (Id: {UserId})", user.Email, user.Id);

            return ApiResult<CustomerDto>.Ok(MapToDto(user), "Tạo tài khoản thành công.");
        }

        public async Task<ApiResult<CustomerDto>> UpdateAsync(Guid id, UpdateCustomerRequest request)
        {
            var user = await _customerRepo.GetByIdWithProfileAsync(id);
            if (user == null)
                return ApiResult<CustomerDto>.Fail("Không tìm thấy khách hàng yêu cầu.");

            // Cập nhật Profile
            if (user.Profile == null)
            {
                user.Profile = new UserProfile { UserId = user.Id };
            }

            user.Profile.FullName = request.FullName.Trim();
            user.Profile.Gender = request.Gender;
            user.Profile.DateOfBirth = request.DateOfBirth;
            user.Profile.AvatarUrl = request.AvatarUrl;
            user.Profile.Address = request.Address?.Trim();
            user.Profile.City = request.City?.Trim();

            // Note: Email and PhoneNumber cannot be updated by Admin per clarification rules

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return ApiResult<CustomerDto>.Fail("Cập nhật thông tin thất bại. " + errors);
            }

            _logger.LogInformation("Cập nhật thông tin khách hàng: {Email} (Id: {UserId})", user.Email, user.Id);

            return ApiResult<CustomerDto>.Ok(MapToDto(user), "Cập nhật tài khoản thành công.");
        }

        public async Task<ApiResult<bool>> DeactivateAsync(Guid id, string? lockReason)
        {
            var user = await _customerRepo.GetByIdWithProfileAsync(id);
            if (user == null)
                return ApiResult<bool>.Fail("Không tìm thấy khách hàng yêu cầu.");

            // Kiểm tra ràng buộc
            var hasPendingOrders = await _customerRepo.HasPendingOrdersAsync(id);
            if (hasPendingOrders)
            {
                return ApiResult<bool>.Fail("Tài khoản đang có đơn hàng chờ xử lý, không thể khóa.");
            }

            user.IsActive = false;
            user.LockReason = lockReason;
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return ApiResult<bool>.Fail("Cập nhật trạng thái thất bại.");
            }

            _cache.Remove($"user_isactive_{id.ToString().ToLowerInvariant()}");
            _logger.LogInformation("Khóa tài khoản khách hàng: {Email} (Id: {UserId})", user.Email, user.Id);

            return ApiResult<bool>.Ok(true, "Khóa tài khoản thành công.");
        }

        public async Task<ApiResult<bool>> ReactivateAsync(Guid id)
        {
            var user = await _customerRepo.GetByIdWithProfileAsync(id);
            if (user == null)
                return ApiResult<bool>.Fail("Không tìm thấy khách hàng yêu cầu.");

            user.IsActive = true;
            user.LockReason = null;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return ApiResult<bool>.Fail("Cập nhật trạng thái thất bại.");

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
            _cache.Remove($"user_isactive_{id.ToString().ToLowerInvariant()}");
            _logger.LogInformation("Mở khóa tài khoản khách hàng: {Email} (Id: {UserId})", user.Email, user.Id);
            return ApiResult<bool>.Ok(true, "Mở khóa tài khoản thành công.");
        }

        // ===================================
        // PRIVATE HELPERS
        // ===================================
        private static CustomerDto MapToDto(AppUser user)
        {
            return new CustomerDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                FullName = user.Profile?.FullName ?? string.Empty,
                Gender = user.Profile?.Gender ?? 0,
                DateOfBirth = user.Profile?.DateOfBirth,
                AvatarUrl = user.Profile?.AvatarUrl,
                Address = user.Profile?.Address,
                City = user.Profile?.City,
                IsActive = user.IsActive,
                LockReason = user.LockReason,
                CreatedDate = user.CreatedDate
            };
        }

        private static string GenerateRandomPassword(int length)
        {
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "1234567890";
            const string special = "!@#$%^&*()";

            var random = new Random();
            var password = new char[length];

            // Ensure requirements
            password[0] = lowercase[random.Next(lowercase.Length)];
            password[1] = uppercase[random.Next(uppercase.Length)];
            password[2] = digits[random.Next(digits.Length)];
            password[3] = special[random.Next(special.Length)];

            var allChars = lowercase + uppercase + digits + special;
            for (int i = 4; i < length; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }

            // Shuffle
            for (int i = 0; i < length; i++)
            {
                int r = i + random.Next(length - i);
                var temp = password[r];
                password[r] = password[i];
                password[i] = temp;
            }

            return new string(password);
        }
    }
}
