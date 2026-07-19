using NetArchTest.Rules;
using Xunit;

namespace Tessera.ArchitectureTests;

/// <summary>
///     Folder- and project-structure rules — enforces
///     <c>module-structure-5-cap.md</c>: <c>src/shared/</c> has a hard 5-project
///     cap; <c>src/modules/</c> is currently at 4 (no cap but documented to
///     core/extended-split at 5). Per-folder source-file cap (max 3) lives
///     in <c>folder-organization.md</c> §4 and is a code-review rule rather
///     than a NetArchTest-friendly one (filesystem-based), so it's a manual
///     self-audit and not a test here.
/// </summary>
public sealed class FolderStructureTests
{
    private static readonly string SrcRoot =
        System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src");

    /// <summary>
    ///     <c>src/shared/</c> holds leaf, layer-agnostic projects. The 5-project
    ///     cap is structural: when adding a 6th, nest into
    ///     <c>shared/core/</c> + <c>shared/extended/</c> per
    ///     <c>module-structure-5-cap.md</c>. This test guards against silent
    ///     overflow.
    /// </summary>
    [Fact]
    public void SharedProjects_DoNotExceedFiveProjectCap()
    {
        var sharedDir = System.IO.Path.Combine(SrcRoot, "shared");
        var projectFiles = System.IO.Directory
            .EnumerateFiles(sharedDir, "Tessera.Shared.*.csproj", System.IO.SearchOption.TopDirectoryOnly)
            .ToArray();

        Assert.True(projectFiles.Length <= 5,
            $"src/shared/ has {projectFiles.Length} projects (cap is 5). " +
            $"Projects: {string.Join(", ", projectFiles.Select(System.IO.Path.GetFileName))}. " +
            $"Per module-structure-5-cap.md, nest a new project into shared/core/ or shared/extended/.");

        // Sanity: also assert the cap is enforced AFTER adding new projects
        // by requiring the exact expected count for MVP-01 (5 currently).
        Assert.Equal(5, projectFiles.Length);
    }

    /// <summary>
    ///     <c>src/modules/</c> is currently at 4 modules (Traces, Logs,
    ///     Discovery, Health). The rule from <c>module-structure-5-cap.md</c>
    ///     says: when at 5, nest into <c>modules/core/</c> + <c>modules/extended/</c>;
    ///     for now this test asserts the current count is below 5 so any
    ///     accidental addition is a deliberate commit that also updates
    ///     <c>modules.md</c>.
    /// </summary>
    [Fact]
    public void ModuleProjects_StayBelowFiveProjectCap()
    {
        var modulesDir = System.IO.Path.Combine(SrcRoot, "modules");
        var projectFiles = System.IO.Directory
            .EnumerateFiles(modulesDir, "Tessera.Modules.*.csproj", System.IO.SearchOption.TopDirectoryOnly)
            .ToArray();

        Assert.True(projectFiles.Length < 5,
            $"src/modules/ has {projectFiles.Length} modules (cap is below 5 per module-structure-5-cap.md). " +
            $"Projects: {string.Join(", ", projectFiles.Select(System.IO.Path.GetFileName))}. " +
            $"At 5, nest into modules/core/ + modules/extended/.");
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
        var providersDir = System.IO.Path.Combine(SrcRoot, "providers");
        var projectFiles = System.IO.Directory
            .EnumerateDirectories(providersDir)
            .Select(System.IO.Path.GetFileName)
            .Where(name => name!.StartsWith("Tessera.Providers.", StringComparison.Ordinal))
            .ToArray();

        Assert.All(projectFiles, name =>
        {
            // Tessera.Providers.X where X is one concrete backend
            var backend = name!.Substring("Tessera.Providers.".Length);
            Assert.Matches(@"^[A-Z][A-Za-z0-9]+$", backend);
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
        var sharedDir = System.IO.Path.Combine(SrcRoot, "shared", "Tessera.Host");
        var modulesDir = System.IO.Path.Combine(SrcRoot, "modules", "Tessera.Host");
        Assert.False(System.IO.Directory.Exists(sharedDir), "Tessera.Host must not live in src/shared/");
        Assert.False(System.IO.Directory.Exists(modulesDir), "Tessera.Host must not live in src/modules/");
        Assert.True(System.IO.Directory.Exists(System.IO.Path.Combine(SrcRoot, "host", "Tessera.Host")),
            "Tessera.Host must live in src/host/.");
    }
}
