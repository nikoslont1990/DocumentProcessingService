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

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();


            // Add services to the container.
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            // Add services to the container
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddScoped<IDocumentService>(x =>
                new DocumentService(
                    
                    x.GetRequiredService<IDocumentRepository>(),
                    x.GetRequiredService<IBlobStorageService>(),
                    x.GetRequiredService<IOutboxService>(),
                    x.GetRequiredService<ILogger<DocumentService>>()
                ));

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI();

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
