using Azure.Storage.Blobs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Infrastructure.Pesistence;
using DocumentProcessingService.Infrastructure.Services;
using DocumentProcessingService.Infrastructure.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace DocumentProcessingService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastrucutre(this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration
            .GetConnectionString("DefaultConnection");

            services.AddDbContext<DocumentDbContext>(options =>
                options.UseSqlServer(connectionString));


            var blobConnectionString = configuration.GetConnectionString("AzureBlobStorage");

            var blobOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2026_06_06);

            services.AddSingleton(
                new BlobServiceClient(blobConnectionString, blobOptions));

            services.AddScoped<IBlobStorageService, BlobStorageService>();


            var repositoryType = configuration.GetSection("RepositorySettings:Type").Value;


            if(repositoryType == "InMemory")
            {
                services.AddScoped<IDocumentRepository, InMemoryRepository>();
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
