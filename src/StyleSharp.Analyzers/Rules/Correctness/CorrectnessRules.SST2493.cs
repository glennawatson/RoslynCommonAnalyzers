// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2493 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2493 — <c>== null</c> on an unconstrained type parameter behaves surprisingly for value types.</summary>
    public static readonly DiagnosticDescriptor NullComparisonOnUnconstrainedGeneric = Create(
        "SST2493",
        "Compare an unconstrained type parameter with 'is null', not '== null'",
        "'{0}' is an unconstrained type parameter; '{1}' compares by reference and is always false for value-type substitutions — use '{2}' instead",
        NullComparisonOnUnconstrainedGenericDescription);

    /// <summary>The NullComparisonOnUnconstrainedGeneric rule description.</summary>
    private const string NullComparisonOnUnconstrainedGenericDescription =
        "A value of an unconstrained type parameter is compared to null with '==' or '!='. Because the type parameter has "
        + "no 'class', 'struct', or 'notnull' constraint, it can be substituted with a value type, and there the operator "
        + "does not mean what it reads: the compiler resolves it to a reference comparison, which is always false for a "
        + "non-nullable value type however the value was produced. The check silently answers the wrong question for half "
        + "the types it accepts. The constant pattern 'is null' / 'is not null' is correct for every substitution — it "
        + "compares against the default for value types and against a null reference for reference types — and never boxes.";
}
