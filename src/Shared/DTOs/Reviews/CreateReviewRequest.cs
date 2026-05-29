namespace PBL3.Shared.DTOs.Reviews
{
    public class CreateReviewRequest
    {
        public int ProductId { get; set; }
        public byte Rating { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
    }
}
