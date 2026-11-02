using Azure.Storage.Blobs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Application.Services;
using DocumentProcessingService.Infrastructure;
using DocumentProcessingService.Infrastructure.Pesistence;
using DocumentProcessingService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DocumentProcessingService.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddDbContext<DocumentDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.AddInfrastrucutre(builder.Configuration);
            builder.Services.AddScoped<IDocumentService>(x =>
                new DocumentService(
                    x.GetRequiredService<IDocumentRepository>(),
                    x.GetRequiredService<IBlobStorageService>()
                ));

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            app.MapGet("/test-blob", async (BlobServiceClient serviceClient) =>
            {
                try
                {
                    var containerClient =
                        serviceClient.GetBlobContainerClient("documents");

                    await containerClient.CreateIfNotExistsAsync();

                    var blobClient =
                        containerClient.GetBlobClient("test.txt");

                    using var stream = new MemoryStream(
                        Encoding.UTF8.GetBytes("Hello from Azurite!"));

                    await blobClient.UploadAsync(stream, overwrite: true);

                    return Results.Ok("Upload successful!");
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        title: "Azurite upload failed",
                        detail: ex.Message);
                }
            });

            // Configure
            // the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
