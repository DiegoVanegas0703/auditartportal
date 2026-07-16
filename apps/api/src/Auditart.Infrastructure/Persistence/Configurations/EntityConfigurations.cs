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
        builder.Property(x => x.Especialidad).HasMaxLength(120);
        builder.Property(x => x.Profesional).HasMaxLength(200);
        builder.Property(x => x.Notas).HasMaxLength(4000);
        builder.Property(x => x.ValorPactado).HasPrecision(18, 2);
        builder.Property(x => x.TipoServicio).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Queue).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Urgency).HasConversion<string>().HasMaxLength(32);

        builder.HasOne(x => x.Operador)
            .WithMany(x => x.AssignedServices)
            .HasForeignKey(x => x.OperadorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.IncomingEmail)
            .WithMany()
            .HasForeignKey(x => x.IncomingEmailId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Reason).HasMaxLength(500);

        builder.HasOne(x => x.AuditService)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.AuditServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
