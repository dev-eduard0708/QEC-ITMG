using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Threading;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.NumberSequence;

public sealed class NumberSequenceService(
    PlatformDbContext db,
    IClock clock) : INumberSequenceService
{
    private static readonly SemaphoreSlim NonRelationalLock = new(1, 1);

    public async Task<string> NextAsync(
        string sequenceKey,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sequenceKey, nameof(sequenceKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix, nameof(prefix));

        string normalizedKey = sequenceKey.Trim();
        string normalizedPrefix = prefix.Trim();

        int year = clock.UtcNow.Year;

        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await NextAsyncOnce(normalizedKey, normalizedPrefix, year, cancellationToken);
            }
            catch (Exception ex) when (IsTransientSqlException(ex))
            {
                if (attempt == maxAttempts)
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(75 * attempt), cancellationToken);
            }
        }

        // should be unreachable
        return await NextAsyncOnce(normalizedKey, normalizedPrefix, year, cancellationToken);
    }

    private async Task<string> NextAsyncOnce(
        string normalizedKey,
        string normalizedPrefix,
        int year,
        CancellationToken cancellationToken)
    {
        long issued;

        if (!db.Database.IsRelational())
        {
            // Unit-test friendly fallback for non-relational providers.
            // Production concurrency correctness comes from SERIALIZABLE + SQL Server.
            await NonRelationalLock.WaitAsync(cancellationToken);
            try
            {
                Qec.Itmg.Platform.Domain.NumberSequence? current =
                    await db.NumberSequences.SingleOrDefaultAsync(
                        candidate => candidate.SequenceKey == normalizedKey && candidate.Year == year,
                        cancellationToken);

                if (current is null)
                {
                    issued = 1;
                    db.NumberSequences.Add(new Qec.Itmg.Platform.Domain.NumberSequence(normalizedKey, year, nextValue: 2));
                }
                else
                {
                    issued = current.NextValue;
                    current.ConsumeNextValue();
                }

                await db.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                NonRelationalLock.Release();
            }
        }
        else
        {
            // Atomic SQL Server update/insert so parallel callers cannot produce duplicate numbers.
            // MERGE is a single statement that either inserts the initial row (NextValue=2, issued=1) or
            // increments NextValue and issues the previous value.
            System.Data.Common.DbConnection connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using System.Data.Common.DbCommand command = connection.CreateCommand();
            command.CommandText = @"
DECLARE @Out TABLE (Issued bigint);

;MERGE [plt].[NumberSequence] AS target
USING (SELECT @sequenceKey AS [SequenceKey], @year AS [Year]) AS source
    ON target.[SequenceKey] = source.[SequenceKey] AND target.[Year] = source.[Year]
WHEN MATCHED THEN
    UPDATE SET [NextValue] = target.[NextValue] + 1
WHEN NOT MATCHED THEN
    INSERT ([SequenceKey], [Year], [NextValue])
    VALUES (source.[SequenceKey], source.[Year], 2)
OUTPUT
    CASE
        WHEN $action = 'INSERT' THEN inserted.[NextValue] - 1
        ELSE deleted.[NextValue]
    END
INTO @Out(Issued);

SELECT TOP(1) Issued FROM @Out;";

            // Parameters keep this safe and allow plan caching.
            System.Data.Common.DbParameter p1 = command.CreateParameter();
            p1.ParameterName = "@sequenceKey";
            p1.DbType = System.Data.DbType.String;
            p1.Size = 64;
            p1.Value = normalizedKey;
            command.Parameters.Add(p1);

            System.Data.Common.DbParameter p2 = command.CreateParameter();
            p2.ParameterName = "@year";
            p2.DbType = System.Data.DbType.Int32;
            p2.Value = year;
            command.Parameters.Add(p2);

            object? scalar = await command.ExecuteScalarAsync(cancellationToken);
            if (scalar is null || scalar is DBNull)
            {
                throw new InvalidOperationException("NumberSequence issuance failed: no scalar value returned.");
            }

            issued = Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
        }

        return Format(normalizedPrefix, year, issued);
    }

    private static bool IsTransientSql(SqlException ex)
    {
        // 1205 = deadlock victim
        // 3960 = snapshot/serializable isolation deadlock/serialization failure
        return ex.Number is 1205 or 3960;
    }

    private static bool IsTransientSqlException(Exception exception)
    {
        if (exception is SqlException sql)
        {
            return IsTransientSql(sql);
        }

        return exception.InnerException is not null && IsTransientSqlException(exception.InnerException);
    }

    private static string Format(string prefix, int year, long issued)
    {
        // business contract: PREFIX-YYYY-000001
        string sequence = issued.ToString("000000", CultureInfo.InvariantCulture);
        return $"{prefix}-{year}-{sequence}";
    }
}

