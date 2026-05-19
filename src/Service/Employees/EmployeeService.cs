using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.Common;
using PBL3.Shared.DTOs.Employees;
using PBL3.Shared.DTOs.Products;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3.Service.Employees
{
    public class EmployeeService : IEmployeeService
    {
        private readonly IEmployeeRepository _employeeRepo;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IMemoryCache _cache;
        private readonly ILogger<EmployeeService> _logger;

        public EmployeeService(
            IEmployeeRepository employeeRepo,
            UserManager<AppUser> userManager,
            RoleManager<AppRole> roleManager,
            IMemoryCache cache,
            ILogger<EmployeeService> logger)
        {
            _employeeRepo = employeeRepo;
            _userManager = userManager;
            _roleManager = roleManager;
            _cache = cache;
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

            var techRole = await _roleManager.FindByNameAsync("Technician");
            var techUserIds = techRole == null ? new HashSet<Guid>() :
                (await _userManager.GetUsersInRoleAsync("Technician")).Select(u => u.Id).ToHashSet();

            var result = new PagedResult<EmployeeListDto>
            {
                Items = items.Select(u => MapToDto(u, techUserIds.Contains(u.Id))).ToList(),
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

            var isTechnician = await _userManager.IsInRoleAsync(user, "Technician");
            return ApiResult<EmployeeListDto>.Ok(MapToDto(user, isTechnician));
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

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Tạo tài khoản nhân viên thất bại: {Errors}", errors);
                return ApiResult<EmployeeListDto>.Fail("Khởi tạo tài khoản thất bại. " + errors);
            }

            await _userManager.AddToRoleAsync(user, "Employee");
            if (request.IsTechnician)
                await _userManager.AddToRoleAsync(user, "Technician");

            _logger.LogInformation("Tạo tài khoản nhân viên thành công: {Email} (Id: {UserId})", user.Email, user.Id);

            return ApiResult<EmployeeListDto>.Ok(MapToDto(user, request.IsTechnician), "Tạo tài khoản nhân viên thành công.");
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

            var currentlyTechnician = await _userManager.IsInRoleAsync(user, "Technician");
            if (request.IsTechnician && !currentlyTechnician)
                await _userManager.AddToRoleAsync(user, "Technician");
            else if (!request.IsTechnician && currentlyTechnician)
                await _userManager.RemoveFromRoleAsync(user, "Technician");

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return ApiResult<EmployeeListDto>.Fail("Cập nhật thông tin thất bại. " + errors);
            }

            _logger.LogInformation("Cập nhật thông tin nhân viên: {Email} (Id: {UserId})", user.Email, user.Id);
            return ApiResult<EmployeeListDto>.Ok(MapToDto(user, request.IsTechnician), "Cập nhật tài khoản thành công.");
        }

        public async Task<ApiResult<bool>> DeactivateAsync(Guid id, string? lockReason)
        {
            var user = await _employeeRepo.GetByIdWithProfileAsync(id);
            if (user == null)
                return ApiResult<bool>.Fail("Không tìm thấy nhân viên yêu cầu.");

            user.IsActive = false;
            user.LockReason = lockReason;
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return ApiResult<bool>.Fail("Cập nhật trạng thái thất bại.");

            _cache.Remove($"user_isactive_{id.ToString().ToLowerInvariant()}");
            _logger.LogInformation("Khóa tài khoản nhân viên: {Email} (Id: {UserId})", user.Email, user.Id);

            return ApiResult<bool>.Ok(true, "Khóa tài khoản thành công.");
        }

        public async Task<ApiResult<bool>> ReactivateAsync(Guid id)
        {
            var user = await _employeeRepo.GetByIdWithProfileAsync(id);
            if (user == null)
                return ApiResult<bool>.Fail("Không tìm thấy nhân viên yêu cầu.");

            user.IsActive = true;
            user.LockReason = null;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return ApiResult<bool>.Fail("Cập nhật trạng thái thất bại.");

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
            _cache.Remove($"user_isactive_{id.ToString().ToLowerInvariant()}");
            _logger.LogInformation("Mở khóa tài khoản nhân viên: {Email} (Id: {UserId})", user.Email, user.Id);
            return ApiResult<bool>.Ok(true, "Mở khóa tài khoản thành công.");
        }

        public async Task<ApiResult<List<EmployeeDto>>> GetTechniciansSimpleAsync()
        {
            var (items, _) = await _employeeRepo.GetPagedListAsync(null, true, null, 1, 500, null, false);
            var techUsers = await _userManager.GetUsersInRoleAsync("Technician");
            var techIds = techUsers.Select(u => u.Id).ToHashSet();
            var result = items.Where(u => techIds.Contains(u.Id))
                .Select(u => new EmployeeDto { Id = u.Id, FullName = u.Profile?.FullName ?? u.Email ?? "" })
                .ToList();
            return ApiResult<List<EmployeeDto>>.Ok(result);
        }

        private static EmployeeListDto MapToDto(AppUser user, bool isTechnician = false) => new()
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
            CreatedDate = user.CreatedDate,
            IsTechnician = isTechnician
        };
    }
}
