using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreciosCatalogoAndValorConciliado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrecioCatalogoId",
                table: "audit_services",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoProfesional",
                table: "audit_services",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorConciliadoArt",
                table: "audit_services",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "precios_catalogo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoProfesional = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ArtNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Concepto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notas = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_precios_catalogo", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_services_PrecioCatalogoId",
                table: "audit_services",
                column: "PrecioCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_precios_catalogo_Concepto",
                table: "precios_catalogo",
                column: "Concepto");

            migrationBuilder.CreateIndex(
                name: "IX_precios_catalogo_TipoProfesional_ArtNombre_IsActive",
                table: "precios_catalogo",
                columns: new[] { "TipoProfesional", "ArtNombre", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_audit_services_precios_catalogo_PrecioCatalogoId",
                table: "audit_services",
                column: "PrecioCatalogoId",
                principalTable: "precios_catalogo",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_services_precios_catalogo_PrecioCatalogoId",
                table: "audit_services");

            migrationBuilder.DropTable(
                name: "precios_catalogo");

            migrationBuilder.DropIndex(
                name: "IX_audit_services_PrecioCatalogoId",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "PrecioCatalogoId",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "TipoProfesional",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "ValorConciliadoArt",
                table: "audit_services");
        }
    }
}
