// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Collection native-method benchmark shapes.</summary>
public enum CollectionNativeMethodBenchmarkShape
{
    /// <summary>List FirstOrDefault predicate replaced by Find (PSH1110).</summary>
    ListPredicate,

    /// <summary>Array Any predicate replaced by the static Array.Exists helper (PSH1110).</summary>
    ArrayPredicate,

    /// <summary>Any equality predicate replaced by Contains (PSH1111).</summary>
    Membership
}
