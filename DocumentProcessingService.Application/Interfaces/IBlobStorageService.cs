namespace DocumentProcessingService.Application.Interfaces
{
    public interface IBlobStorageService
    {
        Task<string> UploadDocumentAsync(Guid documentId, string fileName, Stream fileStream, CancellationToken cancellationToken);

        Task<Stream> DownloadAsync(string blobName, CancellationToken cancellationToken);
    }
}
