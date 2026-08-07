using System.Text.RegularExpressions;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Observability;

namespace Tessera.Shared.Kernel.Analysis.Errors;

/// <summary>
///     Pure span-error derivation: status check, exception extract, message
///     normalization for stable grouping keys, and collect-over-trace.
///     No I/O.
/// </summary>
public static partial class ErrorAnalysis
{
    /// <summary>OpenTelemetry span event name for exceptions.</summary>
    public const string ExceptionEventName = "exception";

    private static readonly Regex UuidPattern = UuidRegex();
    private static readonly Regex LongHexPattern = LongHexRegex();
    private static readonly Regex DigitRunPattern = DigitRunRegex();

    /// <summary>
    ///     Primary error signal: span status is <see cref="TraceStatus.Error" />.
    /// </summary>
    public static bool IsErrorSpan(Span span)
    {
        return span.Status is TraceStatus.Error;
    }

    /// <summary>
    ///     Extract type/message/stacktrace from the first span event named
    ///     <see cref="ExceptionEventName" /> using semantic-convention keys.
    ///     Returns null when no exception event is present or all attributes
    ///     are empty.
    /// </summary>
    public static ExceptionPayload? TryExtractException(Span span)
    {
        foreach (var spanEvent in span.Events)
        {
            if (!string.Equals(spanEvent.Name, ExceptionEventName, StringComparison.Ordinal))
            {
                continue;
            }

            spanEvent.Attributes.TryGetValue(SemanticConventions.ExceptionType, out var type);
            spanEvent.Attributes.TryGetValue(SemanticConventions.ExceptionMessage, out var message);
            spanEvent.Attributes.TryGetValue(SemanticConventions.ExceptionStacktrace, out var stacktrace);

            if (type is null && message is null && stacktrace is null)
            {
                return null;
            }

            return new ExceptionPayload(type, message, stacktrace);
        }

        return null;
    }

    /// <summary>
    ///     Collapse volatile tokens (UUIDs, long hex, digit runs) so messages
    ///     that differ only by ids group together in the errors inbox.
    /// </summary>
    public static string NormalizeMessage(string message)
    {
        if (message.Length == 0)
        {
            return message;
        }

        var normalized = UuidPattern.Replace(message, "{uuid}");
        normalized = LongHexPattern.Replace(normalized, "{hex}");
        normalized = DigitRunPattern.Replace(normalized, "{n}");
        return normalized;
    }

    /// <summary>
    ///     Stable MVP group key: <c>{type}|{normalizedMessage}</c>.
    ///     Null type/message become empty segments.
    /// </summary>
    public static string BuildGroupKey(string? exceptionType, string? normalizedMessage)
    {
        return string.Concat(
            exceptionType ?? string.Empty,
            "|",
            normalizedMessage ?? string.Empty);
    }

    /// <summary>
    ///     Collect one <see cref="SpanError" /> per span with
    ///     <see cref="TraceStatus.Error" />, attaching exception attributes
    ///     when an exception event is present.
    /// </summary>
    public static IReadOnlyList<SpanError> CollectErrors(TraceDetail? trace)
    {
        if (trace is not { Spans.Count: > 0 })
        {
            return [];
        }

        var errors = new List<SpanError>();
        foreach (var span in trace.Spans)
        {
            if (!IsErrorSpan(span))
            {
                continue;
            }

            var payload = TryExtractException(span);
            errors.Add(new SpanError(
                span.SpanId,
                span.Service,
                span.Operation,
                payload?.Type,
                payload?.Message,
                payload?.Stacktrace));
        }

        return errors;
    }

    /// <summary>
    ///     Count spans with <see cref="TraceStatus.Error" /> (request-view badge).
    /// </summary>
    public static int CountErroredSpans(TraceDetail? trace)
    {
        if (trace is not { Spans.Count: > 0 })
        {
            return 0;
        }

        var count = 0;
        foreach (var span in trace.Spans)
        {
            if (IsErrorSpan(span))
            {
                count++;
            }
        }

        return count;
    }

    [GeneratedRegex(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.CultureInvariant)]
    private static partial Regex UuidRegex();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex LongHexRegex();

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex DigitRunRegex();
}
