// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace TraceFocus;

/// <summary>Transforms raw evented speedscope profiles into aggregated retained-frame totals.</summary>
internal static class TraceProfileProcessor
{
    /// <summary>Processes all evented profiles in a speedscope document.</summary>
    /// <param name="document">The speedscope document.</param>
    /// <param name="filters">The active frame filters.</param>
    /// <returns>The aggregated retained-frame totals.</returns>
    public static TraceAggregation Process(SpeedscopeDocument document, FrameFilters filters)
    {
        var aggregation = new TraceAggregation();
        foreach (var profile in document.Profiles)
        {
            if (!profile.IsEvented)
            {
                continue;
            }

            aggregation.RegisterEventedProfile(profile.Unit);
            if (ProcessProfile(profile, document.Shared.Frames, filters, aggregation))
            {
                aggregation.RegisterMatchedProfile();
            }
        }

        return aggregation;
    }

    /// <summary>Applies one open or close event to the active frame stack.</summary>
    /// <param name="stack">The active frame stack.</param>
    /// <param name="frameEvent">The event to apply.</param>
    private static void ApplyEvent(List<int> stack, FrameEvent frameEvent)
    {
        if (frameEvent.IsOpen)
        {
            stack.Add(frameEvent.Frame);
            return;
        }

        if (!frameEvent.IsClose || stack.Count == 0)
        {
            return;
        }

        stack.RemoveAt(stack.Count - 1);
    }

    /// <summary>Processes one evented profile and contributes its retained samples to the aggregation.</summary>
    /// <param name="profile">The evented profile.</param>
    /// <param name="frames">The shared frame table.</param>
    /// <param name="filters">The active frame filters.</param>
    /// <param name="aggregation">The target aggregation.</param>
    /// <returns><see langword="true"/> when the profile contributed focused analyzer-visible data.</returns>
    private static bool ProcessProfile(EventedProfile profile, List<FrameDefinition> frames, FrameFilters filters, TraceAggregation aggregation)
    {
        var stack = new List<int>();
        var lastAt = profile.StartValue;
        var matched = false;

        foreach (var frameEvent in profile.Events)
        {
            matched |= aggregation.ProcessInterval(frameEvent.At - lastAt, stack, frames, filters);
            ApplyEvent(stack, frameEvent);
            lastAt = frameEvent.At;
        }

        return aggregation.ProcessInterval(profile.EndValue - lastAt, stack, frames, filters) || matched;
    }
}
