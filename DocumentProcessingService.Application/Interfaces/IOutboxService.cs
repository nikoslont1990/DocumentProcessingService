namespace DocumentProcessingService.Application.Interfaces
{
    public interface IOutboxService
    {
        Task AddAsync(string type, string content, CancellationToken cancellationToken);
    }
}
