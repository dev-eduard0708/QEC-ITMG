using System.Reflection;
using NetArchTest.Rules;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Identity;
using Qec.Itmg.Organization;
using Qec.Itmg.Platform;
using Xunit;

namespace Qec.Itmg.ArchitectureTests;

/// <summary>
/// Lightweight assembly dependency boundaries for the modular monolith.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Assembly BuildingBlocks = typeof(IClock).Assembly;
    private static readonly Assembly Contracts = typeof(IModule).Assembly;
    private static readonly Assembly Host = typeof(Program).Assembly;
    private static readonly Assembly Identity = typeof(IdentityModule).Assembly;
    private static readonly Assembly Organization = typeof(OrganizationModule).Assembly;
    private static readonly Assembly Platform = typeof(PlatformModule).Assembly;
    private static readonly Assembly Cmdb = typeof(CmdbModule).Assembly;

    private static readonly string[] FeatureModules =
    [
        "Qec.Itmg.Identity",
        "Qec.Itmg.Organization",
        "Qec.Itmg.Platform",
        "Qec.Itmg.Cmdb",
    ];

    [Fact]
    public void BuildingBlocks_MustNotDependOn_HostOrFeatureModules()
    {
        AssertNoDependencyOn(BuildingBlocks, ["Qec.Itmg.Host", .. FeatureModules]);
    }

    [Fact]
    public void Contracts_MustNotDependOn_HostOrFeatureModules()
    {
        AssertNoDependencyOn(Contracts, ["Qec.Itmg.Host", .. FeatureModules]);
    }

    [Fact]
    public void Identity_MustNotDependOn_OrganizationOrPlatformOrCmdb()
    {
        AssertNoDependencyOn(Identity, ["Qec.Itmg.Organization", "Qec.Itmg.Platform", "Qec.Itmg.Cmdb"]);
    }

    [Fact]
    public void Organization_MustNotDependOn_IdentityOrPlatformOrCmdb()
    {
        AssertNoDependencyOn(Organization, ["Qec.Itmg.Identity", "Qec.Itmg.Platform", "Qec.Itmg.Cmdb"]);
    }

    [Fact]
    public void Platform_MustNotDependOn_IdentityOrOrganizationOrCmdb()
    {
        AssertNoDependencyOn(Platform, ["Qec.Itmg.Identity", "Qec.Itmg.Organization", "Qec.Itmg.Cmdb"]);
    }

    [Fact]
    public void Cmdb_MustNotDependOn_IdentityOrOrganizationOrPlatform()
    {
        AssertNoDependencyOn(Cmdb, ["Qec.Itmg.Identity", "Qec.Itmg.Organization", "Qec.Itmg.Platform"]);
    }

    [Fact]
    public void FeatureModules_MustNotReference_Host()
    {
        AssertNoDependencyOn(Identity, ["Qec.Itmg.Host"]);
        AssertNoDependencyOn(Organization, ["Qec.Itmg.Host"]);
        AssertNoDependencyOn(Platform, ["Qec.Itmg.Host"]);
        AssertNoDependencyOn(Cmdb, ["Qec.Itmg.Host"]);
    }

    [Fact]
    public void Host_MayReference_FeatureModules_AsCompositionRoot()
    {
        Assert.Contains(
            Types.InAssembly(Host).That().HaveDependencyOn("Qec.Itmg.Identity").GetTypes(),
            static _ => true);

        Assert.Contains(
            Types.InAssembly(Host).That().HaveDependencyOn("Qec.Itmg.Organization").GetTypes(),
            static _ => true);

        Assert.Contains(
            Types.InAssembly(Host).That().HaveDependencyOn("Qec.Itmg.Platform").GetTypes(),
            static _ => true);

        Assert.Contains(
            Types.InAssembly(Host).That().HaveDependencyOn("Qec.Itmg.Cmdb").GetTypes(),
            static _ => true);
    }

    private static void AssertNoDependencyOn(Assembly assembly, IEnumerable<string> forbiddenNamespaces)
    {
        TestResult result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenNamespaces.ToArray())
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    private static string FormatFailures(TestResult result)
    {
        if (result.IsSuccessful)
        {
            return string.Empty;
        }

        IEnumerable<string> failingTypes = result.FailingTypes?.Select(static type => type.FullName ?? type.Name)
            ?? [];

        return "Architecture rule failed for: " + string.Join(", ", failingTypes);
    }
}
