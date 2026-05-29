using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 2. UserProfile — Thông tin cá nhân (1:1 với AppUser, shared primary key)
    [Table("UserProfiles")]
    public class UserProfile
    {
        [Key]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(110)]
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
}
