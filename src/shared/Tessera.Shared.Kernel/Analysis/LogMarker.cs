using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Shared.Kernel.Analysis;

/// <summary>
///     Timeline marker for a log entry overlaid on a request view.
///     <see cref="OffsetMs" /> is relative to the matched span's
///     <c>StartTime</c> when <see cref="SpanId" /> is set and that span
///     exists on the trace; otherwise relative to the trace
///     <c>StartTime</c>. When there is no trace (logs-only), offsets are
///     relative to the earliest log timestamp in the batch (so the first
///     log sits at 0).
/// </summary>
/// <param name="SpanId">
///     Span this log is pinned to, or <c>null</c> for a trace-level marker
///     (no span_id on the log, or span not present on the trace tree).
/// </param>
/// <param name="OffsetMs">Milliseconds from the anchor start (see summary).</param>
/// <param name="Level">Log severity.</param>
/// <param name="Message">Message preview (full message; FE may truncate).</param>
/// <param name="TimestampUnixMs">Absolute log timestamp (unix ms UTC).</param>
public sealed record LogMarker(
    SpanId? SpanId,
    long OffsetMs,
    LogLevel Level,
    string Message,
    long TimestampUnixMs);
