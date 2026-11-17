using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Domain.Entities;

namespace DocumentProcessingService.Application.Interfaces
{
    public interface IDocumentRepository
    {
        Task<Document> AddDocumentAsync(Document document, CancellationToken cancellationToken);
        Task<Document?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken);

        void Update(Document document, CancellationToken cancellationToken);

        Task DeleteAsync(Guid documentId, CancellationToken cancellationToken);

        Task<DocumentCompletionResult> MarkDocumentAsCompleted(Guid documentId, CancellationToken cancellationToken);
        Task SaveAsync(CancellationToken cancellationToken);
    }
}
