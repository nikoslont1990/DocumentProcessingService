using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Domain.Entities;
using DocumentProcessingService.Infrastructure.Pesistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessingService.Infrastructure.Services
{
    public class DocumentRepository : IDocumentRepository
    {

        private readonly DocumentDbContext _documentDbContext;


        public DocumentRepository(DocumentDbContext documentDbContext)
        {
            _documentDbContext = documentDbContext;
        }

        public async Task<Document> AddDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            await _documentDbContext.Documents.AddAsync(document, cancellationToken);
            return document;
        }


        public async Task SaveAsync(CancellationToken cancellationToken)
        {
            await _documentDbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<Document?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken)
        {
            if (documentId == Guid.Empty)
                throw new ArgumentException("Document ID cannot be empty.", nameof(documentId));

            var document = await _documentDbContext.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            return document;
        }

        public void Update(Document document, CancellationToken cancellationToken)
        {
            _documentDbContext.Documents.Update(document);
        }

        public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken)
        {
            var document = await _documentDbContext.Documents.FindAsync(new object[] { documentId }, cancellationToken: cancellationToken);
            if(document != null)
            {
                _documentDbContext.Documents.Remove(document);
                await _documentDbContext.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<DocumentCompletionResult> MarkDocumentAsCompleted(Guid documentId, CancellationToken cancellationToken)
        {
            var document = await _documentDbContext.Documents.FindAsync(new object[] { documentId }, cancellationToken: cancellationToken);
            if (document == null)
            {
                //throw new Exception($"Document with ID {documentId} not found.");
                return DocumentCompletionResult.NotFound;
            }

            if (document.Status == DocumentStatus.Completed)
            {
                return DocumentCompletionResult.AlreadyCompleted; // idempotent success
            }

            var transitioned = document.TryTransition(DocumentStatus.Completed);
            if(!transitioned)
            {
                throw new InvalidOperationException($"Invalid transition to Completed. Current status: {document.Status}");
            }
            return DocumentCompletionResult.CompletedNow;
        }
    }
}
