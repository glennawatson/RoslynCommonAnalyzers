// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2311 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2311 — a visible constant is compiled into its callers.</summary>
    public static readonly DiagnosticDescriptor PublicConstantField = Create(
        "SST2311",
        "Visible constants should be static readonly",
        "'{0}' is a visible const; its value is copied into every assembly that reads it, so changing it never reaches a caller already compiled",
        PublicConstantFieldDescription);

    /// <summary>The PublicConstantField rule description.</summary>
    private const string PublicConstantFieldDescription =
        "A 'const' is not read at run time — its value is copied into the call site by the compiler. When that call site is in another "
        + "assembly, the copy is taken at the moment that assembly is built, and it stays there. Ship a new version with a different value "
        + "and every caller compiled against the old one keeps the old number, silently, until it is rebuilt. A 'static readonly' field is "
        + "read from the declaring assembly at run time, so a change reaches everybody. It is not a drop-in replacement, though: the language "
        + "requires a real 'const' for an attribute argument, a 'case' label, and a default parameter value, and a value that feeds one of "
        + "those has to stay a 'const'.";
}
