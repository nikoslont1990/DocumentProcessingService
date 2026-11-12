namespace DocumentProcessingService.Application.DTOs
{
    public class OutboxMessageDTO
    {
        public Guid Id { get; set; }

        public string Type { get; set; }

        public string Content { get; set; }

        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
