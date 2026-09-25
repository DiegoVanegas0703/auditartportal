using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PacientesAndCeleste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PacienteId",
                table: "audit_services",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pacientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NombreNormalizado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dni = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DniNormalizado = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Art = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NumeroSiniestro = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pacientes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_services_PacienteId",
                table: "audit_services",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_pacientes_DniNormalizado",
                table: "pacientes",
                column: "DniNormalizado");

            migrationBuilder.CreateIndex(
                name: "IX_pacientes_DniNormalizado_NombreNormalizado",
                table: "pacientes",
                columns: new[] { "DniNormalizado", "NombreNormalizado" });

            migrationBuilder.AddForeignKey(
                name: "FK_audit_services_pacientes_PacienteId",
                table: "audit_services",
                column: "PacienteId",
                principalTable: "pacientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(
                """
                UPDATE audit_services SET "Status" = 'Celeste' WHERE "Status" = 'CuentaPagoAnticipado';
                UPDATE sla_rules SET "Status" = 'Celeste' WHERE "Status" = 'CuentaPagoAnticipado';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_services_pacientes_PacienteId",
                table: "audit_services");

            migrationBuilder.DropTable(
                name: "pacientes");

            migrationBuilder.DropIndex(
                name: "IX_audit_services_PacienteId",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "PacienteId",
                table: "audit_services");
        }
    }
}
