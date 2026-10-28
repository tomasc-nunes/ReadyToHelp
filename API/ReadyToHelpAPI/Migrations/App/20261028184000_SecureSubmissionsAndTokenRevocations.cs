using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyToHelpAPI.Migrations.App
{
    /// <inheritdoc />
    public partial class SecureSubmissionsAndTokenRevocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OccurrenceId",
                table: "reports",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "revoked_tokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revoked_tokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reports_OccurrenceId_UserId",
                table: "reports",
                columns: new[] { "OccurrenceId", "UserId" },
                unique: true);

            // Only anchor reports have a reliable historical association. Other historical
            // reports remain intact and unlinked; never invent corroborating identities.
            migrationBuilder.Sql("""
                UPDATE reports AS r SET "OccurrenceId" = anchors."Id"
                FROM (SELECT MIN("Id") AS "Id", "ReportId" FROM occurrences
                      WHERE "ReportId" IS NOT NULL GROUP BY "ReportId") AS anchors
                WHERE r."Id" = anchors."ReportId";

                UPDATE occurrences AS o SET "ReportCount" = (
                    SELECT COUNT(DISTINCT r."UserId") FROM reports AS r
                    WHERE r."OccurrenceId" = o."Id"
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_revoked_tokens_ExpiresAt",
                table: "revoked_tokens",
                column: "ExpiresAt");

            migrationBuilder.AddForeignKey(
                name: "FK_reports_occurrences_OccurrenceId",
                table: "reports",
                column: "OccurrenceId",
                principalTable: "occurrences",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reports_occurrences_OccurrenceId",
                table: "reports");

            migrationBuilder.DropTable(
                name: "revoked_tokens");

            migrationBuilder.DropIndex(
                name: "IX_reports_OccurrenceId_UserId",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "OccurrenceId",
                table: "reports");
        }
    }
}
