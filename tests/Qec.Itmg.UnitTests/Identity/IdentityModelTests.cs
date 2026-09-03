using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Identity;

public sealed class IdentityModelTests
{
    [Fact]
    public void Model_HasUniqueIndexes_AndRowVersions()
    {
        using IdentityDbContext context = CreateContext();
        IModel model = context.Model;

        IEntityType user = GetEntity(model, typeof(User));
        IEntityType role = GetEntity(model, typeof(Role));
        IEntityType permission = GetEntity(model, typeof(Permission));
        IEntityType userRole = GetEntity(model, typeof(UserRole));
        IEntityType rolePermission = GetEntity(model, typeof(RolePermission));

        Assert.Contains(user.GetIndexes(), index => index.IsUnique && IndexHasProperty(index, "Upn"));
        Assert.Contains(role.GetIndexes(), index => index.IsUnique && IndexHasProperty(index, "Name"));
        Assert.Contains(permission.GetIndexes(), index => index.IsUnique && IndexHasProperty(index, "Key"));

        Assert.Equal(["UserId", "RoleId"], userRole.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(["RoleId", "PermissionId"], rolePermission.FindPrimaryKey()!.Properties.Select(property => property.Name));

        Assert.True(user.FindProperty(nameof(User.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("rowversion", user.FindProperty(nameof(User.RowVersion))!.GetColumnType());
        Assert.True(role.FindProperty(nameof(Role.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("rowversion", role.FindProperty(nameof(Role.RowVersion))!.GetColumnType());

        Assert.Equal(
            DeleteBehavior.Restrict,
            userRole.FindNavigation(nameof(UserRole.User))!.ForeignKey.DeleteBehavior);
        Assert.Equal(
            DeleteBehavior.Restrict,
            rolePermission.FindNavigation(nameof(RolePermission.Permission))!.ForeignKey.DeleteBehavior);
    }

    [Fact]
    public void UserRole_And_RolePermission_Factories_RequireIds()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => UserRole.Create(Guid.Empty, Guid.NewGuid(), now));
        Assert.Throws<ArgumentException>(() => RolePermission.Create(Guid.NewGuid(), Guid.Empty));

        UserRole assignment = UserRole.Create(Guid.NewGuid(), Guid.NewGuid(), now);
        Assert.Equal(now, assignment.AssignedAtUtc);
    }

    private static IdentityDbContext CreateContext()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=.;Database=QecItmg_ModelProbe;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new IdentityDbContext(options);
    }

    private static IEntityType GetEntity(IModel model, Type clrType) =>
        model.FindEntityType(clrType)
        ?? throw new InvalidOperationException($"Entity type '{clrType.Name}' was not found in the model.");

    private static bool IndexHasProperty(IIndex index, string propertyName) =>
        index.Properties.Any(property => property.Name == propertyName);
}
