using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Host.Persistence;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Persistence;
using Qec.Itmg.Platform.Audit;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.IntegrationTests.Persistence;

/// <summary>
/// Relational proof that Identity + Platform share one SQL connection/transaction.
/// Skips automatically on CI / when SQL Server is unreachable unless QEC_ITMG_SQL_INTEGRATION=1.
/// </summary>
public sealed class SharedSqlTransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Commit_PersistsIdentityMutationAndBusinessAudit()
    {
        if (!SqlServerTestGate.TryCreate(out SqlServerFixture? created))
        {
            return;
        }

        await using SqlServerFixture fixture = created!;

        Guid userId = Guid.CreateVersion7();
        string upn = $"tx-commit-{userId:N}@qehc.edu.sa";

        await fixture.SharedTx.ExecuteAsync(async ct =>
        {
            User user = User.Create(upn, "Commit User", UserType.Employee, Now, directoryObjectId: $"oid-{userId:N}");
            fixture.Identity.Users.Add(user);
            userId = user.Id;

            await fixture.BusinessAudit.AppendAsync(
                new BusinessAuditEntry
                {
                    AggregateType = AuditAggregateType.User,
                    AggregateId = user.Id,
                    BusinessNumber = user.Upn,
                    Action = BusinessAuditAction.Created,
                    NewValue = user.DisplayName,
                    Source = AuditSource.Api,
                },
                ct);
        });

        Assert.True(
            await fixture.Identity.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.Upn == upn));
        Assert.True(
            await fixture.Platform.BusinessAuditRecords.AsNoTracking()
                .AnyAsync(record => record.AggregateId == userId && record.Action == BusinessAuditAction.Created));

        Assert.True(fixture.SharedConnectionUsed);
        Assert.True(ReferenceEquals(
            fixture.Identity.Database.GetDbConnection(),
            fixture.Platform.Database.GetDbConnection()));
    }

    [Fact]
    public async Task AuditSaveFailure_RollsBackIdentityAndAudit()
    {
        if (!SqlServerTestGate.TryCreate(out SqlServerFixture? created, failPlatformSave: true))
        {
            return;
        }

        await using SqlServerFixture fixture = created!;

        Guid userId = Guid.Empty;
        string upn = $"tx-rollback-{Guid.NewGuid():N}@qehc.edu.sa";

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fixture.SharedTx.ExecuteAsync(async ct =>
            {
                User user = User.Create(upn, "Rollback User", UserType.Employee, Now);
                fixture.Identity.Users.Add(user);
                userId = user.Id;

                await fixture.BusinessAudit.AppendAsync(
                    new BusinessAuditEntry
                    {
                        AggregateType = AuditAggregateType.User,
                        AggregateId = user.Id,
                        BusinessNumber = user.Upn,
                        Action = BusinessAuditAction.Created,
                        NewValue = user.DisplayName,
                        Source = AuditSource.Api,
                    },
                    ct);
            }));

        Assert.Contains("forced audit", exception.Message, StringComparison.OrdinalIgnoreCase);

        fixture.Identity.ChangeTracker.Clear();
        fixture.Platform.ChangeTracker.Clear();

        Assert.False(await fixture.Identity.Users.AsNoTracking().AnyAsync(user => user.Upn == upn));
        Assert.False(
            await fixture.Platform.BusinessAuditRecords.AsNoTracking()
                .AnyAsync(record => record.AggregateId == userId));
    }
}

