using Auditart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auditart.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.GoogleSubjectId).HasMaxLength(128);
        builder.Property(x => x.PasswordHash).HasMaxLength(256);
        builder.Property(x => x.MustChangePassword).HasDefaultValue(false);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.DefaultQueue).HasConversion<string>().HasMaxLength(32);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasOne(x => x.User)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditServiceConfiguration : IEntityTypeConfiguration<AuditService>
{
    public void Configure(EntityTypeBuilder<AuditService> builder)
    {
        builder.ToTable("audit_services");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Numero).IsUnique();
        builder.Property(x => x.Paciente).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Dni).HasMaxLength(32);
        builder.Property(x => x.Art).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NumeroSiniestro).HasMaxLength(64);
        builder.Property(x => x.TelefonoPaciente).HasMaxLength(32);
        builder.Property(x => x.EmailPaciente).HasMaxLength(256);
        builder.Property(x => x.Especialidad).HasMaxLength(120);
        builder.Property(x => x.Profesional).HasMaxLength(200);
        builder.Property(x => x.Notas).HasMaxLength(4000);
        builder.Property(x => x.ValorPactado).HasPrecision(18, 2);
        builder.Property(x => x.ValorConciliadoArt).HasPrecision(18, 2);
        builder.Property(x => x.AutorizacionCodigo).HasMaxLength(120);
        builder.Property(x => x.TipoServicio).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Queue).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Urgency).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ChronicPeriodicity).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.TipoProfesional).HasConversion<string>().HasMaxLength(32);

        builder.HasOne(x => x.PrecioCatalogo)
            .WithMany()
            .HasForeignKey(x => x.PrecioCatalogoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Operador)
            .WithMany(x => x.AssignedServices)
            .HasForeignKey(x => x.OperadorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.IncomingEmail)
            .WithMany()
            .HasForeignKey(x => x.IncomingEmailId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.EmailRequest)
            .WithMany()
            .HasForeignKey(x => x.EmailRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Prestador)
            .WithMany()
            .HasForeignKey(x => x.PrestadorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.PacienteEntity)
            .WithMany(x => x.Prestaciones)
            .HasForeignKey(x => x.PacienteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PrecioCatalogoConfiguration : IEntityTypeConfiguration<PrecioCatalogo>
{
    public void Configure(EntityTypeBuilder<PrecioCatalogo> builder)
    {
        builder.ToTable("precios_catalogo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TipoProfesional).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.ArtNombre).HasMaxLength(200);
        builder.Property(x => x.Concepto).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Valor).HasPrecision(18, 2);
        builder.Property(x => x.Notas).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TipoProfesional, x.ArtNombre, x.IsActive });
        builder.HasIndex(x => x.Concepto);
    }
}

public class PacienteConfiguration : IEntityTypeConfiguration<Paciente>
{
    public void Configure(EntityTypeBuilder<Paciente> builder)
    {
        builder.ToTable("pacientes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NombreNormalizado).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Dni).HasMaxLength(32);
        builder.Property(x => x.DniNormalizado).HasMaxLength(32);
        builder.Property(x => x.Telefono).HasMaxLength(32);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Art).HasMaxLength(200);
        builder.Property(x => x.NumeroSiniestro).HasMaxLength(64);
        builder.HasIndex(x => new { x.DniNormalizado, x.NombreNormalizado });
        builder.HasIndex(x => x.DniNormalizado);
    }
}

