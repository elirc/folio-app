using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Folio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockNesting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Blocks_PageId_Position",
                table: "Blocks");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentBlockId",
                table: "Blocks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_PageId_ParentBlockId_Position",
                table: "Blocks",
                columns: new[] { "PageId", "ParentBlockId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_ParentBlockId",
                table: "Blocks",
                column: "ParentBlockId");

            migrationBuilder.AddForeignKey(
                name: "FK_Blocks_Blocks_ParentBlockId",
                table: "Blocks",
                column: "ParentBlockId",
                principalTable: "Blocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Blocks_Blocks_ParentBlockId",
                table: "Blocks");

            migrationBuilder.DropIndex(
                name: "IX_Blocks_PageId_ParentBlockId_Position",
                table: "Blocks");

            migrationBuilder.DropIndex(
                name: "IX_Blocks_ParentBlockId",
                table: "Blocks");

            migrationBuilder.DropColumn(
                name: "ParentBlockId",
                table: "Blocks");

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_PageId_Position",
                table: "Blocks",
                columns: new[] { "PageId", "Position" });
        }
    }
}
