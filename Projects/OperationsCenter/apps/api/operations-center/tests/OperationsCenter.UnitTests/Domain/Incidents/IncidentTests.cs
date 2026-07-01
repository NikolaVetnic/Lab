using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.UnitTests.Domain.Incidents;

public sealed class IncidentTests
{
    [Fact]
    public void Create_WhenInputIsValid_CreatesIncident()
    {
        var incident = Incident.Create("Database outage", "Primary database is unavailable.", IncidentSeverity.Critical);

        Assert.NotEqual(Guid.Empty, incident.Id);
        Assert.Equal("Database outage", incident.Title);
        Assert.Equal("Primary database is unavailable.", incident.Description);
        Assert.Equal(IncidentSeverity.Critical, incident.Severity);
    }

    [Fact]
    public void Create_WhenTitleIsMissing_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Incident.Create("", "Description", IncidentSeverity.Low));

        Assert.Equal("title", exception.ParamName);
    }

    [Fact]
    public void Create_WhenTitleExceedsMaxLength_ThrowsArgumentException()
    {
        var longTitle = new string('A', 201);

        var exception = Assert.Throws<ArgumentException>(() =>
            Incident.Create(longTitle, "Description", IncidentSeverity.Low));

        Assert.Equal("title", exception.ParamName);
    }

    [Fact]
    public void Create_WhenDescriptionExceedsMaxLength_ThrowsArgumentException()
    {
        var longDescription = new string('A', 4001);

        var exception = Assert.Throws<ArgumentException>(() =>
            Incident.Create("Title", longDescription, IncidentSeverity.Low));

        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void Create_WhenCalled_SetsInitialStatusToOpen()
    {
        var incident = Incident.Create("Title", null, IncidentSeverity.Medium);

        Assert.Equal(IncidentStatus.Open, incident.Status);
    }

    [Fact]
    public void Create_WhenCalled_StoresCreatedAtAsUtc()
    {
        var localOffset = new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.FromHours(2));

        var incident = Incident.Create("Title", null, IncidentSeverity.Medium, localOffset);

        Assert.Equal(TimeSpan.Zero, incident.CreatedAt.Offset);
    }
}
