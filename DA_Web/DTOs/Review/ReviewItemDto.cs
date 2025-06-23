namespace DA_Web.DTOs.Review
{
    public class ReviewItemDto
    {
        public int Id { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; }
        public string? UserAvatar { get; set; }
        public List<string> Images { get; set; } = new List<string>();
    }
}