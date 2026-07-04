using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OperationsCenter.Domain.Audit;

namespace OperationsCenter.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("events", schema: "audit");

        builder.HasKey(auditEvent => auditEvent.Id);

        builder.Property(auditEvent => auditEvent.Id)
            .ValueGeneratedNever();

        builder.Property(auditEvent => auditEvent.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(auditEvent => auditEvent.EntityId)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(auditEvent => auditEvent.OccurredAt)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.ActorId)
            .HasMaxLength(200);

        builder.Property(auditEvent => auditEvent.MetadataJson)
            .HasColumnType("jsonb");

        builder.HasIndex(auditEvent => auditEvent.OccurredAt);
        builder.HasIndex(auditEvent => new { auditEvent.EntityType, auditEvent.EntityId });
    }
}
