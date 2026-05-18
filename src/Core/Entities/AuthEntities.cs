using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace PBL3.Core.Entities
{
    // 1. AppUser kế thừa IdentityUser<Guid>
    public class AppUser : IdentityUser<Guid>
    {
        // B. QUẢN TRỊ & TRẠNG THÁI
        public bool IsActive { get; set; } = true;
        [MaxLength(500)]
        public string? LockReason { get; set; }
        public byte Type { get; set; } // 0: Admin, 1: Employee, 2: Customer

        // C. REFRESH TOKEN (Lưu trực tiếp trên User, 1-1)
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        // D. AUDIT
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        // Navigation Properties
        public virtual UserProfile? Profile { get; set; }
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }

    // 2. UserProfile — Thông tin cá nhân (1:1 với AppUser, shared primary key)
    [Table("UserProfiles")]
    public class UserProfile
    {
        [Key]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;
        public byte Gender { get; set; } // 0: Male, 1: Female, 2: Other
        public DateTime? DateOfBirth { get; set; }
        [MaxLength(500)]
        public string? AvatarUrl { get; set; }

        // Địa chỉ mặc định
        [MaxLength(255)]
        public string? Address { get; set; }
        [MaxLength(100)]
        public string? City { get; set; }

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } = null!;
    }

    // 2. AppRole kế thừa IdentityRole<Guid>
    public class AppRole : IdentityRole<Guid>
    {
        [MaxLength(250)]
        public string? Description { get; set; }
        [Required]
        [MaxLength(50)]
        public string RoleCode { get; set; } = string.Empty;
    }

    // 3. RefreshTokens
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
