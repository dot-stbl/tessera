using Tessera.Modules.Traces.Contracts.Errors;
using Tessera.Shared.Kernel.Analysis.Errors;
using Tessera.Shared.Kernel.Domain.Traces;

namespace Tessera.Modules.Traces.Services.Errors;

/// <summary>
///     Pure filter + group helpers for the errors inbox.
/// </summary>
internal static class ErrorsInboxGrouping
{
    /// <summary>
    ///     Keep only summaries whose root status is <see cref="TraceStatus.Error" />
    ///     (MVP: root Error only — bounds cost; inner-error-only is P7.1).
    /// </summary>
    public static IReadOnlyList<TraceSummary> FilterErrorSummaries(
        IReadOnlyList<TraceSummary> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var filtered = new List<TraceSummary>();
        foreach (var item in items)
        {
            if (item.Status is TraceStatus.Error)
            {
                filtered.Add(item);
            }
        }

        return filtered;
    }

    /// <summary>
    ///     Collect span errors across details, group by
    ///     <see cref="ErrorAnalysis.BuildGroupKey" />, order by count desc.
    /// </summary>
    public static IReadOnlyList<ErrorGroupSummary> GroupErrors(
        IReadOnlyList<TraceDetail> details,
        int maxSampleTraceIds)
    {
        if (details.Count == 0)
        {
            return [];
        }

        var buckets = new Dictionary<string, GroupBucket>(StringComparer.Ordinal);
        foreach (var detail in details)
        {
            foreach (var error in ErrorAnalysis.CollectErrors(detail))
            {
                var normalized = ErrorAnalysis.NormalizeMessage(error.ExceptionMessage ?? string.Empty);
                var key = ErrorAnalysis.BuildGroupKey(error.ExceptionType, normalized);
                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new GroupBucket(key, error.ExceptionType, error.ExceptionMessage);
                    buckets[key] = bucket;
                }

                bucket.OccurrenceCount++;
                if (bucket.SampleTraceIds.Count < maxSampleTraceIds
                    && !bucket.SampleTraceIds.Contains(detail.TraceId.Value, StringComparer.Ordinal))
                {
                    bucket.SampleTraceIds.Add(detail.TraceId.Value);
                }
            }
        }

        var groups = new List<ErrorGroupSummary>(buckets.Count);
        foreach (var bucket in buckets.Values)
        {
            groups.Add(new ErrorGroupSummary
            {
                Key = bucket.Key,
                ExceptionType = bucket.ExceptionType,
                Message = bucket.Message,
                Count = bucket.OccurrenceCount,
                SampleTraceIds = bucket.SampleTraceIds,
            });
        }

        groups.Sort(static (left, right) => right.Count.CompareTo(left.Count));
        return groups;
    }

    private sealed class GroupBucket(string key, string? exceptionType, string? message)
    {
        public string Key { get; } = key;

        public string? ExceptionType { get; } = exceptionType;

        public string? Message { get; } = message;

        public int OccurrenceCount { get; set; }

        public List<string> SampleTraceIds { get; } = [];
    }
}
