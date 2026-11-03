using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;


namespace DocumentProcessingService.Infrastructure.Services
{
    public class InMemoryRepository : IDocumentRepository
    {
        private readonly ConcurrentDictionary<Guid, Document> _documents = new();


        public Task<Document?> GetByIdAsync(
                Guid id,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _documents.TryGetValue(id, out var document);

            return Task.FromResult(document);
        }

        public Task<Guid> AddDocumentAsync(
                Document document,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_documents.TryAdd(document.Id, document))
                return Task.FromResult(Guid.Empty);


            return Task.FromResult(document.Id);
        }

        public Task UpdateAsync(Document document, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}

