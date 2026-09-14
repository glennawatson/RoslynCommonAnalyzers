// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The member's start and the conditional nesting depth reached before it.</summary>
/// <param name="Start">The member's start position.</param>
internal record struct ConditionalRegionScan(int Start)
{
    /// <summary>Gets or sets the number of conditional regions open at the current position.</summary>
    public int Depth { get; set; }
}
