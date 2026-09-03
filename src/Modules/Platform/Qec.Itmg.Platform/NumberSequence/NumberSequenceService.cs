using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
            // SERIALIZABLE ensures parallel callers cannot issue duplicate numbers for the same (SequenceKey, Year).
            await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

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
            await tx.CommitAsync(cancellationToken);
        }

        return Format(normalizedPrefix, year, issued);
    }

    private static string Format(string prefix, int year, long issued)
    {
        // business contract: PREFIX-YYYY-000001
        string sequence = issued.ToString("000000", CultureInfo.InvariantCulture);
        return $"{prefix}-{year}-{sequence}";
    }
}

