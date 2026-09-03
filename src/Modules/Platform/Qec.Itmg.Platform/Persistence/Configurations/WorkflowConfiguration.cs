using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qec.Itmg.Platform.Domain;

namespace Qec.Itmg.Platform.Persistence.Configurations;

public sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinition", PlatformDbContext.SchemaName);
        builder.HasKey(definition => definition.Id);
        builder.Property(definition => definition.Id).ValueGeneratedNever();

        builder.Property(definition => definition.Key)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(definition => definition.Name)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(definition => definition.Version).IsRequired();
        builder.Property(definition => definition.IsActive).IsRequired();

        builder.HasIndex(definition => new { definition.Key, definition.Version })
            .IsUnique()
            .HasDatabaseName("UX_WorkflowDefinition_Key_Version");

        builder.HasMany(definition => definition.States)
            .WithOne()
            .HasForeignKey(state => state.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(definition => definition.Transitions)
            .WithOne()
            .HasForeignKey(transition => transition.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkflowStateConfiguration : IEntityTypeConfiguration<WorkflowState>
{
    public void Configure(EntityTypeBuilder<WorkflowState> builder)
    {
        builder.ToTable("WorkflowState", PlatformDbContext.SchemaName);
        builder.HasKey(state => state.Id);
        builder.Property(state => state.Id).ValueGeneratedNever();

        builder.Property(state => state.Key)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("nvarchar(64)");

        builder.Property(state => state.Name)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(state => state.IsInitial).IsRequired();
        builder.Property(state => state.IsTerminal).IsRequired();

        builder.HasIndex(state => new { state.WorkflowDefinitionId, state.Key })
            .IsUnique()
            .HasDatabaseName("UX_WorkflowState_Definition_Key");
    }
}

public sealed class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransition", PlatformDbContext.SchemaName);
        builder.HasKey(transition => transition.Id);
        builder.Property(transition => transition.Id).ValueGeneratedNever();

        builder.Property(transition => transition.RequiredPermission)
            .HasMaxLength(128)
            .HasColumnType("nvarchar(128)");

        builder.Property(transition => transition.RequiresReason).IsRequired();

        builder.HasIndex(transition => new { transition.WorkflowDefinitionId, transition.FromStateId, transition.ToStateId })
            .IsUnique()
            .HasDatabaseName("UX_WorkflowTransition_From_To");
    }
}
