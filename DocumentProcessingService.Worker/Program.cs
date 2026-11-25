using DocumentProcessingService.Application.Configuration;
using DocumentProcessingService.Infrastructure;
using DocumentProcessingService.Worker.Workers;

namespace DocumentProcessingService.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
           Host.CreateDefaultBuilder(args)
               .ConfigureServices((context, services) =>
               {
                   services.AddInfrastructure(context.Configuration);
                   services.AddOptions<DocumentProcessingQueueOptions>()
                           .Bind(context.Configuration.GetSection(DocumentProcessingQueueOptions.SectionName))
                           .Validate(o => !string.IsNullOrWhiteSpace(o.QueueName), "Workers:DocumentProcessing:QueueName is required.")
                           .ValidateOnStart();
                   services.AddHostedService<OutboxPublisherWorker>();
                   services.AddHostedService<DocumentProcessingWorker>();
               });
    }
}