internal static class SqlServerTestGate
{
    public static bool TryCreate(out SqlServerFixture? fixture, bool failPlatformSave = false)
    {
        fixture = null;

        bool force = string.Equals(
            Environment.GetEnvironmentVariable("QEC_ITMG_SQL_INTEGRATION"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        bool isCi = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"))
            || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__QecItmg")
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

        fixture = SqlServerFixture.Create(connectionString, failPlatformSave);
        return true;
    }
}

internal sealed class SqlServerFixture : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    private SqlServerFixture(
        ServiceProvider services,
        IdentityDbContext identity,
        OrganizationDbContext organization,
        PlatformDbContext platform,
        IBusinessAuditWriter businessAudit,
        ISharedDbTransaction sharedTx,
        ISharedDbConnectionAccessor sharedConnection)
    {
        _services = services;
        Identity = identity;
        Organization = organization;
        Platform = platform;
        BusinessAudit = businessAudit;
        SharedTx = sharedTx;
        SharedConnection = sharedConnection;
    }

    public IdentityDbContext Identity { get; }
    public OrganizationDbContext Organization { get; }
    public PlatformDbContext Platform { get; }
    public IBusinessAuditWriter BusinessAudit { get; }
    public ISharedDbTransaction SharedTx { get; }
    public ISharedDbConnectionAccessor SharedConnection { get; }

    public bool SharedConnectionUsed =>
        ReferenceEquals(Identity.Database.GetDbConnection(), SharedConnection.Connection)
        && ReferenceEquals(Platform.Database.GetDbConnection(), SharedConnection.Connection)
        && ReferenceEquals(Organization.Database.GetDbConnection(), SharedConnection.Connection);

    public static SqlServerFixture Create(string connectionString, bool failPlatformSave)
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{QecEfConventions.ConnectionStringName}"] = connectionString,
            })
            .Build();

        services.AddSingleton(configuration);
        services.AddSingleton<IClock>(new FixedClock(Now));
        services.AddScoped<ISharedDbConnectionAccessor, SharedAppSqlConnection>();
        services.AddScoped<IAuditRequestContext, StaticAuditRequestContext>();
        services.AddQecSqlServerDbContext<IdentityDbContext>(connectionString, IdentityDbContext.SchemaName);
        services.AddQecSqlServerDbContext<OrganizationDbContext>(connectionString, OrganizationDbContext.SchemaName);

        if (failPlatformSave)
        {
            services.AddDbContext<PlatformDbContext>((sp, options) =>
            {
                ISharedDbConnectionAccessor shared = sp.GetRequiredService<ISharedDbConnectionAccessor>();
                options.UseSqlServer(
                    shared.Connection,
                    sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", PlatformDbContext.SchemaName));
                options.AddInterceptors(new FailPlatformAuditSaveInterceptor());
            });
        }
        else
        {
            services.AddQecSqlServerDbContext<PlatformDbContext>(connectionString, PlatformDbContext.SchemaName);
        }

        services.AddScoped<IBusinessAuditWriter, EfBusinessAuditWriter>();
        services.AddScoped<ISecurityAuditLogger, EfSecurityAuditLogger>();
        services.AddScoped<ISharedDbTransaction, SharedSqlTransaction>();

        ServiceProvider provider = services.BuildServiceProvider();
        IServiceScope scope = provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        return new SqlServerFixture(
            provider,
            sp.GetRequiredService<IdentityDbContext>(),
            sp.GetRequiredService<OrganizationDbContext>(),
            sp.GetRequiredService<PlatformDbContext>(),
            sp.GetRequiredService<IBusinessAuditWriter>(),
            sp.GetRequiredService<ISharedDbTransaction>(),
            sp.GetRequiredService<ISharedDbConnectionAccessor>())
        {
            Scope = scope,
        };
    }

    private IServiceScope Scope { get; init; } = null!;

    private static readonly DateTimeOffset Now = new(2026, 9, 3, 17, 0, 0, TimeSpan.Zero);

    public async ValueTask DisposeAsync()
    {
        Scope.Dispose();
        await _services.DisposeAsync();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class StaticAuditRequestContext : IAuditRequestContext
    {
        public AuditActorType ActorType => AuditActorType.System;

        public string? JobName => "sql-integration-test";

        public string? CorrelationId => "sql-tx-proof";

        public string? ClientIp => "127.0.0.1";

        public Task<Guid?> GetActorUserIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);
    }

    private sealed class FailPlatformAuditSaveInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            if (eventData.Context is PlatformDbContext platform
                && platform.ChangeTracker.Entries<BusinessAuditRecord>().Any())
            {
                throw new InvalidOperationException("forced audit persistence failure");
            }

            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is PlatformDbContext platform
                && platform.ChangeTracker.Entries<BusinessAuditRecord>().Any())
            {
                throw new InvalidOperationException("forced audit persistence failure");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
