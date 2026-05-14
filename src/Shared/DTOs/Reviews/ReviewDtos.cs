namespace PBL3.Shared.DTOs.Reviews
{
    public class ReviewDto
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string? UserAvatarUrl { get; set; }
        public byte Rating { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreateReviewRequest
    {
        public int ProductId { get; set; }
        public byte Rating { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
    }
}
