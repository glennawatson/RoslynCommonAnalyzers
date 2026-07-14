// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2404 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2404 — an iterator's argument checks do not run until it is enumerated.</summary>
    public static readonly DiagnosticDescriptor IteratorValidatesTooLate = Create(
        "SST2404",
        "Validate an iterator's arguments before it starts yielding",
        "'{0}' validates its arguments inside an iterator, so the check does not run until the caller enumerates",
        IteratorValidatesTooLateDescription);

    /// <summary>The IteratorValidatesTooLate rule description.</summary>
    private const string IteratorValidatesTooLateDescription =
        "A method containing 'yield' does not run when it is called — the body starts only on the first MoveNext. So the argument guard at "
        + "the top of it does not fire at the call site, where the caller's stack still says who passed the bad value, but later, from "
        + "inside whatever foreach eventually consumed the sequence. Split the method: a normal method that validates and then returns the "
        + "iterator from a private local function.";
}
