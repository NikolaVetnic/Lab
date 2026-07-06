using Microsoft.EntityFrameworkCore;
using OperationsCenter.Application.Identity.Abstractions;
using OperationsCenter.Infrastructure.Development;
using OperationsCenter.Infrastructure.Persistence;

namespace OperationsCenter.UnitTests.Infrastructure.Development;

public sealed class DevelopmentDataSeederTests
{
    [Fact]
    public async Task SeedAsync_WhenRunTwice_DoesNotCreateDuplicates()
    {
        var options = new DbContextOptionsBuilder<OperationsCenterDbContext>()
            .UseInMemoryDatabase($"development-seed-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new OperationsCenterDbContext(options);
        var seedFilePath = Path.Combine(Path.GetTempPath(), $"incidents-seed-{Guid.NewGuid()}.json");

        try
        {
            await File.WriteAllTextAsync(seedFilePath, """
[
    {
        "title": "Incident A",
        "description": "Description A",
        "severity": "High",
        "status": "Open",
        "createdAtUtc": "2026-06-30T09:05:00+00:00"
    },
    {
        "title": "Incident B",
        "description": "Description B",
        "severity": "Medium",
        "status": "Resolved",
        "createdAtUtc": "2026-06-29T14:42:00+00:00"
    },
    {
        "title": "Incident C",
        "description": "Description C",
        "severity": "Low",
        "status": "Closed",
        "createdAtUtc": "2026-06-27T10:40:00+00:00"
    },
    {
        "title": "Incident D",
        "description": "Description D",
        "severity": "Critical",
        "status": "InProgress",
        "createdAtUtc": "2026-06-26T07:55:00+00:00"
    },
    {
        "title": "Incident E",
        "description": "Description E",
        "severity": "High",
        "status": "Open",
        "createdAtUtc": "2026-06-25T10:40:00+00:00"
    }
]
""");

            var seeder = new DevelopmentDataSeeder(dbContext, new FakePasswordHasher(), seedFilePath);

            var firstRunInsertedCount = await seeder.SeedAsync();
            var secondRunInsertedCount = await seeder.SeedAsync();
            var seededIncidentCount = await dbContext.Incidents.CountAsync();
            var statusChangedAuditCount = await dbContext.AuditEvents
                .CountAsync(auditEvent => auditEvent.EntityType == "Incident" && auditEvent.Action == "StatusChanged");

            Assert.InRange(firstRunInsertedCount, 5, 10);
            Assert.Equal(0, secondRunInsertedCount);
            Assert.Equal(firstRunInsertedCount, seededIncidentCount);
            Assert.True(statusChangedAuditCount > 0);
        }
        finally
        {
            if (File.Exists(seedFilePath))
            {
                File.Delete(seedFilePath);
            }
        }
    }

    [Fact]
    public async Task SeedAsync_WhenTimelineIsProvided_WritesAuditEventsForEachTransition()
    {
        var options = new DbContextOptionsBuilder<OperationsCenterDbContext>()
            .UseInMemoryDatabase($"development-seed-timeline-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new OperationsCenterDbContext(options);
        var seedFilePath = Path.Combine(Path.GetTempPath(), $"incidents-seed-timeline-{Guid.NewGuid()}.json");

        try
        {
            await File.WriteAllTextAsync(seedFilePath, """
[
    {
        "title": "Timeline Incident",
        "description": "Timeline Description",
        "severity": "High",
        "status": "Closed",
        "statusTimeline": ["InProgress", "Resolved", "InProgress", "Resolved", "Closed"],
        "createdAtUtc": "2026-06-30T09:05:00+00:00"
    }
]
""");

            var seeder = new DevelopmentDataSeeder(dbContext, new FakePasswordHasher(), seedFilePath);

            _ = await seeder.SeedAsync();

            var statusChangedAuditCount = await dbContext.AuditEvents
                .CountAsync(auditEvent => auditEvent.EntityType == "Incident" && auditEvent.Action == "StatusChanged");
            var createdAuditCount = await dbContext.AuditEvents
                .CountAsync(auditEvent => auditEvent.EntityType == "Incident" && auditEvent.Action == "Created");
            var incident = await dbContext.Incidents.SingleAsync();

            Assert.Equal(5, statusChangedAuditCount);
            Assert.Equal(1, createdAuditCount);
            Assert.Equal(OperationsCenter.Domain.Incidents.IncidentStatus.Closed, incident.Status);
        }
        finally
        {
            if (File.Exists(seedFilePath))
            {
                File.Delete(seedFilePath);
            }
        }
    }
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public bool Verify(string hashedPassword, string providedPassword)
    {
        return hashedPassword == $"hash::{providedPassword}";
    }

    public string Hash(string password)
    {
        return $"hash::{password}";
    }
}
