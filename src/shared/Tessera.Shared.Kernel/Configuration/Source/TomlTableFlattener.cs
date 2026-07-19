using Tomlyn.Model;

namespace Tessera.Shared.Kernel.Configuration;

/// <summary>
///     Pure recursive <see cref="TomlTable" /> / <see cref="TomlArray" />
///     flattener used by <see cref="TomlConfigurationProvider" />. Nested
///     tables recurse with a colon-joined prefix (e.g. <c>victoria:traces</c>);
///     arrays index by ordinal (<c>victoria:traces:0</c>, ...). Scalar values
///     fall through to <c>value?.ToString()</c>. Lives in a sibling
///     <c>internal static class</c> so the provider itself stays focused on
///     stream / parse wiring without carrying private methods
///     (code-shape.md §9 ban).
/// </summary>
internal static class TomlTableFlattener
{
    public static void FlattenTable(TomlTable table, string prefix, Dictionary<string, string?> data)
    {
        foreach (var (key, value) in table)
        {
            var path = string.IsNullOrEmpty(prefix) ? key : prefix + ":" + key;
            FlattenValue(value, path, data);
        }
    }

    public static void FlattenValue(object? value, string path, Dictionary<string, string?> data)
    {
        switch (value)
        {
            case TomlTable nested:
                FlattenTable(nested, path, data);
                break;

            case TomlArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    FlattenValue(array[i], path + ":" + i, data);
                }

                break;

            default:
                data[path] = value?.ToString();
                break;
        }
    }
}
