// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2302 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2302 — an operator is overloaded without its counterpart.</summary>
    public static readonly DiagnosticDescriptor InconsistentOperatorOverloads = Create(
        "SST2302",
        "Overload operators in their complete set",
        "'{0}' overloads '{1}' but not '{2}'",
        InconsistentOperatorOverloadsDescription);

    /// <summary>The InconsistentOperatorOverloads rule description.</summary>
    private const string InconsistentOperatorOverloadsDescription =
        "Operators come in pairs and groups, and the language enforces some of that but not all of it. A type with '==' and no 'Equals' "
        + "override has two notions of equality that can disagree; one with '<' and no '>=' leaves a caller unable to write the obvious "
        + "comparison. Overload the whole set, so the type answers every question a reader assumes it can.";
}
