using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocumentProcessingService.Application.Interfaces;

namespace DocumentProcessingService.Infrastructure.Services.Storage
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private const string ContainerName = "documents";

        public BlobStorageService(
            BlobServiceClient blobServiceClient)
        {
            _containerClient =
                blobServiceClient.GetBlobContainerClient(ContainerName);
        }

        public async Task<string> UploadDocumentAsync(Guid documentId, string fileName, Stream fileStream, CancellationToken cancellationToken)
        {
            try
            {
                await _containerClient.CreateIfNotExistsAsync();

                string blobName = $"{documentId}/{Path.GetFileName(fileName)}";

                var blobClient = _containerClient.GetBlobClient(blobName);

                await blobClient.UploadAsync(fileStream, overwrite: true, cancellationToken);

                return blobName;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to upload document to blob storage: {ex.Message}", ex);
            }
        }

        public async Task<Stream> DownloadAsync(string blobName, CancellationToken cancellationToken)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);

            return await blobClient.OpenReadAsync(
                cancellationToken: cancellationToken);
        }
    }
}
