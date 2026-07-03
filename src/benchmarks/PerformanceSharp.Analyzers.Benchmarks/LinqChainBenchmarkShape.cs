// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>LINQ chain benchmark shapes.</summary>
public enum LinqChainBenchmarkShape
{
    /// <summary>LINQ Where applied after a sort (PSH1107).</summary>
    FilterBeforeSort,

    /// <summary>LINQ OrderBy applied to an already ordered sequence (PSH1108).</summary>
    UseThenBy,

    /// <summary>Consecutive LINQ Where calls (PSH1109).</summary>
    MergeConsecutiveWhere
}
