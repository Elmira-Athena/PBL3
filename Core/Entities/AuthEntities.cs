using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 1. Bảng Users
    [Table("AppUsers")]
    public class AppUser
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [MaxLength(256)]
        public string? UserName { get; set; }
        [MaxLength(256)]
        public string? NormalizedUserName { get; set; }
        [MaxLength(256)]
        public string? Email { get; set; }
        [MaxLength(256)]
        public string? NormalizedEmail { get; set; }
        public string? PasswordHash { get; set; }
        public string? SecurityStamp { get; set; }
        public string? ConcurrencyStamp { get; set; }
        public string? PhoneNumber { get; set; }

        public bool EmailConfirmed { get; set; }
        public bool PhoneNumberConfirmed { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public bool LockoutEnabled { get; set; }
        public int AccessFailedCount { get; set; }

        // B. THÔNG TIN CÁ NHÂN
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;
        public int Gender { get; set; } // 0: Male, 1: Female, 2: Other
        public DateTime? DateOfBirth { get; set; }
        [MaxLength(500)]
        public string? AvatarUrl { get; set; }

        // C. ĐỊA CHỈ MẶC ĐỊNH
        [MaxLength(255)]
        public string? Address { get; set; }
        [MaxLength(100)]
        public string? City { get; set; }

        // D. QUẢN TRỊ & TRẠNG THÁI
        public bool IsActive { get; set; } = true;
        public int Type { get; set; } // 0: Admin, 1: Employee, 2: Customer

        // E. AUDIT
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        // Navigation Properties
        public virtual ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }

    // 2. Bảng Roles
    [Table("AppRoles")]
    public class AppRole
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [MaxLength(256)]
        public string? Name { get; set; }
        [MaxLength(256)]
        public string? NormalizedName { get; set; }
        public string? ConcurrencyStamp { get; set; }

        [MaxLength(250)]
        public string? Description { get; set; }
        [Required]
        [MaxLength(50)]
        public string RoleCode { get; set; } = string.Empty;

        // Navigation Properties
        public virtual ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
    }

    // 3. Bảng AppUserRoles (Many-to-Many)
    [Table("AppUserRoles")]
    public class AppUserRole
    {
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } = null!;
        [ForeignKey("RoleId")]
        public virtual AppRole Role { get; set; } = null!;
    }

    // 4. Bảng RefreshTokens
    [Table("RefreshTokens")]
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }
        
        public Guid UserId { get; set; }

        [Required]
        public string Token { get; set; } = string.Empty;
        [Required]
        [MaxLength(100)]
        public string JwtId { get; set; } = string.Empty;
        public bool IsUsed { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime ExpiryDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } = null!;
    }
}
