using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePatientContactFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailPaciente",
                table: "audit_services",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroSiniestro",
                table: "audit_services",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelefonoPaciente",
                table: "audit_services",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailPaciente",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "NumeroSiniestro",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "TelefonoPaciente",
                table: "audit_services");
        }
    }
}
