// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Modern-syntax value benchmark shapes.</summary>
public enum ModernSyntaxValueBenchmarkShape
{
    /// <summary>Interpolation with a redundant ToString call.</summary>
    Interpolation = 0,

    /// <summary>Ignored expression value.</summary>
    IgnoredValue = 1,

    /// <summary>Local value overwritten before use.</summary>
    OverwrittenValue = 2,

    /// <summary>Null fallback assignment.</summary>
    CoalesceAssignment = 3,

    /// <summary>Anonymous object that can be a tuple.</summary>
    AnonymousTuple = 4,

    /// <summary>Foreach loop with hidden element cast.</summary>
    ForeachCast = 5,

    /// <summary>Cast with a hidden inner conversion.</summary>
    HiddenCast = 6,

    /// <summary>Post-assignment null fallback.</summary>
    FoldNullCheck = 7,

    /// <summary>Delegate local that can be a local function.</summary>
    LocalFunction = 8,

    /// <summary>Broad object pattern used as a null check.</summary>
    NullPattern = 9,

    /// <summary>Concrete generic arguments inside nameof.</summary>
    UnboundGenericName = 10,

    /// <summary>Postfix step discarded by the enclosing return.</summary>
    ReturnedIncrement = 11,

    /// <summary>Local assigned its own postfix step.</summary>
    SelfAssignedIncrement = 12,
}
