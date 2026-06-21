// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Modern-syntax value benchmark shapes.</summary>
public enum ModernSyntaxValueBenchmarkShape
{
    /// <summary>Interpolation with a redundant ToString call.</summary>
    Interpolation,

    /// <summary>Ignored expression value.</summary>
    IgnoredValue,

    /// <summary>Local value overwritten before use.</summary>
    OverwrittenValue,

    /// <summary>Null fallback assignment.</summary>
    CoalesceAssignment,

    /// <summary>Anonymous object that can be a tuple.</summary>
    AnonymousTuple,

    /// <summary>Foreach loop with hidden element cast.</summary>
    ForeachCast,

    /// <summary>Cast with a hidden inner conversion.</summary>
    HiddenCast,

    /// <summary>Post-assignment null fallback.</summary>
    FoldNullCheck,

    /// <summary>Delegate local that can be a local function.</summary>
    LocalFunction,

    /// <summary>LINQ Where followed by a predicate terminal.</summary>
    WhereTerminal,

    /// <summary>LINQ type check followed by Cast.</summary>
    TypeFilter,

    /// <summary>Broad object pattern used as a null check.</summary>
    NullPattern,

    /// <summary>Concrete generic arguments inside nameof.</summary>
    UnboundGenericName,

    /// <summary>LINQ call in hot-path code.</summary>
    HotPathLinq
}
