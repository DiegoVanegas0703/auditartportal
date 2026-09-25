using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailTagsAndIgnored : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "IgnoredAtUtc",
                table: "incoming_emails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IgnoredByUserId",
                table: "incoming_emails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsIgnored",
                table: "incoming_emails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<List<string>>(
                name: "Tags",
                table: "incoming_emails",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.CreateIndex(
                name: "IX_incoming_emails_IsAssigned_IsIgnored_ReceivedAtUtc",
                table: "incoming_emails",
                columns: new[] { "IsAssigned", "IsIgnored", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_incoming_emails_Tags",
                table: "incoming_emails",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_incoming_emails_IsAssigned_IsIgnored_ReceivedAtUtc",
                table: "incoming_emails");

            migrationBuilder.DropIndex(
                name: "IX_incoming_emails_Tags",
                table: "incoming_emails");

            migrationBuilder.DropColumn(
                name: "IgnoredAtUtc",
                table: "incoming_emails");

            migrationBuilder.DropColumn(
                name: "IgnoredByUserId",
                table: "incoming_emails");

            migrationBuilder.DropColumn(
                name: "IsIgnored",
                table: "incoming_emails");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "incoming_emails");
        }
    }
}
