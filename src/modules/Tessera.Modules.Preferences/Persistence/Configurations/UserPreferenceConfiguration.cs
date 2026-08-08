using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tessera.Modules.Preferences.Application;

namespace Tessera.Modules.Preferences.Persistence.Configurations;

/// <summary>
///     Per-module <see cref="UserPreference" /> EF mapping. Owns
///     table / column / length / nullability / index — no data
///     annotations on the entity itself (per
///     <c>csharp/ef-core.md</c> §1).
///     <para>
///         snake_case column names — declared explicitly via
///         <c>HasColumnName</c> so the design-time migration
///         snapshot shows them. <c>csharp/ef-core.md</c> §3 enforces
///         both the runtime snake-case naming convention AND the
///         explicit <c>HasColumnName</c> for the migration output.
///     </para>
///     <para>
///         String properties always carry an explicit
///         <c>HasMaxLength</c>; the
///         <see cref="UserPreference.ValueJson" /> column is bound
///         to <c>jsonb</c> for queryability (current reads project to
///         text but the shape is already in the database).
///     </para>
/// </summary>
public sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    /// <summary>Per-module table name constants. Avoids magic strings.</summary>
    public static class Tables
    {
        /// <summary>Table name for the user_preferences aggregate.</summary>
        public const string UserPreferences = "user_preferences";
    }

    /// <summary>Per-module schema constants. MVP-01 keeps <c>"tessera"</c>.</summary>
    public static class Schemes
    {
        /// <summary>Schema name for all module-owned tables.</summary>
        public const string Tessera = "tessera";
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable(Tables.UserPreferences, Schemes.Tessera);
        builder.HasKey(static preference => preference.Id);

        builder.Property(static preference => preference.Id).HasColumnName("id").HasMaxLength(64).IsRequired();
        builder.Property(static preference => preference.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(static preference => preference.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(static preference => preference.Key).HasColumnName("key").HasMaxLength(128).IsRequired();
        builder.Property(static preference => preference.ValueJson)
            .HasColumnName("value_json")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(static preference => preference.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(static preference => preference.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(static preference => preference.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(static preference => new { preference.UserId, preference.Key })
            .HasDatabaseName("ux_user_preferences_user_key")
            .IsUnique();

        builder.HasIndex(static preference => preference.TenantId)
            .HasDatabaseName("ix_user_preferences_tenant_id");

        builder.HasIndex(static preference => preference.DeletedAt)
            .HasDatabaseName("ix_user_preferences_deleted_at");
    }
}
