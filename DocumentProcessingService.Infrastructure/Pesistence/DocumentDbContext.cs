using DocumentProcessingService.Domain.Entities;
using DocumentProcessingService.Infrastructure.Pesistence.Outbox;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocumentProcessingService.Infrastructure.Pesistence
{
    public class DocumentDbContext : DbContext
    {
        public DocumentDbContext(DbContextOptions<DocumentDbContext> options)
       : base(options)
        {
        }

        public DbSet<Document> Documents => Set<Document>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(DocumentDbContext).Assembly);
        }

    }
}
