namespace DocumentProcessingService.Application.DTOs
{
    public record DocumentDownloadResult(
        Stream Content,
        string FileName,
        string ContentType);
}
