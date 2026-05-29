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
}
