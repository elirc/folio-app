using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Folio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPageLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourcePageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceBlockId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetPageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetTitle = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageLinks_Blocks_SourceBlockId",
                        column: x => x.SourceBlockId,
                        principalTable: "Blocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageLinks_SourceBlockId",
                table: "PageLinks",
                column: "SourceBlockId");

            migrationBuilder.CreateIndex(
                name: "IX_PageLinks_SourcePageId",
                table: "PageLinks",
                column: "SourcePageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageLinks_TargetPageId",
                table: "PageLinks",
                column: "TargetPageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageLinks");
        }
    }
}
