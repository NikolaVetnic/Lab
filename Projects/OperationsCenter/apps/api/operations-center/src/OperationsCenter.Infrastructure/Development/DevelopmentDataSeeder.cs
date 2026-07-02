using Microsoft.EntityFrameworkCore;
using OperationsCenter.Domain.Incidents;
using OperationsCenter.Infrastructure.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OperationsCenter.Infrastructure.Development;

public sealed class DevelopmentDataSeeder
{
    private const string SeedDataRelativePath = "SeedData/incidents.development.json";

    private static readonly JsonSerializerOptions SeedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly OperationsCenterDbContext _dbContext;
    private readonly string _seedDataFilePath;

    public DevelopmentDataSeeder(OperationsCenterDbContext dbContext)
        : this(dbContext, Path.Combine(AppContext.BaseDirectory, SeedDataRelativePath))
    {
    }

    public DevelopmentDataSeeder(OperationsCenterDbContext dbContext, string seedDataFilePath)
    {
        _dbContext = dbContext;
        _seedDataFilePath = seedDataFilePath;
    }

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var seedItems = await LoadSeedItemsAsync(cancellationToken);
        var seedTitles = seedItems.Select(item => item.Title).ToArray();

        var existingTitles = await _dbContext.Incidents
            .AsNoTracking()
            .Where(incident => seedTitles.Contains(incident.Title))
            .Select(incident => incident.Title)
            .ToListAsync(cancellationToken);

        var existingTitleSet = new HashSet<string>(existingTitles, StringComparer.Ordinal);
        var insertedCount = 0;

        foreach (var item in seedItems)
        {
            if (existingTitleSet.Contains(item.Title))
            {
                continue;
            }

            var incident = Incident.Create(item.Title, item.Description, item.Severity, item.CreatedAtUtc);
            ApplyStatus(incident, item.Status);

            await _dbContext.AddIncidentAsync(incident, cancellationToken);
            insertedCount++;
        }

        if (insertedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return insertedCount;
    }

    private async Task<IReadOnlyList<SeedIncident>> LoadSeedItemsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_seedDataFilePath))
        {
            throw new FileNotFoundException("Seed data file was not found.", _seedDataFilePath);
        }

        await using var stream = File.OpenRead(_seedDataFilePath);
        var seedItems = await JsonSerializer.DeserializeAsync<IReadOnlyList<SeedIncident>>(
            stream,
            SeedJsonOptions,
            cancellationToken);

        if (seedItems is null || seedItems.Count == 0)
        {
            throw new InvalidOperationException("Seed data file does not contain incidents.");
        }

        return seedItems;
    }

    private static void ApplyStatus(Incident incident, IncidentStatus targetStatus)
    {
        switch (targetStatus)
        {
            case IncidentStatus.Open:
                return;
            case IncidentStatus.InProgress:
                EnsureTransition(incident, IncidentStatus.InProgress);
                return;
            case IncidentStatus.Resolved:
                EnsureTransition(incident, IncidentStatus.Resolved);
                return;
            case IncidentStatus.Closed:
                EnsureTransition(incident, IncidentStatus.Resolved);
                EnsureTransition(incident, IncidentStatus.Closed);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetStatus), targetStatus, "Unsupported seed incident status.");
        }
    }

    private static void EnsureTransition(Incident incident, IncidentStatus targetStatus)
    {
        if (!incident.TryUpdateStatus(targetStatus))
        {
            throw new InvalidOperationException(
                $"Unable to transition incident '{incident.Title}' to '{targetStatus}'.");
        }
    }

    private sealed record SeedIncident(
        string Title,
        string Description,
        IncidentSeverity Severity,
        IncidentStatus Status,
        DateTimeOffset CreatedAtUtc);
}
