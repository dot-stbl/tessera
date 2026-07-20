namespace Tessera.Host;

/// <summary>
/// <para>
///     Empty anchor that lets cross-project tests (notably
///     <c>Tessera.ArchitectureTests</c>) reference <c>Tessera.Host.dll</c> via
///     <c>typeof(HostAssemblyMarker).Assembly</c>. The host is otherwise a
///     top-level-statements <c>Program.cs</c> with no public types of its own
///     after Phase 3's composition-root extraction.
/// </para>
/// <para>
///     Adding a real public type here would re-introduce business logic into
///     the composition root; the marker keeps Host as pure composition wiring.
/// </para>
/// </summary>
public static class HostAssemblyMarker;
