using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrestadorValorFirmaAndAutorizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirmaContentType",
                table: "prestadores",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirmaFileName",
                table: "prestadores",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirmaS3Key",
                table: "prestadores",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirmaUploadedAtUtc",
                table: "prestadores",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequierePagoAnticipado",
                table: "prestadores",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorConsulta",
                table: "prestadores",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutorizacionCodigo",
                table: "audit_services",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AutorizacionDocumentoAttachmentId",
                table: "audit_services",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequierePagoAnticipado",
                table: "audit_services",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirmaContentType",
                table: "prestadores");

            migrationBuilder.DropColumn(
                name: "FirmaFileName",
                table: "prestadores");

            migrationBuilder.DropColumn(
                name: "FirmaS3Key",
                table: "prestadores");

            migrationBuilder.DropColumn(
                name: "FirmaUploadedAtUtc",
                table: "prestadores");

            migrationBuilder.DropColumn(
                name: "RequierePagoAnticipado",
                table: "prestadores");

            migrationBuilder.DropColumn(
                name: "ValorConsulta",
                table: "prestadores");

            migrationBuilder.DropColumn(
                name: "AutorizacionCodigo",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "AutorizacionDocumentoAttachmentId",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "RequierePagoAnticipado",
                table: "audit_services");
        }
    }
}
