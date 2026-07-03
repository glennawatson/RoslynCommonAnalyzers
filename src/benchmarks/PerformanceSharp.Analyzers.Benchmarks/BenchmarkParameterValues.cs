// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Shared benchmark parameter values used by the benchmark suites.</summary>
internal static class BenchmarkParameterValues
{
    /// <summary>The smaller per-analyzer node count.</summary>
    public const int SmallNodeCount = 100;

    /// <summary>The larger per-analyzer node count.</summary>
    public const int LargeNodeCount = 3000;

    /// <summary>The smaller analyzer-throughput type count.</summary>
    public const int SmallTypeCount = 50;

    /// <summary>The larger analyzer-throughput type count.</summary>
    public const int LargeTypeCount = 500;
}
