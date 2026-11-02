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

        public async Task<Guid> AddDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            await _documentDbContext.Documents.AddAsync(document, cancellationToken);

            await _documentDbContext.SaveChangesAsync(cancellationToken);

            return document.Id;
        }

        public async Task<Document?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken)
        {
            if (documentId == Guid.Empty)
                throw new ArgumentException("Document ID cannot be empty.", nameof(documentId));

            var document = await _documentDbContext.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            return document;
        }

        public async Task UpdateAsync(Document document, CancellationToken cancellationToken)
        {
            _documentDbContext.Documents.Update(document);
            await _documentDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
