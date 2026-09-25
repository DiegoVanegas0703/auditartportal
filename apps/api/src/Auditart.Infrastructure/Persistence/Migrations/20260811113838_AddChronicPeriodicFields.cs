using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChronicPeriodicFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChronicIntervalDays",
                table: "audit_services",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChronicPeriodicity",
                table: "audit_services",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChronicRenewalCount",
                table: "audit_services",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChronicScheduleStartUtc",
                table: "audit_services",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsChronicPeriodic",
                table: "audit_services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRenewedAtUtc",
                table: "audit_services",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsOperadorAssignment",
                table: "audit_services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRenewalDueUtc",
                table: "audit_services",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChronicIntervalDays",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "ChronicPeriodicity",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "ChronicRenewalCount",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "ChronicScheduleStartUtc",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "IsChronicPeriodic",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "LastRenewedAtUtc",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "NeedsOperadorAssignment",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "NextRenewalDueUtc",
                table: "audit_services");
        }
    }
}