public class PrestadorConfiguration : IEntityTypeConfiguration<Prestador>
{
    public void Configure(EntityTypeBuilder<Prestador> builder)
    {
        builder.ToTable("prestadores");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provincia).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Localidad).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Cuit).HasMaxLength(32);
        builder.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Drive).HasMaxLength(500);
        builder.Property(x => x.Especialidad).HasMaxLength(200);
        builder.Property(x => x.Servicio).HasMaxLength(300);
        builder.Property(x => x.Domicilio).HasMaxLength(500);
        builder.Property(x => x.CodigoPostal).HasMaxLength(32);
        builder.Property(x => x.Telefonos).HasMaxLength(1000);
        builder.Property(x => x.Interno).HasMaxLength(64);
        builder.Property(x => x.Horario).HasMaxLength(200);
        builder.Property(x => x.MailContacto).HasMaxLength(320);
        builder.Property(x => x.MailAdmision).HasMaxLength(320);
        builder.Property(x => x.Convenios).HasMaxLength(500);
        builder.Property(x => x.Operativo).HasMaxLength(200);
        builder.Property(x => x.Adhesion).HasMaxLength(200);
        builder.Property(x => x.Dni).HasMaxLength(32);
        builder.Property(x => x.Matricula).HasMaxLength(64);
        builder.Property(x => x.Afip).HasMaxLength(120);
        builder.Property(x => x.Iibb).HasMaxLength(120);
        builder.Property(x => x.Superintendencia).HasMaxLength(200);
        builder.Property(x => x.Seguro).HasMaxLength(200);
        builder.Property(x => x.HabSalud).HasMaxLength(120);
        builder.Property(x => x.HabMunic).HasMaxLength(120);
        builder.Property(x => x.Banco).HasMaxLength(120);
        builder.Property(x => x.Sucursal).HasMaxLength(120);
        builder.Property(x => x.TipoCuenta).HasMaxLength(64);
        builder.Property(x => x.NumeroCuenta).HasMaxLength(64);
        builder.Property(x => x.Cbu).HasMaxLength(64);
        builder.Property(x => x.Alias).HasMaxLength(120);
        builder.Property(x => x.UltimaActualizacionValores).HasMaxLength(200);
        builder.Property(x => x.ValoresAcordados).HasMaxLength(2000);
        builder.Property(x => x.FormaPago).HasMaxLength(200);
        builder.Property(x => x.Observaciones).HasMaxLength(4000);
        builder.Property(x => x.ValorConsulta).HasPrecision(18, 2);
        builder.Property(x => x.FirmaS3Key).HasMaxLength(512);
        builder.Property(x => x.FirmaFileName).HasMaxLength(260);
        builder.Property(x => x.FirmaContentType).HasMaxLength(120);

        builder.HasIndex(x => x.Nombre);
        builder.HasIndex(x => x.Cuit);
        builder.HasIndex(x => new { x.Provincia, x.Localidad });
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.Especialidad);
    }
}

public class IncomingEmailConfiguration : IEntityTypeConfiguration<IncomingEmail>
{
    public void Configure(EntityTypeBuilder<IncomingEmail> builder)
    {
        builder.ToTable("incoming_emails");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.GmailMessageId).IsUnique();
        builder.Property(x => x.GmailMessageId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.FromAddress).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.SuggestedArt).HasMaxLength(200);
        builder.Property(x => x.SuggestedPatientName).HasMaxLength(200);
        builder.Property(x => x.SuggestedQueue).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.SuggestedServiceType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Tags)
            .HasColumnType("text[]")
            .HasDefaultValueSql("ARRAY[]::text[]");
        builder.HasIndex(x => new { x.IsAssigned, x.IsIgnored, x.ReceivedAtUtc });
        builder.HasIndex(x => x.Tags).HasMethod("gin");
    }
}

