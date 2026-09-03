using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Organization.Domain;
using Qec.Itmg.Organization.Persistence;

namespace Qec.Itmg.Organization.Admin;

public sealed record LookupItemDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    string RowVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateLookupItemRequest(string Name, string? Description);

public sealed record UpdateLookupItemRequest(
    string Name,
    string? Description,
    bool IsActive,
    string RowVersion);

public sealed class LookupOperationResult
{
    public int StatusCode { get; init; }

    public LookupItemDto? Item { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public string? Field { get; init; }

    public static LookupOperationResult Ok(LookupItemDto item, int statusCode = 200) =>
        new() { StatusCode = statusCode, Item = item };

    public static LookupOperationResult Created(LookupItemDto item) => Ok(item, 201);

    public static LookupOperationResult Validation(string code, string message, string? field = null) =>
        new()
        {
            StatusCode = 400,
            ErrorCode = code,
            ErrorMessage = message,
            Field = field,
        };

    public static LookupOperationResult NotFound(string code, string message) =>
        new()
        {
            StatusCode = 404,
            ErrorCode = code,
            ErrorMessage = message,
        };

    public static LookupOperationResult Conflict(string code, string message) =>
        new()
        {
            StatusCode = 409,
            ErrorCode = code,
            ErrorMessage = message,
        };
}

public sealed class LookupAdminService(OrganizationDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<LookupItemDto>> ListDepartmentsAsync(CancellationToken cancellationToken)
    {
        List<Department> items = await db.Departments.AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return items.Select(MapDepartment).ToList();
    }

    public async Task<LookupOperationResult> CreateDepartmentAsync(
        CreateLookupItemRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return LookupOperationResult.Validation(
                "admin.lookups.departments.nameRequired",
                "Name is required.",
                "name");
        }

        string name = request.Name.Trim();
        bool exists = await db.Departments.AsNoTracking().AnyAsync(item => item.Name == name, cancellationToken);
        if (exists)
        {
            return LookupOperationResult.Conflict(
                "admin.lookups.departments.nameConflict",
                "A department with this name already exists.");
        }

        Department entity = Department.Create(name, clock.UtcNow, request.Description);
        db.Departments.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return LookupOperationResult.Created(MapDepartment(entity));
    }

    public async Task<LookupOperationResult> UpdateDepartmentAsync(
        Guid id,
        UpdateLookupItemRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return LookupOperationResult.Validation(
                "admin.lookups.departments.nameRequired",
                "Name is required.",
                "name");
        }

        if (!TryParseRowVersion(request.RowVersion, out _))
        {
            return LookupOperationResult.Validation(
                "admin.lookups.departments.rowVersionInvalid",
                "Row version is invalid.",
                "rowVersion");
        }

        Department? entity = await db.Departments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return LookupOperationResult.NotFound(
                "admin.lookups.departments.notFound",
                "Department was not found.");
        }

        if (!MatchesRowVersion(entity.RowVersion, request.RowVersion))
        {
            return LookupOperationResult.Conflict(
                "admin.lookups.departments.concurrencyConflict",
                "The department was modified by another user. Refresh and try again.");
        }

        string name = request.Name.Trim();
        bool nameTaken = await db.Departments.AsNoTracking()
            .AnyAsync(item => item.Name == name && item.Id != id, cancellationToken);
        if (nameTaken)
        {
            return LookupOperationResult.Conflict(
                "admin.lookups.departments.nameConflict",
                "A department with this name already exists.");
        }

        Apply(entity, name, request.Description, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return LookupOperationResult.Ok(MapDepartment(entity));
    }

    public async Task<IReadOnlyList<LookupItemDto>> ListLocationsAsync(CancellationToken cancellationToken)
    {
        List<Location> items = await db.Locations.AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return items.Select(MapLocation).ToList();
    }

    public async Task<LookupOperationResult> CreateLocationAsync(
        CreateLookupItemRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return LookupOperationResult.Validation(
                "admin.lookups.locations.nameRequired",
                "Name is required.",
                "name");
        }

        string name = request.Name.Trim();
        bool exists = await db.Locations.AsNoTracking().AnyAsync(item => item.Name == name, cancellationToken);
        if (exists)
        {
            return LookupOperationResult.Conflict(
                "admin.lookups.locations.nameConflict",
                "A location with this name already exists.");
        }

        Location entity = Location.Create(name, clock.UtcNow, request.Description);
        db.Locations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return LookupOperationResult.Created(MapLocation(entity));
    }

    public async Task<LookupOperationResult> UpdateLocationAsync(
        Guid id,
        UpdateLookupItemRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return LookupOperationResult.Validation(
                "admin.lookups.locations.nameRequired",
                "Name is required.",
                "name");
        }

        if (!TryParseRowVersion(request.RowVersion, out _))
        {
            return LookupOperationResult.Validation(
                "admin.lookups.locations.rowVersionInvalid",
                "Row version is invalid.",
                "rowVersion");
        }

        Location? entity = await db.Locations.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return LookupOperationResult.NotFound(
                "admin.lookups.locations.notFound",
                "Location was not found.");
        }

        if (!MatchesRowVersion(entity.RowVersion, request.RowVersion))
        {
            return LookupOperationResult.Conflict(
                "admin.lookups.locations.concurrencyConflict",
                "The location was modified by another user. Refresh and try again.");
        }

        string name = request.Name.Trim();
        bool nameTaken = await db.Locations.AsNoTracking()
            .AnyAsync(item => item.Name == name && item.Id != id, cancellationToken);
        if (nameTaken)
        {
            return LookupOperationResult.Conflict(
                "admin.lookups.locations.nameConflict",
                "A location with this name already exists.");
        }

        Apply(entity, name, request.Description, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return LookupOperationResult.Ok(MapLocation(entity));
    }

    private void Apply(Department entity, string name, string? description, bool isActive)
    {
        DateTimeOffset utcNow = clock.UtcNow;
        entity.Rename(name, utcNow);
        entity.UpdateDescription(description, utcNow);
        if (isActive)
        {
            entity.Activate(utcNow);
        }
        else
        {
            entity.Deactivate(utcNow);
        }
    }

    private void Apply(Location entity, string name, string? description, bool isActive)
    {
        DateTimeOffset utcNow = clock.UtcNow;
        entity.Rename(name, utcNow);
        entity.UpdateDescription(description, utcNow);
        if (isActive)
        {
            entity.Activate(utcNow);
        }
        else
        {
            entity.Deactivate(utcNow);
        }
    }

    private static LookupItemDto MapDepartment(Department entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.IsActive,
            Convert.ToBase64String(entity.RowVersion),
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);

    private static LookupItemDto MapLocation(Location entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.IsActive,
            Convert.ToBase64String(entity.RowVersion),
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);

    private static bool TryParseRowVersion(string? value, out byte[] rowVersion)
    {
        rowVersion = Array.Empty<byte>();
        if (value is null)
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        try
        {
            rowVersion = Convert.FromBase64String(trimmed);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool MatchesRowVersion(byte[] current, string expectedBase64)
    {
        if (!TryParseRowVersion(expectedBase64, out byte[] expected))
        {
            return false;
        }

        return current.AsSpan().SequenceEqual(expected);
    }
}
