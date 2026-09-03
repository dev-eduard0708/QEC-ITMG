using Qec.Itmg.Organization.Domain;
using Xunit;

namespace Qec.Itmg.UnitTests.Organization;

public sealed class DepartmentLocationDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Department_Create_DefaultsActive()
    {
        Department department = Department.Create("IT", Now, " Information Technology ");

        Assert.NotEqual(Guid.Empty, department.Id);
        Assert.Equal("IT", department.Name);
        Assert.Equal("Information Technology", department.Description);
        Assert.True(department.IsActive);
        Assert.Equal(Now, department.CreatedAtUtc);
        Assert.Equal(Now, department.UpdatedAtUtc);
    }

    [Fact]
    public void Location_Create_DefaultsActive()
    {
        Location location = Location.Create("Main Campus", Now);

        Assert.True(location.IsActive);
        Assert.Null(location.Description);
    }

    [Fact]
    public void Rename_And_UpdateDescription_UpdateTimestamps()
    {
        Department department = Department.Create("Ops", Now);
        DateTimeOffset renamedAt = Now.AddMinutes(1);
        DateTimeOffset describedAt = Now.AddMinutes(2);

        department.Rename("Operations", renamedAt);
        Assert.Equal("Operations", department.Name);
        Assert.Equal(renamedAt, department.UpdatedAtUtc);

        department.UpdateDescription(" Ops desk ", describedAt);
        Assert.Equal("Ops desk", department.Description);
        Assert.Equal(describedAt, department.UpdatedAtUtc);
    }

    [Fact]
    public void Activate_And_Deactivate_ToggleActive()
    {
        Location location = Location.Create("Lab", Now);
        DateTimeOffset deactivatedAt = Now.AddMinutes(5);
        DateTimeOffset activatedAt = Now.AddMinutes(6);

        location.Deactivate(deactivatedAt);
        Assert.False(location.IsActive);
        Assert.Equal(deactivatedAt, location.UpdatedAtUtc);

        location.Activate(activatedAt);
        Assert.True(location.IsActive);
        Assert.Equal(activatedAt, location.UpdatedAtUtc);
    }

    [Fact]
    public void Create_RejectsBlankName()
    {
        Assert.Throws<ArgumentException>(() => Department.Create(" ", Now));
        Assert.Throws<ArgumentException>(() => Location.Create("", Now));
    }
}
