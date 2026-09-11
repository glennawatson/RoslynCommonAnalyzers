// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Collection-expression advanced benchmark shapes.</summary>
public enum CollectionExpressionAdvancedBenchmarkShape
{
    /// <summary>Span-targeted stackalloc initializer.</summary>
    Stackalloc = 0,

    /// <summary>Collection-builder factory call.</summary>
    Create = 1,

    /// <summary>Short builder local sequence.</summary>
    Builder = 2,

    /// <summary>Inline array immediately materialized with LINQ.</summary>
    Fluent = 3,
}
