using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Qec.Itmg.Organization.Domain;
using Qec.Itmg.Organization.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Organization;

public sealed class OrganizationModelTests
{
    [Fact]
    public void Model_MapsOrgSchema_UniqueNames_AndRowVersions()
    {
        using OrganizationDbContext context = CreateContext();
        IModel model = context.Model;

        IEntityType department = GetEntity(model, typeof(Department));
        IEntityType location = GetEntity(model, typeof(Location));

        Assert.Equal("org", department.GetSchema());
        Assert.Equal("Department", department.GetTableName());
        Assert.Equal("org", location.GetSchema());
        Assert.Equal("Location", location.GetTableName());

        Assert.Contains(department.GetIndexes(), index => index.IsUnique && IndexHasProperty(index, "Name"));
        Assert.Contains(location.GetIndexes(), index => index.IsUnique && IndexHasProperty(index, "Name"));

        Assert.True(department.FindProperty(nameof(Department.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("rowversion", department.FindProperty(nameof(Department.RowVersion))!.GetColumnType());
        Assert.True(location.FindProperty(nameof(Location.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("rowversion", location.FindProperty(nameof(Location.RowVersion))!.GetColumnType());

        Assert.Equal("datetimeoffset", department.FindProperty(nameof(Department.CreatedAtUtc))!.GetColumnType());
        Assert.Equal("datetimeoffset", location.FindProperty(nameof(Location.UpdatedAtUtc))!.GetColumnType());
    }

    private static OrganizationDbContext CreateContext()
    {
        DbContextOptions<OrganizationDbContext> options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseSqlServer("Server=.;Database=QecItmg_ModelProbe;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new OrganizationDbContext(options);
    }

    private static IEntityType GetEntity(IModel model, Type clrType) =>
        model.FindEntityType(clrType)
        ?? throw new InvalidOperationException($"Entity type '{clrType.Name}' was not found in the model.");

    private static bool IndexHasProperty(IIndex index, string propertyName) =>
        index.Properties.Any(property => property.Name == propertyName);
}
