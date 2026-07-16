using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incoming_emails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmailMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttachmentCount = table.Column<int>(type: "integer", nullable: false),
                    IsAssigned = table.Column<bool>(type: "boolean", nullable: false),
                    SuggestedQueue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SuggestedServiceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SuggestedArt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SuggestedPatientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultingServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incoming_emails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    GoogleSubjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefaultQueue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Paciente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dni = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Art = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TipoServicio = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Especialidad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Profesional = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Queue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Urgency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FechaIngresoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaTurnoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaConsultaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SlaDeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValorPactado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PresupuestoEnviado = table.Column<bool>(type: "boolean", nullable: false),
                    AutorizacionArt = table.Column<bool>(type: "boolean", nullable: false),
                    Autofisica = table.Column<bool>(type: "boolean", nullable: false),
                    Notas = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IncomingEmailId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_services_incoming_emails_IncomingEmailId",
                        column: x => x.IncomingEmailId,
                        principalTable: "incoming_emails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_audit_services_users_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncomingEmailId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    S3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    S3Bucket = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_attachments_audit_services_AuditServiceId",
                        column: x => x.AuditServiceId,
                        principalTable: "audit_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_attachments_incoming_emails_IncomingEmailId",
                        column: x => x.IncomingEmailId,
                        principalTable: "incoming_emails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "service_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_status_history_audit_services_AuditServiceId",
                        column: x => x.AuditServiceId,
                        principalTable: "audit_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_services_IncomingEmailId",
                table: "audit_services",
                column: "IncomingEmailId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_services_Numero",
                table: "audit_services",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_services_OperadorId",
                table: "audit_services",
                column: "OperadorId");

            migrationBuilder.CreateIndex(
                name: "IX_incoming_emails_GmailMessageId",
                table: "incoming_emails",
                column: "GmailMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_service_attachments_AuditServiceId",
                table: "service_attachments",
                column: "AuditServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_service_attachments_IncomingEmailId",
                table: "service_attachments",
                column: "IncomingEmailId");

            migrationBuilder.CreateIndex(
                name: "IX_service_status_history_AuditServiceId",
                table: "service_status_history",
                column: "AuditServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "service_attachments");

            migrationBuilder.DropTable(
                name: "service_status_history");

            migrationBuilder.DropTable(
                name: "audit_services");

            migrationBuilder.DropTable(
                name: "incoming_emails");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