public class ServiceAttachmentConfiguration : IEntityTypeConfiguration<ServiceAttachment>
{
    public void Configure(EntityTypeBuilder<ServiceAttachment> builder)
    {
        builder.ToTable("service_attachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.S3Key).HasMaxLength(512).IsRequired();
        builder.Property(x => x.S3Bucket).HasMaxLength(128);

        builder.HasOne(x => x.AuditService)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.AuditServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.IncomingEmail)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.IncomingEmailId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ServiceStatusHistoryConfiguration : IEntityTypeConfiguration<ServiceStatusHistory>
{
    public void Configure(EntityTypeBuilder<ServiceStatusHistory> builder)
    {
        builder.ToTable("service_status_history");
        builder.HasKey(x => x.Id);
        // Ids se generan en dominio (Guid.NewGuid). Sin esto, EF Core 3+ trata el hijo
        // agregado a una colección de un padre tracked como Modified → UPDATE 0 filas.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Reason).HasMaxLength(500);

        builder.HasOne(x => x.AuditService)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.AuditServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SlaRuleConfiguration : IEntityTypeConfiguration<SlaRule>
{
    public void Configure(EntityTypeBuilder<SlaRule> builder)
    {
        builder.ToTable("sla_rules");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.Queue, x.Status }).IsUnique();
        builder.Property(x => x.Queue).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.DurationUnit).HasConversion<string>().HasMaxLength(16);
    }
}

public class InAppAlertConfiguration : IEntityTypeConfiguration<InAppAlert>
{
    public void Configure(EntityTypeBuilder<InAppAlert> builder)
    {
        builder.ToTable("in_app_alerts");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.IsRead, x.ResolvedAtUtc });
        builder.HasIndex(x => new { x.AuditServiceId, x.UserId, x.Kind, x.ServiceStatus });
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ServiceStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Queue).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Message).HasMaxLength(500).IsRequired();

        builder.HasOne(x => x.AuditService)
            .WithMany()
            .HasForeignKey(x => x.AuditServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmailRequestConfiguration : IEntityTypeConfiguration<EmailRequest>
{
    public void Configure(EntityTypeBuilder<EmailRequest> builder)
    {
        builder.ToTable("email_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Tags)
            .HasColumnType("text[]")
            .HasDefaultValueSql("ARRAY[]::text[]");
        builder.Property(x => x.Participants)
            .HasColumnType("text[]")
            .HasDefaultValueSql("ARRAY[]::text[]");
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.State, x.LastMessageAtUtc });
        builder.HasIndex(x => x.Tags).HasMethod("gin");
        builder.HasIndex(x => x.AuditServiceId);

        builder.HasOne(x => x.AuditService)
            .WithMany()
            .HasForeignKey(x => x.AuditServiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class EmailThreadConfiguration : IEntityTypeConfiguration<EmailThread>
{
    public void Configure(EntityTypeBuilder<EmailThread> builder)
    {
        builder.ToTable("email_threads");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderThreadId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Mailbox).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.Mailbox, x.ProviderThreadId }).IsUnique();
        builder.HasIndex(x => x.LastMessageAtUtc);

        builder.HasOne(x => x.EmailRequest)
            .WithMany(x => x.Threads)
            .HasForeignKey(x => x.EmailRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.ToTable("email_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderMessageId).HasMaxLength(128);
        builder.Property(x => x.InternetMessageId).HasMaxLength(500);
        builder.Property(x => x.FromAddress).HasMaxLength(320).IsRequired();
        builder.Property(x => x.ReplyTo).HasMaxLength(320);
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.BodyText).IsRequired();
        builder.Property(x => x.InReplyTo).HasMaxLength(500);
        builder.Property(x => x.ReferencesHeader).HasMaxLength(4000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ToAddresses)
            .HasColumnType("text[]")
            .HasDefaultValueSql("ARRAY[]::text[]");
        builder.Property(x => x.CcAddresses)
            .HasColumnType("text[]")
            .HasDefaultValueSql("ARRAY[]::text[]");

        builder.HasIndex(x => x.ProviderMessageId).IsUnique();
        builder.HasIndex(x => new { x.EmailThreadId, x.OccurredAtUtc });
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.IncomingEmailId);

        builder.HasOne(x => x.EmailThread)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.EmailThreadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmailAttachmentConfiguration : IEntityTypeConfiguration<EmailAttachment>
{
    public void Configure(EntityTypeBuilder<EmailAttachment> builder)
    {
        builder.ToTable("email_attachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderAttachmentId).HasMaxLength(1024);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.S3Key).HasMaxLength(512).IsRequired();
        builder.Property(x => x.S3Bucket).HasMaxLength(128);
        builder.Property(x => x.Sha256).HasMaxLength(128);

        builder.HasOne(x => x.EmailMessage)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.EmailMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmailSendOutboxConfiguration : IEntityTypeConfiguration<EmailSendOutbox>
{
    public void Configure(EntityTypeBuilder<EmailSendOutbox> builder)
    {
        builder.ToTable("email_send_outbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.HasIndex(x => new { x.State, x.AvailableAtUtc });
        builder.HasIndex(x => x.EmailMessageId).IsUnique();

        builder.HasOne(x => x.EmailMessage)
            .WithOne(x => x.OutboxItem)
            .HasForeignKey<EmailSendOutbox>(x => x.EmailMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GmailSyncStateConfiguration : IEntityTypeConfiguration<GmailSyncState>
{
    public void Configure(EntityTypeBuilder<GmailSyncState> builder)
    {
        builder.ToTable("gmail_sync_states");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Mailbox).HasMaxLength(320).IsRequired();
        builder.Property(x => x.LastHistoryId).HasMaxLength(64);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.HasIndex(x => x.Mailbox).IsUnique();
    }
}
