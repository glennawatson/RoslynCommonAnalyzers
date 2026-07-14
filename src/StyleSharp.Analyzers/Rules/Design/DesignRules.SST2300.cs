// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2300 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2300 — a disposable type does not follow the disposal pattern.</summary>
    public static readonly DiagnosticDescriptor DisposePattern = Create(
        "SST2300",
        "Implement the disposal pattern correctly",
        "'{0}' implements IDisposable but {1}",
        DisposePatternDescription);

    /// <summary>The DisposePattern rule description.</summary>
    private const string DisposePatternDescription =
        "The disposal pattern exists because two different callers reach a type's cleanup: the code that owns it, and — if it holds "
        + "unmanaged state — the finalizer. An unsealed disposable type needs 'protected virtual void Dispose(bool)' so a derived type can "
        + "add its own cleanup without breaking the base's, and 'Dispose()' should call it and then suppress finalization. A sealed type "
        + "with no finalizer needs none of that ceremony and is not asked for it. What the rule will not accept is the half-built version: "
        + "a public 'Dispose(bool)', a 'Dispose()' that does not chain, or a finalizer that does the work the pattern says belongs in "
        + "'Dispose(bool)'.";
}
