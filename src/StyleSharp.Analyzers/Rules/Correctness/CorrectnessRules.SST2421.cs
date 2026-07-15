// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2421 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2421 — a write goes through a readonly field of an unconstrained type parameter.</summary>
    public static readonly DiagnosticDescriptor ReadonlyGenericFieldWrite = Create(
        "SST2421",
        "A readonly field of an unconstrained type parameter should not be mutated",
        "'{0}' is a readonly field of an unconstrained type parameter; this write lands on a copy and is lost",
        ReadonlyGenericFieldWriteDescription);

    /// <summary>The ReadonlyGenericFieldWrite rule description.</summary>
    private const string ReadonlyGenericFieldWriteDescription =
        "A readonly field whose type is a type parameter with no reference-type constraint is written through — a property or field is "
        + "assigned on it, or a mutating member is called on it. When the type argument is a struct, reading a readonly field yields a "
        + "defensive copy, so the write changes the copy and is silently discarded; the field keeps its old value. Nothing warns "
        + "because the same code is correct when the type argument is a class. The three repairs each mean something different: "
        + "constrain the parameter to a reference type if the field is shared state, drop 'readonly' if the value is meant to change "
        + "in place, or give the type a value-returning API so the update produces a new value instead of mutating one.";
}
