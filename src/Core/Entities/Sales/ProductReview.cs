using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3.Core.Entities
{
    // 9. ProductReviews
    [Table("ProductReviews")]
    public class ProductReview
    {
        [Key]
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Guid UserId { get; set; }

        public byte Rating { get; set; } // 1-5

        [MaxLength(200)]
        public string? Title { get; set; }
        [MaxLength(2000)]
        public string? Content { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; } = null!;
    }
}
