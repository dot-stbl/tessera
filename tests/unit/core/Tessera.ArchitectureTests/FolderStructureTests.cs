namespace Tessera.ArchitectureTests;

/// <summary>
///     Folder- and project-structure rules — enforces
///     <c>module-structure-5-cap.md</c>: every physical folder under
///     <c>src/</c> that contains project directories has at most 5 direct-child
///     <c>.csproj</c> projects (cap is per-folder, not recursive total).
///     Provider naming + host location sanity checks live here too.
/// </summary>
public sealed class FolderStructureTests
{
    private static readonly string SrcRoot = FolderStructureProbe.ResolveSrcRoot();

    /// <summary>
    ///     Every folder under <c>src/</c> that has at least one direct-child
    ///     project directory (a subfolder containing a <c>*.csproj</c> named
    ///     after the folder) must keep that count ≤ 5. Nesting is the
    ///     prescribed escape hatch when a layer grows past the cap.
    /// </summary>
    public static TheoryData<string> FoldersWithProjects()
    {
        var data = new TheoryData<string>();
        if (!Directory.Exists(SrcRoot))
        {
            return data;
        }

        foreach (var folder in FolderStructureProbe.EnumerateFoldersWithProjectChildren(SrcRoot))
        {
            data.Add(folder);
        }

        return data;
    }

    /// <inheritdoc/>
    [Theory]
    [MemberData(nameof(FoldersWithProjects))]
    public void Folder_DoesNotExceedFiveProjectCap(string folder)
    {
        var count = FolderStructureProbe.CountDirectProjectChildren(folder);
        var relative = Path.GetRelativePath(Path.GetDirectoryName(SrcRoot)!, folder);

        Assert.True(count <= 5,
            $"Folder '{relative}' has {count} direct-child projects (cap is 5 per module-structure-5-cap.md). Nest if more needed.");
    }

    /// <summary>
    ///     <c>src/providers/</c> follows the Grafana datasource model — one
    ///     concrete backend per project (Tessera.Providers.&lt;Name&gt;).
    ///     No upper cap documented (separate concerns: future Tempo/Jaeger/Loki
    ///     each become a project here). Today = 1.
    /// </summary>
    [Fact]
    public void ProviderProjects_FollowGrafanaDatasourceModel()
    {
        var providersDir = Path.Combine(SrcRoot, "providers");
        var projectFiles = Directory
            .EnumerateDirectories(providersDir)
            .Select(Path.GetFileName)
            .Where(static name => name!.StartsWith("Tessera.Providers.", StringComparison.Ordinal))
            .ToArray();

        Assert.All(projectFiles, static name =>
        {
            // Tessera.Providers.X where X is one concrete backend
            var backend = name!["Tessera.Providers.".Length..];
            Assert.Matches("^[A-Z][A-Za-z0-9]+$", backend);
        });
    }

    /// <summary>
    ///     Sanity check — Tessera.Host must NOT be inside <c>src/shared/</c> or
    ///     <c>src/modules/</c>. The host is its own top-level layer, the only
    ///     place where cross-cutting composition lives.
    /// </summary>
    [Fact]
    public void HostIsNotInSharedOrModulesFolders()
    {
        var sharedDir = Path.Combine(SrcRoot, "shared", "Tessera.Host");
        var modulesDir = Path.Combine(SrcRoot, "modules", "Tessera.Host");
        Assert.False(Directory.Exists(sharedDir), "Tessera.Host must not live in src/shared/");
        Assert.False(Directory.Exists(modulesDir), "Tessera.Host must not live in src/modules/");
        Assert.True(Directory.Exists(Path.Combine(SrcRoot, "host", "Tessera.Host")),
            "Tessera.Host must live in src/host/.");
    }
}

/// <summary>
///     Filesystem probes for <see cref="FolderStructureTests" />. Kept
///     file-local so the test class stays free of private helpers.
/// </summary>
file static class FolderStructureProbe
{
    /// <summary>
    ///     Locate the repository's <c>src/</c> directory by walking up from
    ///     this test assembly's location. The standard base-directory walk is
    ///     <c>bin/Debug/net10.0 → bin/Debug → bin → Tessera.ArchitectureTests
    ///     → core → unit → tests → repo-root</c>, hence six <c>..</c>.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public static string ResolveSrcRoot()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 7 && dir is not null; i++)
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new System.IO.DirectoryNotFoundException(
                "src/ not reachable from " + AppContext.BaseDirectory)
            : Path.Combine(dir.FullName, "src");
    }

    /// <summary>
    ///     Walk <paramref name="root"/> and yield every directory (including
    ///     root) whose direct children include one or more project dirs —
    ///     a project dir is a child directory containing a <c>*.csproj</c>
    ///     at its root (not deeper).
    /// </summary>
    public static IEnumerable<string> EnumerateFoldersWithProjectChildren(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            string[] children;
            try
            {
                children = Directory.GetDirectories(current);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            var projectChildCount = 0;
            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (name is "bin" or "obj" or "node_modules")
                {
                    continue;
                }

                if (Directory.EnumerateFiles(child, "*.csproj", SearchOption.TopDirectoryOnly).Any())
                {
                    projectChildCount++;
                }

                stack.Push(child);
            }

            if (projectChildCount > 0)
            {
                yield return current;
            }
        }
    }

    /// <summary>
    ///     Count direct-child project directories under <paramref name="folder"/>
    ///     (a child dir that holds a <c>*.csproj</c> at its own top level).
    /// </summary>
    public static int CountDirectProjectChildren(string folder)
    {
        return Directory
            .GetDirectories(folder)
            .Count(static child =>
            {
                var name = Path.GetFileName(child);
                if (name is "bin" or "obj" or "node_modules")
                {
                    return false;
                }

                return Directory.EnumerateFiles(child, "*.csproj", SearchOption.TopDirectoryOnly).Any();
            });
    }
}
