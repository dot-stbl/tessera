using Microsoft.Extensions.Configuration;
using Tomlyn.Model;

namespace Tessera.Shared.Kernel.Configuration.Source;

/// <summary>
///     <see cref="FileConfigurationProvider" /> that parses a TOML file via
///     Tomlyn and flattens the resulting <see cref="TomlTable" /> into the
///     <c>section:key</c> shape ASP.NET Core <see cref="IConfiguration" />
///     expects. Nested tables become colons (e.g. <c>[victoria.traces]</c>
///     table → <c>victoria:traces:*</c> keys); arrays become indexed
///     (<c>array.0</c>, <c>array.1</c>, ...).
/// </summary>
/// <remarks>
///     Keys are emitted case-insensitively (OrdinalIgnoreCase) so
///     <c>VictoriaOptions</c> binding works regardless of TOML case conventions.
///     Unknown value types fall through to <c>value?.ToString()</c> — covers
///     strings, numbers, booleans, datetimes; complex types (TomlTable,
///     TomlArray) are handled by recursive flattening in
///     <see cref="TomlTableFlattener" />.
/// </remarks>
public sealed class TomlConfigurationProvider(TomlConfigurationSource source) : FileConfigurationProvider(source)
{
    /// <inheritdoc />
    public override void Load(Stream stream)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StreamReader(stream);
        var tomlText = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(tomlText))
        {
            Data = data;
            return;
        }

        var model = Tomlyn.TomlSerializer.Deserialize<TomlTable>(tomlText);
        if (model is not null)
        {
            TomlTableFlattener.FlattenTable(model, prefix: string.Empty, data);
        }

        Data = data;
    }
}
