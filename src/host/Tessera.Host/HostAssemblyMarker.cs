namespace Tessera.Host;

/// <summary>
///     Empty anchor that lets cross-project tests (notably
///     <c>Tessera.ArchitectureTests</c>) reference <c>Tessera.Host.dll</c> via
///     <c>typeof(HostAssemblyMarker).Assembly</c>. The host is otherwise a
///     top-level-statements <c>Program.cs</c> with no public types of its own
///     after Phase 3's composition-root extraction.
///
///     Adding a real public type here would re-introduce business logic into
///     the composition root; the marker keeps Host as pure composition wiring.
/// </summary>
public static class HostAssemblyMarker
{
}

/// <summary>
///     Public partial <see cref="Program" /> type required by
///     <c>Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory&lt;TEntryPoint&gt;</c>
///     to reach the host's entry point. The top-level statements in
///     <c>Program.cs</c> compile into another partial of this class,
///     so the test factory can invoke the host in-process without
///     exposing any business logic. Co-located with
///     <see cref="HostAssemblyMarker" /> because both are empty public
///     anchors that exist solely to make the assembly externally
///     introspectable.
/// </summary>
public partial class Program
{
}
