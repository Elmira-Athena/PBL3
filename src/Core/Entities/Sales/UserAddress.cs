using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 8. UserAddresses
    [Table("UserAddresses")]
    public class UserAddress
    {
        [Key]
        public int Id { get; set; }
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ReceiverName { get; set; } = string.Empty;
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;
        [Required]
        [MaxLength(255)]
        public string AddressLine { get; set; } = string.Empty;
        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } = null!;
    }
}
