using Microsoft.EntityFrameworkCore;
using OperationsCenter.Application.Identity.Abstractions;
using OperationsCenter.Domain.Audit;
using OperationsCenter.Domain.Identity;
using OperationsCenter.Domain.Incidents;
using OperationsCenter.Infrastructure.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OperationsCenter.Infrastructure.Development;

public sealed class DevelopmentDataSeeder
{
    private const string StandardSeedDataRelativePath = "SeedData/incidents.development.json";
    private const string DemoSeedDataRelativePath = "SeedData/incidents.demo.json";

    private static readonly JsonSerializerOptions SeedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly OperationsCenterDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly string? _seedDataFilePathOverride;

    public DevelopmentDataSeeder(OperationsCenterDbContext dbContext, IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _seedDataFilePathOverride = null;
    }

    public DevelopmentDataSeeder(OperationsCenterDbContext dbContext, IPasswordHasher passwordHasher, string seedDataFilePath)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _seedDataFilePathOverride = seedDataFilePath;
    }

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        return await SeedAsync(DevelopmentSeedProfile.Standard, cancellationToken);
    }

    public async Task<int> SeedAsync(DevelopmentSeedProfile profile, CancellationToken cancellationToken = default)
    {
        await SeedIdentityAsync(profile, cancellationToken);

        var seedItems = await LoadSeedItemsAsync(profile, cancellationToken);
        var seedTitles = seedItems.Select(item => item.Title).ToArray();
        var seedSource = GetSeedSource(profile);
        var seedActor = GetSeedActor(profile);

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
            await _dbContext.AddIncidentAsync(incident, cancellationToken);
            await _dbContext.AddAuditEventAsync(
                AuditEvent.Create(
                    entityType: "Incident",
                    entityId: incident.Id,
                    action: "Created",
                    occurredAt: incident.CreatedAt,
                    actorId: seedActor,
                    metadataJson: JsonSerializer.Serialize(new
                    {
                        source = seedSource
                    })),
                cancellationToken);

            IReadOnlyList<StatusTransition> transitions = ApplyStatusAndCollectTransitions(incident, item);

            for (var index = 0; index < transitions.Count; index++)
            {
                StatusTransition transition = transitions[index];
                var metadata = JsonSerializer.Serialize(new
                {
                    oldStatus = transition.From.ToString(),
                    newStatus = transition.To.ToString(),
                    source = seedSource
                });

                await _dbContext.AddAuditEventAsync(
                    AuditEvent.Create(
                        entityType: "Incident",
                        entityId: incident.Id,
                        action: "StatusChanged",
                        occurredAt: incident.CreatedAt.AddMinutes(index + 1),
                        actorId: seedActor,
                        metadataJson: metadata),
                    cancellationToken);
            }

            insertedCount++;
        }

        if (insertedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return insertedCount;
    }

    private async Task SeedIdentityAsync(DevelopmentSeedProfile profile, CancellationToken cancellationToken)
    {
        var users = GetSeedUsers(profile);

        foreach (var user in users)
        {
            var existingUser = await _dbContext.GetUserByEmailAsync(user.Email, cancellationToken);
            if (existingUser is not null)
            {
                continue;
            }

            var passwordHash = _passwordHasher.Hash(user.Password);
            var newUser = User.Create(user.Email, passwordHash, user.Role, DateTimeOffset.UtcNow);
            await _dbContext.AddUserAsync(newUser, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SeedIncident>> LoadSeedItemsAsync(DevelopmentSeedProfile profile, CancellationToken cancellationToken)
    {
        var seedDataFilePath = ResolveSeedDataFilePath(profile);

        if (!File.Exists(seedDataFilePath))
        {
            throw new FileNotFoundException("Seed data file was not found.", seedDataFilePath);
        }

        await using var stream = File.OpenRead(seedDataFilePath);
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

    private string ResolveSeedDataFilePath(DevelopmentSeedProfile profile)
    {
        if (_seedDataFilePathOverride is not null)
        {
            return _seedDataFilePathOverride;
        }

        var relativePath = profile == DevelopmentSeedProfile.Demo
            ? DemoSeedDataRelativePath
            : StandardSeedDataRelativePath;

        return Path.Combine(AppContext.BaseDirectory, relativePath);
    }

    private static string GetSeedSource(DevelopmentSeedProfile profile)
    {
        return profile == DevelopmentSeedProfile.Demo
            ? "development-seed-demo"
            : "development-seed";
    }

    private static string GetSeedActor(DevelopmentSeedProfile profile)
    {
        return profile == DevelopmentSeedProfile.Demo
            ? "system:dev-seed:demo"
            : "system:dev-seed";
    }

    private static IReadOnlyList<SeedUser> GetSeedUsers(DevelopmentSeedProfile profile)
    {
        var suffix = profile == DevelopmentSeedProfile.Demo ? "-demo" : string.Empty;
        var adminPassword = Environment.GetEnvironmentVariable("DEV_SEED_ADMIN_PASSWORD") ?? "Admin123!";
        var operatorPassword = Environment.GetEnvironmentVariable("DEV_SEED_OPERATOR_PASSWORD") ?? "Operator123!";
        var viewerPassword = Environment.GetEnvironmentVariable("DEV_SEED_VIEWER_PASSWORD") ?? "Viewer123!";

        return
        [
            new SeedUser($"admin{suffix}@operations-center.local", adminPassword, SystemRole.Admin),
            new SeedUser($"operator{suffix}@operations-center.local", operatorPassword, SystemRole.Operator),
            new SeedUser($"viewer{suffix}@operations-center.local", viewerPassword, SystemRole.Viewer)
        ];
    }

    private static IReadOnlyList<StatusTransition> ApplyStatusAndCollectTransitions(Incident incident, SeedIncident item)
    {
        var transitions = new List<StatusTransition>();
        IncidentStatus[] targets = ResolveStatusTargets(item);

        foreach (IncidentStatus targetStatus in targets)
        {
            EnsureReachedStatus(incident, targetStatus, transitions);
        }

        return transitions;
    }

    private static IncidentStatus[] ResolveStatusTargets(SeedIncident item)
    {
        if (item.StatusTimeline is { Length: > 0 })
        {
            return item.StatusTimeline;
        }

        return [item.Status];
    }

    private static void EnsureReachedStatus(Incident incident, IncidentStatus targetStatus, ICollection<StatusTransition> transitions)
    {
        while (incident.Status != targetStatus)
        {
            IncidentStatus nextStatus = ResolveNextStep(incident.Status, targetStatus);
            EnsureTransition(incident, nextStatus, transitions);
        }
    }

    private static IncidentStatus ResolveNextStep(IncidentStatus currentStatus, IncidentStatus targetStatus)
    {
        if (currentStatus == IncidentStatus.Closed)
        {
            throw new InvalidOperationException("Closed incidents cannot be transitioned by seed data.");
        }

        switch (targetStatus)
        {
            case IncidentStatus.Open:
                throw new InvalidOperationException("Seed data does not support transitioning back to Open.");
            case IncidentStatus.InProgress:
                if (currentStatus == IncidentStatus.Open || currentStatus == IncidentStatus.Resolved)
                {
                    return IncidentStatus.InProgress;
                }

                break;
            case IncidentStatus.Resolved:
                if (currentStatus == IncidentStatus.Open)
                {
                    return IncidentStatus.InProgress;
                }

                if (currentStatus == IncidentStatus.InProgress)
                {
                    return IncidentStatus.Resolved;
                }

                break;
            case IncidentStatus.Closed:
                if (currentStatus == IncidentStatus.Open)
                {
                    return IncidentStatus.InProgress;
                }

                if (currentStatus == IncidentStatus.InProgress)
                {
                    return IncidentStatus.Resolved;
                }

                if (currentStatus == IncidentStatus.Resolved)
                {
                    return IncidentStatus.Closed;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetStatus), targetStatus, "Unsupported seed incident status.");
        }

        throw new InvalidOperationException(
            $"Unable to compute transition from '{currentStatus}' to '{targetStatus}'.");
    }

    private static void EnsureTransition(Incident incident, IncidentStatus targetStatus, ICollection<StatusTransition> transitions)
    {
        IncidentStatus previousStatus = incident.Status;

        if (!incident.TryUpdateStatus(targetStatus))
        {
            throw new InvalidOperationException(
                $"Unable to transition incident '{incident.Title}' to '{targetStatus}'.");
        }

        transitions.Add(new StatusTransition(previousStatus, incident.Status));
    }

    private sealed record StatusTransition(IncidentStatus From, IncidentStatus To);

    private sealed record SeedUser(string Email, string Password, SystemRole Role);

    private sealed record SeedIncident(
        string Title,
        string Description,
        IncidentSeverity Severity,
        IncidentStatus Status,
        IncidentStatus[]? StatusTimeline,
        DateTimeOffset CreatedAtUtc);
}
