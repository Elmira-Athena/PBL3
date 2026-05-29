using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 7. Warranties
    [Table("Warranties")]
    public class Warranty
    {
        [Key]
        public int Id { get; set; }
        public int SerialId { get; set; }
        public Guid? CustomerId { get; set; }
        public int OrderId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public byte Status { get; set; } // 0: Active, 1: Expired, 2: Claimed

        [ForeignKey("SerialId")]
        public virtual ProductSerial Serial { get; set; } = null!;
        [ForeignKey("CustomerId")]
        public virtual AppUser? Customer { get; set; }
        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; } = null!;
    }
}
