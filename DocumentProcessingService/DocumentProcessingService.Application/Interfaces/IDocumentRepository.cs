using DocumentProcessingService.Domain.Entities;

namespace DocumentProcessingService.Application.Interfaces
{
    public interface IDocumentRepository
    {
        /// <summary>
        /// Adds a document to the repository.
        /// </summary>
        /// <param name="document">The document to add.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<Guid> AddDocumentAsync(Document document, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a document by its ID.
        /// </summary>
        /// <param name="documentId">The ID of the document to retrieve.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<Document?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken);

        Task UpdateAsync(Document document, CancellationToken cancellationToken);
    }
}
