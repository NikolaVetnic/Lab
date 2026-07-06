using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OperationsCenter.Infrastructure.Persistence;

namespace OperationsCenter.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestWebApplicationFactory>
{
    public const string Name = "OperationsCenter integration collection";
}

public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string _databaseName;
    private string _testConnectionString;
    private readonly string _adminConnectionString;
    private readonly string _baseConnectionString;
    private readonly string _fallbackDatabaseName;
    private bool _useEphemeralDatabase = true;

    private const string FallbackDatabaseEnvVar = "OPERATIONS_CENTER_TEST_DB_NAME";
    private const string DefaultFallbackDatabaseName = "operations_center_integration";

    public IntegrationTestWebApplicationFactory()
    {
        _baseConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OperationsCenterDatabase")
            ?? "Host=localhost;Port=5432;Database=operations_center;Username=operations_center;Password=operations_center";

        _fallbackDatabaseName = Environment.GetEnvironmentVariable(FallbackDatabaseEnvVar)
            ?? DefaultFallbackDatabaseName;

        _databaseName = $"operations_center_it_{Guid.NewGuid():N}";
        _testConnectionString = BuildConnectionString(_databaseName);

        var adminBuilder = new NpgsqlConnectionStringBuilder(_baseConnectionString)
        {
            Database = "postgres"
        };

        _adminConnectionString = adminBuilder.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OperationsCenterDatabase"] = _testConnectionString
            });
        });
    }

    public async Task InitializeAsync()
    {
        if (!await TryCreateDatabaseAsync())
        {
            _useEphemeralDatabase = false;
            _databaseName = _fallbackDatabaseName;
            _testConnectionString = BuildConnectionString(_databaseName);

            Console.WriteLine(
                $"Integration tests: CREATE DATABASE permission unavailable. Falling back to shared test database '{_databaseName}'.");
        }

        using var client = CreateClient();

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OperationsCenterDbContext>();

        if (!_useEphemeralDatabase)
        {
            await ResetSharedDatabaseStateAsync(dbContext);
        }

        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        if (_useEphemeralDatabase)
        {
            await DropDatabaseAsync();
        }
    }

    private async Task<bool> TryCreateDatabaseAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();

            var commandText = $"CREATE DATABASE \"{_databaseName}\"";
            await using var command = new NpgsqlCommand(commandText, connection);
            await command.ExecuteNonQueryAsync();

            return true;
        }
        catch (PostgresException postgresException) when (postgresException.SqlState is "42501")
        {
            return false;
        }
    }

    private string BuildConnectionString(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(_baseConnectionString)
        {
            Database = databaseName
        };

        return builder.ConnectionString;
    }

    private async Task DropDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();

        await using (var terminate = new NpgsqlCommand(
                         """
                         SELECT pg_terminate_backend(pid)
                         FROM pg_stat_activity
                         WHERE datname = @databaseName
                           AND pid <> pg_backend_pid();
                         """,
                         connection))
        {
            terminate.Parameters.AddWithValue("databaseName", _databaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\"", connection);
        await drop.ExecuteNonQueryAsync();
    }

    private static async Task ResetSharedDatabaseStateAsync(OperationsCenterDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DO $$
            DECLARE table_record record;
            BEGIN
                FOR table_record IN
                    SELECT schemaname, tablename
                    FROM pg_tables
                    WHERE schemaname IN ('incidents', 'audit', 'identity')
                LOOP
                    EXECUTE format(
                        'TRUNCATE TABLE %I.%I RESTART IDENTITY CASCADE',
                        table_record.schemaname,
                        table_record.tablename);
                END LOOP;
            END $$;
            """);
    }
}
