using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GDriveLight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncFolderLocalPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalPath",
                table: "SyncFolders",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalPath",
                table: "SyncFolders");

        }
    }
}
