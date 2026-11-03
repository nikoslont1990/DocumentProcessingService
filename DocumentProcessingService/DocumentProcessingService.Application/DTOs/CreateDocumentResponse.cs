namespace DocumentProcessingService.Application.DTOs
{
    public class CreateDocumentResponse
    {
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
