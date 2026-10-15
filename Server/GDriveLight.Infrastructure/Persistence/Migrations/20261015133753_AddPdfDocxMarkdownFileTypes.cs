using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GDriveLight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfDocxMarkdownFileTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "FileTypes",
                columns: new[] { "Id", "Extension", "MimeType", "Name" },
                values: new object[,]
                {
                    { 13, ".pdf", "application/pdf", "PDF" },
                    { 14, ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "DOCX" },
                    { 15, ".md", "text/markdown", "Markdown" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "FileTypes",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "FileTypes",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "FileTypes",
                keyColumn: "Id",
                keyValue: 15);
        }
    }
}
