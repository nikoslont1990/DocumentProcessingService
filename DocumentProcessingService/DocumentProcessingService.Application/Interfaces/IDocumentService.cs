using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Domain.Entities;

namespace DocumentProcessingService.Application.Interfaces
{
    public interface IDocumentService
    {
        Task<CreateDocumentResponse?> GetById(Guid documentId, CancellationToken cancellationToken);

        Task<Document?> CreateDocument(string fileName, long fileSize, string contentType, Stream fileStream, CancellationToken cancellationToken);
        Task<DocumentDownloadResult?> DownloadAsync(Guid documentId, CancellationToken cancellationToken);
    }
}
