namespace DocumentProcessingService.Api.Models
{
    public class CreateDocumentResponse
    {
        public Guid DocumentId { get; set; }
        public string FileName { get; set; }
        public string Status { get; set; }  
    }
}