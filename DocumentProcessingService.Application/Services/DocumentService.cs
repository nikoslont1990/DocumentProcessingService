using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Domain.Entities;

namespace DocumentProcessingService.Application.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IBlobStorageService _blobStorageService;
        private static Dictionary<Guid, string> documents = new Dictionary<Guid, string>();
        private static readonly Dictionary<string, string> AllowedFileTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                        { ".pdf", "application/pdf" },
                        { ".png", "image/png" },
                        { ".jpg", "image/jpeg" },
                        { ".jpeg", "image/jpeg" }
            };

        public DocumentService(IDocumentRepository documentRepository, IBlobStorageService blobStorageService)
        {
            _documentRepository = documentRepository;
            _blobStorageService = blobStorageService;
        }

        public async Task<Document> CreateDocument(string fileName, long fileSize, string contentType, Stream fileStream, CancellationToken cancellationToken)
        {
            // Guard clauses - Validations
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be empty.", nameof(fileName));

            string? fileExtension = Path.GetExtension(fileName);
            if (!AllowedFileTypes.TryGetValue(fileExtension, out _))
                throw new ArgumentException($"File type '{fileExtension}' is not supported.", nameof(fileName));

            // 2. Create document entity
            var document = new Document()
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                Status = "Pending",
                ContentType = contentType,
                CreatedAt = DateTime.UtcNow,
            };

            try
            {
                // 3. Save metadata to database
                var docId = await _documentRepository.AddDocumentAsync(document, cancellationToken);
                if (docId == Guid.Empty)
                    return null;


                // 4. ✅ INJECT AND UPLOAD TO BLOB STORAGE
                fileStream.Position = 0; // Reset stream
                string blobName = await _blobStorageService.UploadDocumentAsync(
                    document.Id,
                    fileName,
                    fileStream,
                    cancellationToken);

                if (blobName == null)
                    throw new InvalidOperationException("Failed to upload document to blob storage.");

                // 5. Update status
                document.BlobName = blobName;
                document.Status = "Completed";

                await _documentRepository.UpdateAsync(document, cancellationToken);

                return document;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create document: {ex.Message}", ex);
            }
        }

        public async Task<CreateDocumentResponse?> GetById(Guid documentId, CancellationToken cancellationToken)
        {
            if (documentId == Guid.Empty)
                throw new ArgumentException("Document ID cannot be empty.", nameof(documentId));

            Document? document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);

            if(document == null)
            {
                return null;
            }
            
            return new CreateDocumentResponse
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Status = document.Status,
                CreatedAt = document.CreatedAt
            };
        }

        public async Task<DocumentDownloadResult?> DownloadAsync(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            Document? document = await _documentRepository.GetByIdAsync(
                documentId,
                cancellationToken);

            if (document is null ||
                document.Status != "Completed")
            {
                return null;
            }

            var stream = await _blobStorageService.DownloadAsync(
                document.BlobName,
                cancellationToken);

            return new DocumentDownloadResult(
                stream,
                document.FileName,
                document.ContentType);
        }
    }
}
