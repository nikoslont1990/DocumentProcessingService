using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentProcessingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBlob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlobName",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlobName",
                table: "Documents");
        }
    }
}
