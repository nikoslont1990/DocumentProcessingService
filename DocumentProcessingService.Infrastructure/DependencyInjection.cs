using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Infrastructure.Pesistence;
using DocumentProcessingService.Infrastructure.Services;
using DocumentProcessingService.Infrastructure.Services.Outbox;
using DocumentProcessingService.Infrastructure.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace DocumentProcessingService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration
            .GetConnectionString("DefaultConnection");

            services.AddDbContext<DocumentDbContext>(options =>
                options.UseSqlServer(connectionString));


            var blobConnectionString = configuration.GetConnectionString("AzureBlobStorage");


            // Provide the client configuration options for connecting to Azure Blob Storage
            var blobOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2026_06_06)
            {
                Retry = {
                Delay = TimeSpan.FromSeconds(2),
                MaxRetries = 5,
                Mode = RetryMode.Exponential,
                MaxDelay = TimeSpan.FromSeconds(10),
                NetworkTimeout = TimeSpan.FromSeconds(100)
                }
            };  

            //var blobOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2026_06_06);

            services.AddSingleton(
                new BlobServiceClient(blobConnectionString, blobOptions));


            // Register ServiceBusClient
            var serviceBusConnectionString = configuration.GetConnectionString("ServiceBus");
            var options = new ServiceBusClientOptions
            {
                RetryOptions = new ServiceBusRetryOptions
                {
                    Mode = ServiceBusRetryMode.Exponential,
                    MaxRetries = 6,
                    Delay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(10),
                    TryTimeout = TimeSpan.FromSeconds(30)
                }
            };
            services.AddSingleton(new ServiceBusClient(serviceBusConnectionString,options));

            services.AddScoped<IOutboxRepository, OutboxRepository>();
            services.AddScoped<IOutboxService, OutboxService>();
            services.AddScoped<IBlobStorageService, BlobStorageService>();
            services.AddScoped<IMessageBus, AzureEmulatorMessageBus>();
            services.AddScoped<IOutboxPublisher, OutboxPublisher>();

            var repositoryType = configuration.GetSection("RepositorySettings:Type").Value;


            if(repositoryType == "InMemory")
            {
                //services.AddScoped<IDocumentRepository, InMemoryRepository>();
            }
            else if (repositoryType == "Database")
            {
                services.AddScoped<IDocumentRepository, DocumentRepository>();
                // Add database repository implementation here
            }
            else
            {
                throw new Exception("Invalid repository type specified in configuration.");
            }
            return services;
        }
    }
}
