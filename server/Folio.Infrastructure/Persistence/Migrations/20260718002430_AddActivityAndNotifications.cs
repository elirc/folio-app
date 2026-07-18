using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Folio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByMemberId",
                table: "Pages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PageTitle = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    CommentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PageTitle = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_WorkspaceId_CreatedAt",
                table: "Activities",
                columns: new[] { "WorkspaceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ActivityId",
                table: "Notifications",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientMemberId_IsRead",
                table: "Notifications",
                columns: new[] { "RecipientMemberId", "IsRead" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropColumn(
                name: "CreatedByMemberId",
                table: "Pages");
        }
    }
}
