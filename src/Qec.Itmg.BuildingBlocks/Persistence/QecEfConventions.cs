using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Qec.Itmg.BuildingBlocks.Persistence;

/// <summary>
/// Shared EF Core model conventions for QEC ITMG module DbContexts.
/// </summary>
public static class QecEfConventions
{
    public const string ConnectionStringName = "QecItmg";

    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetColumnType("datetimeoffset");
                }

                if (property.Name == "RowVersion" && property.ClrType == typeof(byte[]))
                {
                    property.IsConcurrencyToken = true;
                    property.ValueGenerated = ValueGenerated.OnAddOrUpdate;
                    property.SetColumnType("rowversion");
                }
            }
        }
    }
}
