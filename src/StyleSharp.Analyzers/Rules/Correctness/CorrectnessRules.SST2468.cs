// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2468 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2468 — a classic partial method is declared but never implemented, so the compiler discards it.</summary>
    public static readonly DiagnosticDescriptor UnimplementedPartialMethod = Create(
        "SST2468",
        "A partial method with no implementation is silently discarded",
        "The partial method '{0}' has no implementing declaration, so the compiler removes it and every call to it, and the calls never run",
        UnimplementedPartialMethodDescription);

    /// <summary>The UnimplementedPartialMethod rule description.</summary>
    private const string UnimplementedPartialMethodDescription =
        "A classic partial method — a 'partial void' declaration with no accessibility modifier, no return value, and no 'out' "
        + "parameters — is not required to have a body. When no implementing declaration exists anywhere in the compilation, the "
        + "compiler silently deletes the declaration and rewrites away every call to it, emitting no error and no warning. A hook that "
        + "was declared and then never implemented is almost always a mistake: the author expected the call to run something, but "
        + "because the body was never written each call is erased and does nothing. Only the classic form is reported. A partial method "
        + "that carries an accessibility modifier, returns a value, or takes an 'out' parameter must be implemented, and the compiler "
        + "already reports the missing body as an error, so it is left to the compiler.";
}
