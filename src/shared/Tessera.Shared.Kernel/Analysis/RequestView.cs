using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Domain.Traces;

namespace Tessera.Shared.Kernel.Analysis;

/// <summary>
///     Degradable request view: optional span tree + correlated logs + mode
///     + timeline markers. Built by pure analysis helpers; modules fetch
///     providers and call assembly.
/// </summary>
/// <param name="Trace">Full trace with spans, or null when only logs exist.</param>
/// <param name="Logs">Correlated log entries for the trace id (may be empty).</param>
/// <param name="Mode">Degradation classification for FE banners.</param>
/// <param name="Markers">Timeline markers derived from <paramref name="Logs" />.</param>
public sealed record RequestView(
    TraceDetail? Trace,
    IReadOnlyList<LogEntry> Logs,
    RequestViewMode Mode,
    IReadOnlyList<LogMarker> Markers);
