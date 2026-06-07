// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace TraceFocus;

/// <summary>Accumulates the filtered frame, leaf, and stack totals across all evented profiles.</summary>
internal sealed class TraceAggregation
{
    /// <summary>Scratch buffer reused while filtering one sampled stack.</summary>
    private readonly List<string> _filteredFrames = [];

    /// <summary>Initializes a new instance of the <see cref="TraceAggregation"/> class.</summary>
    public TraceAggregation()
    {
        FrameTotals = new(StringComparer.Ordinal);
        LeafTotals = new(StringComparer.Ordinal);
        StackTotals = new(StringComparer.Ordinal);
        Unit = "samples";
    }

    /// <summary>Gets inclusive totals for all retained frames.</summary>
    public Dictionary<string, double> FrameTotals { get; }

    /// <summary>Gets totals for retained leaf frames.</summary>
    public Dictionary<string, double> LeafTotals { get; }

    /// <summary>Gets totals for retained stacks.</summary>
    public Dictionary<string, double> StackTotals { get; }

    /// <summary>Gets the number of evented profiles processed.</summary>
    public int EventedProfiles { get; private set; }

    /// <summary>Gets the number of profiles that contributed focused analyzer-visible samples.</summary>
    public int MatchedProfiles { get; private set; }

    /// <summary>Gets the total retained analyzer-visible duration.</summary>
    public double FocusedDuration { get; private set; }

    /// <summary>Gets the total sampled duration across evented profiles.</summary>
    public double TotalDuration { get; private set; }

    /// <summary>Gets the units used by the sampled trace values.</summary>
    public string Unit { get; private set; }

    /// <summary>Registers an evented profile and captures its reported unit.</summary>
    /// <param name="unit">The profile unit.</param>
    public void RegisterEventedProfile(string unit)
    {
        EventedProfiles++;
        Unit = unit;
    }

    /// <summary>Registers that a profile contributed at least one focused sample.</summary>
    public void RegisterMatchedProfile() => MatchedProfiles++;

    /// <summary>Processes one sampled interval from an evented profile.</summary>
    /// <param name="duration">The interval duration.</param>
    /// <param name="stack">The active frame stack.</param>
    /// <param name="frames">The shared frame table.</param>
    /// <param name="filters">The active frame filters.</param>
    /// <returns><see langword="true"/> when the interval contributed focused analyzer-visible data.</returns>
    public bool ProcessInterval(double duration, List<int> stack, List<FrameDefinition> frames, FrameFilters filters)
    {
        if (duration <= 0 || stack.Count == 0)
        {
            return false;
        }

        TotalDuration += duration;
        if (!TryBuildFilteredStack(stack, frames, filters, out var matchedInclude))
        {
            return false;
        }

        if (!matchedInclude)
        {
            return false;
        }

        FocusedDuration += duration;
        AccumulateFrameTotals(duration);
        AccumulateLeafTotal(duration);
        AccumulateStackTotal(duration);
        return true;
    }

    /// <summary>Adds a sampled duration to the supplied totals table.</summary>
    /// <param name="totals">The totals table.</param>
    /// <param name="key">The frame or stack key.</param>
    /// <param name="duration">The sampled duration to add.</param>
    private static void AddDuration(Dictionary<string, double> totals, string key, double duration) =>
        totals[key] = totals.TryGetValue(key, out var existing) ? existing + duration : duration;

    /// <summary>Builds the printable stack key used in the aggregated stack table.</summary>
    /// <param name="frames">The retained stack frames.</param>
    /// <returns>The printable multi-line stack key.</returns>
    private static string BuildStackKey(List<string> frames)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < frames.Count; index++)
        {
            if (index > 0)
            {
                builder.Append('\n').Append("  -> ");
            }

            builder.Append(frames[index]);
        }

        return builder.ToString();
    }

    /// <summary>Normalizes a raw frame name by dropping the module prefix and parameter list.</summary>
    /// <param name="frameName">The raw frame name.</param>
    /// <returns>The normalized frame name.</returns>
    private static string NormalizeFrameName(string frameName)
    {
        var separatorIndex = frameName.IndexOf('!');
        var normalized = separatorIndex >= 0 ? frameName[(separatorIndex + 1)..] : frameName;
        var parameterIndex = normalized.IndexOf('(');
        return parameterIndex >= 0 ? normalized[..parameterIndex] : normalized;
    }

    /// <summary>Adds the interval duration to each retained inclusive frame total.</summary>
    /// <param name="duration">The sampled duration.</param>
    private void AccumulateFrameTotals(double duration)
    {
        for (var index = 0; index < _filteredFrames.Count; index++)
        {
            AddDuration(FrameTotals, _filteredFrames[index], duration);
        }
    }

    /// <summary>Adds the interval duration to the retained leaf frame total.</summary>
    /// <param name="duration">The sampled duration.</param>
    private void AccumulateLeafTotal(double duration) => AddDuration(LeafTotals, _filteredFrames[^1], duration);

    /// <summary>Adds the interval duration to the retained stack total.</summary>
    /// <param name="duration">The sampled duration.</param>
    private void AccumulateStackTotal(double duration) => AddDuration(StackTotals, BuildStackKey(_filteredFrames), duration);

    /// <summary>Builds the retained analyzer-visible frame stack for one interval.</summary>
    /// <param name="stack">The active frame stack.</param>
    /// <param name="frames">The shared frame table.</param>
    /// <param name="filters">The active frame filters.</param>
    /// <param name="matchedInclude">Whether any retained frame matched an include filter.</param>
    /// <returns><see langword="true"/> when at least one frame remained after filtering.</returns>
    private bool TryBuildFilteredStack(List<int> stack, List<FrameDefinition> frames, FrameFilters filters, out bool matchedInclude)
    {
        _filteredFrames.Clear();
        matchedInclude = !filters.HasIncludeTerms;

        for (var index = 0; index < stack.Count; index++)
        {
            var frameName = frames[stack[index]].Name;
            if (filters.MatchesExclude(frameName))
            {
                continue;
            }

            if (filters.MatchesInclude(frameName))
            {
                matchedInclude = true;
            }

            var normalized = NormalizeFrameName(frameName);
            if (_filteredFrames.Count > 0 && string.Equals(_filteredFrames[^1], normalized, StringComparison.Ordinal))
            {
                continue;
            }

            _filteredFrames.Add(normalized);
        }

        return _filteredFrames.Count > 0;
    }
}
