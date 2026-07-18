// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2481 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2481 — a hash override folds the base object identity hash into a value hash.</summary>
    public static readonly DiagnosticDescriptor IdentityHashInValueHash = Create(
        "SST2481",
        "GetHashCode should not fold in the base object identity hash",
        "'base.GetHashCode()' is the object identity hash; folding it into a value hash makes two value-equal instances hash differently and lose each other in a hash-based collection",
        IdentityHashInValueHashDescription);

    /// <summary>The IdentityHashInValueHash rule description.</summary>
    private const string IdentityHashInValueHashDescription =
        "When a type derives straight from 'object', 'base.GetHashCode()' is the runtime identity hash — a value fixed to the reference, "
        + "unrelated to the fields the object carries. Folding it into a hash built from those fields ties the whole hash to identity: two "
        + "instances that 'Equals' reports as equal produce different hashes, so an object placed in a dictionary or hash set under one "
        + "instance can never be found under an equal one, breaking the collection contract the override exists to honor. Combine only the "
        + "fields that 'Equals' compares — through 'HashCode.Combine(...)' or an equivalent — and drop the 'base.GetHashCode()' term. A base "
        + "call that binds to a base class's own value hash is a legitimate chain and is not reported, and a 'GetHashCode' that returns "
        + "'base.GetHashCode()' outright rather than mixing it into a value hash is left to the reference-delegation rule. Structs are not "
        + "reported: there 'base.GetHashCode()' is the reflection-based value-type hash, which is deterministic on the field values, so "
        + "mixing it is a performance concern rather than a lookup bug.";
}
