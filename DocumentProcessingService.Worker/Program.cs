using DocumentProcessingService.Infrastructure;
using DocumentProcessingService.Worker.Workers;

namespace DocumentProcessingService.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
            //builder.Services.AddInfrastructure(builder.Configuration);
            ////builder.Services.AddScoped<IMessageBus, MessageBus>();
            //builder.Services.AddHostedService<OutboxPublisherWorker>();
            //builder.Services.AddHostedService<DocumentProcessingWorker>();


            //var host = builder.Build();
            //host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
           Host.CreateDefaultBuilder(args)
               .ConfigureServices((context, services) =>
               {
                   services.AddInfrastructure(context.Configuration);
                   services.AddHostedService<OutboxPublisherWorker>();
                   services.AddHostedService<DocumentProcessingWorker>();
               });
    }
}
