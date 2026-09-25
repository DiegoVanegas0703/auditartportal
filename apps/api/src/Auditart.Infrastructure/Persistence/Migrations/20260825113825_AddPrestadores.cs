using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrestadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrestadorId",
                table: "audit_services",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "prestadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provincia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Localidad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cuit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Drive = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Especialidad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Servicio = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Domicilio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CodigoPostal = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Telefonos = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Interno = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Horario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MailContacto = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    MailAdmision = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Convenios = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Operativo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Adhesion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Dni = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Matricula = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Afip = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Iibb = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Superintendencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Seguro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HabSalud = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    HabMunic = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Banco = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Sucursal = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TipoCuenta = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NumeroCuenta = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Cbu = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Alias = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UltimaActualizacionValores = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValoresAcordados = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FormaPago = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Observaciones = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExcelRowNumber = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prestadores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_services_PrestadorId",
                table: "audit_services",
                column: "PrestadorId");

            migrationBuilder.CreateIndex(
                name: "IX_prestadores_Cuit",
                table: "prestadores",
                column: "Cuit");

            migrationBuilder.CreateIndex(
                name: "IX_prestadores_Especialidad",
                table: "prestadores",
                column: "Especialidad");

            migrationBuilder.CreateIndex(
                name: "IX_prestadores_IsActive",
                table: "prestadores",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_prestadores_Nombre",
                table: "prestadores",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_prestadores_Provincia_Localidad",
                table: "prestadores",
                columns: new[] { "Provincia", "Localidad" });

            migrationBuilder.AddForeignKey(
                name: "FK_audit_services_prestadores_PrestadorId",
                table: "audit_services",
                column: "PrestadorId",
                principalTable: "prestadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_services_prestadores_PrestadorId",
                table: "audit_services");

            migrationBuilder.DropTable(
                name: "prestadores");

            migrationBuilder.DropIndex(
                name: "IX_audit_services_PrestadorId",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "PrestadorId",
                table: "audit_services");
        }
    }
}
