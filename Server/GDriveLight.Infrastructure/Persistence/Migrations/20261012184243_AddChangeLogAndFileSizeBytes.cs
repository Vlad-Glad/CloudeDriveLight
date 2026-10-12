using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GDriveLight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeLogAndFileSizeBytes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncFileStates");

            migrationBuilder.DropColumn(
                name: "LocalPath",
                table: "SyncFolders");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "DriveFiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ChangeLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentFolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeLogs_Devices_OriginDeviceId",
                        column: x => x.OriginDeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogs_OriginDeviceId",
                table: "ChangeLogs",
                column: "OriginDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogs_ResourceId",
                table: "ChangeLogs",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogs_UserId_Id",
                table: "ChangeLogs",
                columns: new[] { "UserId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeLogs");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "DriveFiles");

            migrationBuilder.AddColumn<string>(
                name: "LocalPath",
                table: "SyncFolders",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SyncFileStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriveFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    SyncFolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncedHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LocalHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LocalModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RelativePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncFileStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncFileStates_DriveFiles_DriveFileId",
                        column: x => x.DriveFileId,
                        principalTable: "DriveFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SyncFileStates_SyncFolders_SyncFolderId",
                        column: x => x.SyncFolderId,
                        principalTable: "SyncFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncFileStates_DriveFileId",
                table: "SyncFileStates",
                column: "DriveFileId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncFileStates_SyncFolderId",
                table: "SyncFileStates",
                column: "SyncFolderId");
        }
    }
}
