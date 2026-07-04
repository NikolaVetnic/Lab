using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Infrastructure.Persistence.Configurations;

public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents", schema: "incidents");

        builder.HasKey(incident => incident.Id);

        builder.Property(incident => incident.Id)
            .ValueGeneratedNever();

        builder.Property(incident => incident.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(incident => incident.Description)
            .HasMaxLength(4000);

        builder.Property(incident => incident.Severity)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(incident => incident.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(incident => incident.CreatedAt)
            .IsRequired();

        builder.HasIndex(incident => incident.CreatedAt);
    }
}
