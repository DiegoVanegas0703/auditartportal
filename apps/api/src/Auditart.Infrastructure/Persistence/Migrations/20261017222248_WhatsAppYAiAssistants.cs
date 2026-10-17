using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppYAiAssistants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_assistants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SystemPrompt = table.Column<string>(type: "text", nullable: true),
                    ExternalGemUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_assistants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_outbound_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhoneE164 = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BodyPreview = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_outbound_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_assistants_Name",
                table: "ai_assistants",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbound_logs_AuditServiceId",
                table: "whatsapp_outbound_logs",
                column: "AuditServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbound_logs_SentAtUtc",
                table: "whatsapp_outbound_logs",
                column: "SentAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_assistants");

            migrationBuilder.DropTable(
                name: "whatsapp_outbound_logs");
        }
    }
}
