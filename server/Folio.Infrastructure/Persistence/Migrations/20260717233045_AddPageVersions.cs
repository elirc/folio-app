using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Folio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPageVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    BlocksJson = table.Column<string>(type: "TEXT", nullable: false),
                    BlockCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedByName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageVersions_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageVersions_PageId_VersionNumber",
                table: "PageVersions",
                columns: new[] { "PageId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageVersions");
        }
    }
}
