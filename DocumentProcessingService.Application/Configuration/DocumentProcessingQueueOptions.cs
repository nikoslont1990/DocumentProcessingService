namespace DocumentProcessingService.Application.Configuration
{
    public sealed class DocumentProcessingQueueOptions
    {
        public const string SectionName = "Workers:DocumentProcessing";
        public string QueueName { get; init; } = string.Empty;
    }
}
