using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace PBL3.Core.Entities
{
    // 2. AppRole kế thừa IdentityRole<Guid>
    public class AppRole : IdentityRole<Guid>
    {
        [MaxLength(250)]
        public string? Description { get; set; }
        [Required]
        [MaxLength(50)]
        public string RoleCode { get; set; } = string.Empty;
    }
}
