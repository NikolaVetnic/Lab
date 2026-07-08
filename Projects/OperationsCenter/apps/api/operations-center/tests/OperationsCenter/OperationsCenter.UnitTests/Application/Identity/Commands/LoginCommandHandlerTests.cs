using OperationsCenter.Application.Identity.Abstractions;
using OperationsCenter.Application.Identity.Commands.Login;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Domain.Audit;
using OperationsCenter.Domain.Identity;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.UnitTests.Application.Identity.Commands;

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCredentialsAreValid_ReturnsTokenResponse()
    {
        var user = User.Create("admin@operations-center.local", "hash::Admin123!", SystemRole.Admin, DateTimeOffset.UtcNow);
        var dbContext = new FakeOperationsCenterDbContext(user);
        var handler = new LoginCommandHandler(dbContext, new FakePasswordHasher(), new FakeAccessTokenGenerator());

        var result = await handler.Handle(new LoginCommand(user.Email, "Admin123!"), CancellationToken.None);

        Assert.Equal(LoginOutcome.Success, result.Outcome);
        Assert.NotNull(result.Response);
        Assert.Equal("test-token", result.Response.AccessToken);
    }

    [Fact]
    public async Task Handle_WhenPasswordIsInvalid_ReturnsInvalidCredentials()
    {
        var user = User.Create("admin@operations-center.local", "hash::Admin123!", SystemRole.Admin, DateTimeOffset.UtcNow);
        var dbContext = new FakeOperationsCenterDbContext(user);
        var handler = new LoginCommandHandler(dbContext, new FakePasswordHasher(), new FakeAccessTokenGenerator());

        var result = await handler.Handle(new LoginCommand(user.Email, "WrongPassword!"), CancellationToken.None);

        Assert.Equal(LoginOutcome.InvalidCredentials, result.Outcome);
        Assert.Null(result.Response);
    }

    private sealed class FakeOperationsCenterDbContext(User? user) : IOperationsCenterDbContext
    {
        private readonly User? _user = user;

        public Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<Incident?> GetIncidentByIdAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Incident?>(null);
        }

        public Task<Incident?> GetIncidentByIdForUpdateAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Incident?>(null);
        }

        public Task<bool> IncidentExistsAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<Incident>> ListIncidentsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Incident>>([]);
        }

        public Task<IReadOnlyList<AuditEvent>> ListIncidentAuditEventsAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AuditEvent>>([]);
        }

        public Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(string? entityType, Guid? entityId, string? action, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AuditEvent>>([]);
        }

        public Task AddUserAsync(User user, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
        {
            if (_user is not null && _user.MatchesEmail(email))
            {
                return Task.FromResult<User?>(_user);
            }

            return Task.FromResult<User?>(null);
        }

        public Task<IReadOnlyDictionary<Guid, string>> GetUserEmailsByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
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

    private sealed class FakeAccessTokenGenerator : IAccessTokenGenerator
    {
        public AccessTokenResult Generate(User user)
        {
            return new AccessTokenResult("test-token", DateTimeOffset.UtcNow.AddMinutes(10));
        }
    }
}
