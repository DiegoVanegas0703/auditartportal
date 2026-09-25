using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaRulesAndInAppAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "in_app_alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ServiceStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Queue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_in_app_alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_in_app_alerts_audit_services_AuditServiceId",
                        column: x => x.AuditServiceId,
                        principalTable: "audit_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_in_app_alerts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sla_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Queue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DurationValue = table.Column<int>(type: "integer", nullable: false),
                    DurationUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WarnBeforeHours = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_in_app_alerts_AuditServiceId_UserId_Kind_ServiceStatus",
                table: "in_app_alerts",
                columns: new[] { "AuditServiceId", "UserId", "Kind", "ServiceStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_in_app_alerts_UserId_IsRead_ResolvedAtUtc",
                table: "in_app_alerts",
                columns: new[] { "UserId", "IsRead", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_rules_Queue_Status",
                table: "sla_rules",
                columns: new[] { "Queue", "Status" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "in_app_alerts");

            migrationBuilder.DropTable(
                name: "sla_rules");
        }
    }
}
