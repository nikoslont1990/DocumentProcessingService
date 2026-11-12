using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentProcessingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamedProcessedAtToPublishedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProcessedAt",
                table: "OutboxMessages",
                newName: "PublishedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PublishedAt",
                table: "OutboxMessages",
                newName: "ProcessedAt");
        }
    }
}
