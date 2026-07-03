// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>LINQ usage benchmark shapes.</summary>
public enum LinqUsageBenchmarkShape
{
    /// <summary>LINQ Where followed by a predicate terminal.</summary>
    WhereTerminal,

    /// <summary>LINQ type check followed by Cast.</summary>
    TypeFilter,

    /// <summary>LINQ call in hot-path code.</summary>
    HotPathLinq
}
