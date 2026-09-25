using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auditart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmailRequestId",
                table: "audit_services",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "email_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    AttachmentCount = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "ARRAY[]::text[]"),
                    Participants = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "ARRAY[]::text[]"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    AuditServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IgnoredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IgnoredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_requests_audit_services_AuditServiceId",
                        column: x => x.AuditServiceId,
                        principalTable: "audit_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "gmail_sync_states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mailbox = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    LastHistoryId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastSuccessfulSyncAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_sync_states", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "email_threads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderThreadId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Mailbox = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_threads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_threads_email_requests_EmailRequestId",
                        column: x => x.EmailRequestId,
                        principalTable: "email_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    InternetMessageId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ToAddresses = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "ARRAY[]::text[]"),
                    CcAddresses = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "ARRAY[]::text[]"),
                    ReplyTo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: true),
                    InReplyTo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferencesHeader = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    IncomingEmailId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_messages_email_threads_EmailThreadId",
                        column: x => x.EmailThreadId,
                        principalTable: "email_threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderAttachmentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    S3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    S3Bucket = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ServiceAttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_attachments_email_messages_EmailMessageId",
                        column: x => x.EmailMessageId,
                        principalTable: "email_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_send_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AvailableAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_send_outbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_send_outbox_email_messages_EmailMessageId",
                        column: x => x.EmailMessageId,
                        principalTable: "email_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_services_EmailRequestId",
                table: "audit_services",
                column: "EmailRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_email_attachments_EmailMessageId",
                table: "email_attachments",
                column: "EmailMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_EmailThreadId_OccurredAtUtc",
                table: "email_messages",
                columns: new[] { "EmailThreadId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_IdempotencyKey",
                table: "email_messages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_IncomingEmailId",
                table: "email_messages",
                column: "IncomingEmailId");

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_ProviderMessageId",
                table: "email_messages",
                column: "ProviderMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_requests_AuditServiceId",
                table: "email_requests",
                column: "AuditServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_email_requests_State_LastMessageAtUtc",
                table: "email_requests",
                columns: new[] { "State", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_email_requests_Tags",
                table: "email_requests",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_email_send_outbox_EmailMessageId",
                table: "email_send_outbox",
                column: "EmailMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_send_outbox_State_AvailableAtUtc",
                table: "email_send_outbox",
                columns: new[] { "State", "AvailableAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_email_threads_EmailRequestId",
                table: "email_threads",
                column: "EmailRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_email_threads_LastMessageAtUtc",
                table: "email_threads",
                column: "LastMessageAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_email_threads_Mailbox_ProviderThreadId",
                table: "email_threads",
                columns: new[] { "Mailbox", "ProviderThreadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gmail_sync_states_Mailbox",
                table: "gmail_sync_states",
                column: "Mailbox",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_services_email_requests_EmailRequestId",
                table: "audit_services",
                column: "EmailRequestId",
                principalTable: "email_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_services_email_requests_EmailRequestId",
                table: "audit_services");

            migrationBuilder.DropTable(
                name: "email_attachments");

            migrationBuilder.DropTable(
                name: "email_send_outbox");

            migrationBuilder.DropTable(
                name: "gmail_sync_states");

            migrationBuilder.DropTable(
                name: "email_messages");

            migrationBuilder.DropTable(
                name: "email_threads");

            migrationBuilder.DropTable(
                name: "email_requests");

            migrationBuilder.DropIndex(
                name: "IX_audit_services_EmailRequestId",
                table: "audit_services");

            migrationBuilder.DropColumn(
                name: "EmailRequestId",
                table: "audit_services");
        }
    }
}
