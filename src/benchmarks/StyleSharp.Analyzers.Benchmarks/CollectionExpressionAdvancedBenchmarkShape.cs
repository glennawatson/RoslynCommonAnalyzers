// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Collection-expression advanced benchmark shapes.</summary>
public enum CollectionExpressionAdvancedBenchmarkShape
{
    /// <summary>Span-targeted stackalloc initializer.</summary>
    Stackalloc,

    /// <summary>Collection-builder factory call.</summary>
    Create,

    /// <summary>Short builder local sequence.</summary>
    Builder,

    /// <summary>Inline array immediately materialized with LINQ.</summary>
    Fluent
}
