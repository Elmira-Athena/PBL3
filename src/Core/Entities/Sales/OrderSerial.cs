using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 4. OrderSerials
    [Table("OrderSerials")]
    public class OrderSerial
    {
        [Key]
        public int Id { get; set; }
        public int OrderDetailId { get; set; }
        public int SerialId { get; set; }

        [ForeignKey("OrderDetailId")]
        public virtual OrderDetail OrderDetail { get; set; } = null!;
        [ForeignKey("SerialId")]
        public virtual ProductSerial Serial { get; set; } = null!;
    }
}
