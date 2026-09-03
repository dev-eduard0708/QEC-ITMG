using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Platform.Attachments;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Platform;

public sealed class AttachmentStorageTests
{
    [Fact]
    public async Task StoreAndRead_RoundTripsMetadataAndContent()
    {
        string root = CreateTempRoot();
        try
        {
            AttachmentStorageOptions options = new()
            {
                RootPath = root,
                MaxFileSizeBytes = 1024 * 1024,
            };

            IClock clock = new FixedClock(new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero));

            DbContextOptions<PlatformDbContext> dbOptions = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase($"att-{Guid.NewGuid():N}")
                .Options;

            await using PlatformDbContext db = new(dbOptions);
            LocalDiskFileStorage fileStorage = new(Options.Create(options));
            AttachmentStorageService storageService = new(db, clock, fileStorage);

            byte[] content = [1, 2, 3, 4, 5, 6];
            using MemoryStream stream = new(content);

            AttachmentMetadata metadata = await storageService.StoreAsync(
                content: stream,
                originalFileName: "hello.txt",
                contentType: "text/plain",
                uploadedByUserId: Guid.NewGuid(),
                cancellationToken: default);

            Assert.NotEqual(Guid.Empty, metadata.Id);
            Assert.False(string.IsNullOrWhiteSpace(metadata.StorageKey));

            AttachmentMetadata? loaded = await storageService.GetMetadataAsync(metadata.Id);
            Assert.NotNull(loaded);
            Assert.Equal(metadata.Sha256, loaded!.Sha256);

            using Stream read = await storageService.OpenReadAsync(metadata.Id);
            byte[] roundTrip = await ToArrayAsync(read);
            Assert.Equal(content, roundTrip);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Store_OriginalFileName_DoesNotAllowPathTraversal()
    {
        string root = CreateTempRoot();
        try
        {
            AttachmentStorageOptions options = new()
            {
                RootPath = root,
                MaxFileSizeBytes = 1024 * 1024,
            };

            string? outsidePath = null;
            try
            {
                outsidePath = Path.GetFullPath(Path.Combine(root, "..", "outside-from-test.txt"));
            }
            catch
            {
                // ignore; path resolution might fail on some environments.
            }

            if (outsidePath is not null && File.Exists(outsidePath))
            {
                File.Delete(outsidePath);
            }

            IClock clock = new FixedClock(new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero));
            DbContextOptions<PlatformDbContext> dbOptions = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase($"att-{Guid.NewGuid():N}")
                .Options;

            await using PlatformDbContext db = new(dbOptions);
            LocalDiskFileStorage fileStorage = new(Options.Create(options));
            AttachmentStorageService storageService = new(db, clock, fileStorage);

            byte[] content = [10, 20, 30];
            using MemoryStream stream = new(content);

            _ = await storageService.StoreAsync(
                content: stream,
                originalFileName: ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + "outside-from-test.txt",
                contentType: "application/octet-stream",
                uploadedByUserId: Guid.NewGuid(),
                cancellationToken: default);

            if (outsidePath is not null)
            {
                Assert.False(File.Exists(outsidePath));
            }
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Store_WhenFileIsOversized_RejectsAndDoesNotPersistFile()
    {
        string root = CreateTempRoot();
        try
        {
            AttachmentStorageOptions options = new()
            {
                RootPath = root,
                MaxFileSizeBytes = 4, // intentionally tiny
            };

            IClock clock = new FixedClock(new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero));
            DbContextOptions<PlatformDbContext> dbOptions = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase($"att-{Guid.NewGuid():N}")
                .Options;

            await using PlatformDbContext db = new(dbOptions);
            LocalDiskFileStorage fileStorage = new(Options.Create(options));
            AttachmentStorageService storageService = new(db, clock, fileStorage);

            byte[] content = [1, 2, 3, 4, 5]; // 5 bytes > 4
            using MemoryStream stream = new(content);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.StoreAsync(
                    content: stream,
                    originalFileName: "too-big.bin",
                    contentType: "application/octet-stream",
                    uploadedByUserId: Guid.NewGuid(),
                    cancellationToken: default));

            Assert.Empty(Directory.EnumerateFiles(root));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), $"qec-itmg-att-{Guid.NewGuid():N}");
        return root;
    }

    private static void TryDeleteDirectory(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    private static async Task<byte[]> ToArrayAsync(Stream input)
    {
        using MemoryStream ms = new();
        await input.CopyToAsync(ms);
        return ms.ToArray();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}

