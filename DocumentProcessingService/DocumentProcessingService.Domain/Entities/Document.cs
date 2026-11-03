
namespace DocumentProcessingService.Domain.Entities
{
    public class Document
    {
        public Guid Id { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string BlobName { get; set; } = string.Empty;
    }
}
