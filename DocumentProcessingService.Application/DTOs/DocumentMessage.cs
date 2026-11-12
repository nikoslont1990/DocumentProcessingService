namespace DocumentProcessingService.Application.DTOs
{
    public class DocumentMessage
    {
        public Guid DocumentId { get; set; }
        public string? BlobName { get; set;}
        public string? ContentType { get; set; }
    }
}
