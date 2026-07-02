using Microsoft.EntityFrameworkCore;
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

            var seeder = new DevelopmentDataSeeder(dbContext, seedFilePath);

            var firstRunInsertedCount = await seeder.SeedAsync();
            var secondRunInsertedCount = await seeder.SeedAsync();
            var seededIncidentCount = await dbContext.Incidents.CountAsync();

            Assert.InRange(firstRunInsertedCount, 5, 10);
            Assert.Equal(0, secondRunInsertedCount);
            Assert.Equal(firstRunInsertedCount, seededIncidentCount);
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
