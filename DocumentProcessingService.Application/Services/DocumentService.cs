using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DocumentProcessingService.Application.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IBlobStorageService _documentStorage;
        private readonly ILogger<DocumentService> _logger;
        private static readonly Dictionary<string, string> AllowedFileTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                        { ".pdf", "application/pdf" },
                        { ".png", "image/png" },
                        { ".jpg", "image/jpeg" },
                        { ".jpeg", "image/jpeg" }
            };

        public DocumentService(IDocumentRepository documentRepository, IBlobStorageService documentStorage,
            ILogger<DocumentService> logger)
        {
            _documentRepository = documentRepository;
            _documentStorage = documentStorage;
            _logger = logger;
        }

        public async Task<Document> CreateDocument(string fileName, long fileSize, string contentType, Stream fileStream, CancellationToken cancellationToken)
        {
            // Guard clauses - Validations
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be empty.", nameof(fileName));

            string? fileExtension = Path.GetExtension(fileName);
            if (!AllowedFileTypes.TryGetValue(fileExtension, out _))
                throw new ArgumentException($"File type '{fileExtension}' is not supported.", nameof(fileName));

            var document = new Document()
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                Status = "Pending",
                ContentType = contentType,
                CreatedAt = DateTime.UtcNow,
            };

            string? blobName = null;

            try
            {
                // Step 1: Save metadata before uploading the file.
                _logger.LogInformation("Save document metadata started");
                var docId = await _documentRepository.AddDocumentAsync(document, cancellationToken);
                if (docId == Guid.Empty)
                    return null;


                // Step 2: Upload the file.
                _logger.LogInformation("Document upload started");
                blobName = await _documentStorage.UploadDocumentAsync(
                    document.Id,
                    fileName,
                    fileStream,
                    cancellationToken);

                // Step 3: Update metadata after successful upload.
                document.BlobName = blobName;
                document.Status = "Completed";

                await _documentRepository.UpdateAsync(document, cancellationToken);

                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,"Upload failed for document {DocumentId}",document.Id);
                // Compensation: remove the blob if it was uploaded.
                if (blobName is not null)
                {
                    try
                    {
                        await _documentStorage.DeleteAsync(
                            blobName,
                            CancellationToken.None);
                    }
                    catch (Exception cleanupException)
                    {
                        _logger.LogError(cleanupException, "Failed to delete blob {BlobName}", blobName);
                    }
                }
                // Try to mark the document as failed.
                document.Status = "Failed";

                try
                {
                    await _documentRepository.UpdateAsync(document, CancellationToken.None);
                }
                catch (Exception statusException)
                {   
                    _logger.LogError(statusException, "Failed to mark document {DocumentId} as Failed", document.Id);
                }
                throw;
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

            var stream = await _documentStorage.DownloadAsync(
                document.BlobName,
                cancellationToken);

            return new DocumentDownloadResult(
                stream,
                document.FileName,
                document.ContentType);
        }
    }
}
