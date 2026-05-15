using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Employees;
using PBL3.Shared.DTOs.Products;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3.Service.Employees
{
    public class EmployeeService : IEmployeeService
    {
        private readonly IEmployeeRepository _employeeRepo;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<EmployeeService> _logger;

        public EmployeeService(
            IEmployeeRepository employeeRepo,
            UserManager<AppUser> userManager,
            ILogger<EmployeeService> logger)
        {
            _employeeRepo = employeeRepo;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<ApiResult<PagedResult<EmployeeListDto>>> GetPagedListAsync(EmployeeFilterRequest filter)
        {
            var (items, totalCount) = await _employeeRepo.GetPagedListAsync(
                filter.Keyword,
                filter.IsActive,
                filter.Gender,
                filter.PageNumber,
                filter.PageSize,
                filter.SortBy,
                filter.SortDescending);

            var result = new PagedResult<EmployeeListDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };

            return ApiResult<PagedResult<EmployeeListDto>>.Ok(result);
        }

        public async Task<ApiResult<EmployeeListDto>> GetByIdAsync(Guid id)
        {
            var user = await _employeeRepo.GetByIdWithProfileAsync(id);
            if (user == null)
                return ApiResult<EmployeeListDto>.Fail("Không tìm thấy nhân viên yêu cầu.");

            return ApiResult<EmployeeListDto>.Ok(MapToDto(user));
        }

        public async Task<ApiResult<EmployeeListDto>> CreateAsync(CreateEmployeeRequest request)
        {
            var existing = await _userManager.FindByEmailAsync(request.Email);
            if (existing != null)
                return ApiResult<EmployeeListDto>.Fail("Email đã được sử dụng.");

            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = request.Email,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                IsActive = true,
                Type = 1,
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

            var generatedPassword = GenerateRandomPassword(12);
            var createResult = await _userManager.CreateAsync(user, generatedPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Tạo tài khoản nhân viên thất bại: {Errors}", errors);
                return ApiResult<EmployeeListDto>.Fail("Khởi tạo tài khoản thất bại. " + errors);
            }

            await _userManager.AddToRoleAsync(user, "Employee");
            _logger.LogInformation("Tạo tài khoản nhân viên thành công: {Email} (Id: {UserId})", user.Email, user.Id);

            return ApiResult<EmployeeListDto>.Ok(MapToDto(user), "Tạo tài khoản nhân viên thành công.");
        }

        public async Task<ApiResult<EmployeeListDto>> UpdateAsync(Guid id, UpdateEmployeeRequest request)
        {
            var user = await _employeeRepo.GetByIdWithProfileAsync(id);
            if (user == null)
                return ApiResult<EmployeeListDto>.Fail("Không tìm thấy nhân viên yêu cầu.");

            if (user.Profile == null)
                user.Profile = new UserProfile { UserId = user.Id };

            user.Profile.FullName = request.FullName.Trim();
            user.Profile.Gender = request.Gender;
            user.Profile.DateOfBirth = request.DateOfBirth;
            user.Profile.AvatarUrl = request.AvatarUrl;
            user.Profile.Address = request.Address?.Trim();
            user.Profile.City = request.City?.Trim();

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return ApiResult<EmployeeListDto>.Fail("Cập nhật thông tin thất bại. " + errors);
            }

            _logger.LogInformation("Cập nhật thông tin nhân viên: {Email} (Id: {UserId})", user.Email, user.Id);
            return ApiResult<EmployeeListDto>.Ok(MapToDto(user), "Cập nhật tài khoản thành công.");
        }

        public async Task<ApiResult<bool>> DeactivateAsync(Guid id)
        {
            var user = await _employeeRepo.GetByIdWithProfileAsync(id);
            if (user == null)
                return ApiResult<bool>.Fail("Không tìm thấy nhân viên yêu cầu.");

            user.IsActive = false;
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return ApiResult<bool>.Fail("Cập nhật trạng thái thất bại.");

            await _userManager.UpdateSecurityStampAsync(user);
            _logger.LogInformation("Khóa tài khoản nhân viên: {Email} (Id: {UserId})", user.Email, user.Id);

            return ApiResult<bool>.Ok(true, "Khóa tài khoản thành công.");
        }

        public async Task<ApiResult<bool>> ReactivateAsync(Guid id)
        {
            var user = await _employeeRepo.GetByIdWithProfileAsync(id);
            if (user == null)
                return ApiResult<bool>.Fail("Không tìm thấy nhân viên yêu cầu.");

            user.IsActive = true;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return ApiResult<bool>.Fail("Cập nhật trạng thái thất bại.");

            _logger.LogInformation("Mở khóa tài khoản nhân viên: {Email} (Id: {UserId})", user.Email, user.Id);
            return ApiResult<bool>.Ok(true, "Mở khóa tài khoản thành công.");
        }

        private static EmployeeListDto MapToDto(AppUser user) => new()
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

        private static string GenerateRandomPassword(int length)
        {
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "1234567890";
            const string special = "!@#$%^&*()";

            var random = new Random();
            var password = new char[length];

            password[0] = lowercase[random.Next(lowercase.Length)];
            password[1] = uppercase[random.Next(uppercase.Length)];
            password[2] = digits[random.Next(digits.Length)];
            password[3] = special[random.Next(special.Length)];

            var allChars = lowercase + uppercase + digits + special;
            for (int i = 4; i < length; i++)
                password[i] = allChars[random.Next(allChars.Length)];

            for (int i = 0; i < length; i++)
            {
                int r = i + random.Next(length - i);
                (password[r], password[i]) = (password[i], password[r]);
            }

            return new string(password);
        }
    }
}
