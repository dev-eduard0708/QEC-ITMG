using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Platform.NumberSequence;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.IntegrationTests.Platform;

public sealed class NumberSequenceConcurrencyTests
{
    private const string ConnectionStringKey = "ConnectionStrings__QecItmg";

    [Fact]
    public async Task NextAsync_SequentialNumbers_AndIndependentKeys()
    {
        if (!SqlServerTestGate.TryCreate(out SqlServerFixture? created))
        {
            return; // skip locally/CI without SQL Server
        }

        await using SqlServerFixture fixture = created!;
        {
            await fixture.Platform.Database.MigrateAsync();

            string incKey = $"inc-key-{Guid.NewGuid():N}";
            string chgKey = $"chg-key-{Guid.NewGuid():N}";

            string a1 = await IssueAsync(fixture.Provider, incKey, "INC");
            string a2 = await IssueAsync(fixture.Provider, incKey, "INC");
            string b1 = await IssueAsync(fixture.Provider, chgKey, "CHG");

            Assert.EndsWith("-000001", a1, StringComparison.Ordinal);
            Assert.EndsWith("-000002", a2, StringComparison.Ordinal);
            Assert.EndsWith("-000001", b1, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task NextAsync_ParallelIssuance_ProducesUniqueValues()
    {
        if (!SqlServerTestGate.TryCreate(out SqlServerFixture? created))
        {
            return; // skip locally/CI without SQL Server
        }

        await using SqlServerFixture fixture = created!;
        {
            await fixture.Platform.Database.MigrateAsync();

            string sequenceKey = $"parallel-key-{Guid.NewGuid():N}";
            const string prefix = "INC";

            int workers = 20;
            Task<string>[] tasks = new Task<string>[workers];
            for (int i = 0; i < workers; i++)
            {
                tasks[i] = IssueAsync(fixture.Provider, sequenceKey, prefix);
            }

            string[] issued = await Task.WhenAll(tasks);

            Assert.Equal(workers, issued.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public async Task NextAsync_FirstUseParallelIssuance_AllUnique_NoExceptions()
    {
        if (!SqlServerTestGate.TryCreate(out SqlServerFixture? created))
        {
            return; // skip locally/CI without SQL Server
        }

        await using SqlServerFixture fixture = created!;
        await fixture.Platform.Database.MigrateAsync();

        // Brand-new (SequenceKey, Year): every parallel caller hits first-use insert/update path.
        string sequenceKey = $"first-use-{Guid.NewGuid():N}";
        const string prefix = "INC";

        int workers = 20;
        Task<string>[] tasks = new Task<string>[workers];
        for (int i = 0; i < workers; i++)
        {
            tasks[i] = IssueAsync(fixture.Provider, sequenceKey, prefix);
        }

        string[] issued = await Task.WhenAll(tasks);

        Assert.Equal(workers, issued.Distinct(StringComparer.Ordinal).Count());
        Assert.All(issued, number => Assert.StartsWith($"{prefix}-2026-", number, StringComparison.Ordinal));
    }

    private static async Task<string> IssueAsync(IServiceProvider provider, string sequenceKey, string prefix)
    {
        using IServiceScope scope = provider.CreateScope();
        INumberSequenceService service = scope.ServiceProvider.GetRequiredService<INumberSequenceService>();
        return await service.NextAsync(sequenceKey, prefix);
    }

    private sealed class SqlServerFixture : IAsyncDisposable
    {
        public SqlServerFixture(
            ServiceProvider provider,
            PlatformDbContext platform)
        {
            Provider = provider;
            Platform = platform;
        }

        public PlatformDbContext Platform { get; }

        public ServiceProvider Provider { get; }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
        }
    }

    private static class SqlServerTestGate
    {
        public static bool TryCreate(out SqlServerFixture? fixture)
        {
            fixture = null;

            bool force = string.Equals(
                Environment.GetEnvironmentVariable("QEC_ITMG_SQL_INTEGRATION"),
                "1",
                StringComparison.OrdinalIgnoreCase);

            bool isCi =
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"))
                || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

            string connectionString =
                Environment.GetEnvironmentVariable(ConnectionStringKey)
                ?? "Server=.\\SQLEXPRESS;Database=QecItmg_Dev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

            if (isCi && !force)
            {
                return false;
            }

            try
            {
                using SqlConnection probe = new(connectionString);
                probe.Open();
            }
            catch
            {
                if (force)
                {
                    throw;
                }

                return false;
            }

            fixture = CreateFixture(connectionString);
            return true;
        }

        private static SqlServerFixture CreateFixture(string connectionString)
        {
            ServiceCollection services = new();
            Dictionary<string, string?> config = new()
            {
                [$"ConnectionStrings:QecItmg"] = connectionString,
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(config)
                .Build();

            services.AddSingleton(configuration);
            services.AddSingleton<IClock>(new FixedClock(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));

            services.AddQecSqlServerDbContext<PlatformDbContext>(connectionString, PlatformDbContext.SchemaName);
            services.AddScoped<INumberSequenceService, NumberSequenceService>();

            // Important: do not auto-create schema here; the test calls MigrateAsync explicitly.
            ServiceProvider provider = services.BuildServiceProvider();
            PlatformDbContext platform = provider.GetRequiredService<PlatformDbContext>();
            return new SqlServerFixture(provider, platform);
        }

        private sealed class FixedClock(DateTimeOffset utcNow) : IClock
        {
            public DateTimeOffset UtcNow { get; } = utcNow;
        }
    }
}

