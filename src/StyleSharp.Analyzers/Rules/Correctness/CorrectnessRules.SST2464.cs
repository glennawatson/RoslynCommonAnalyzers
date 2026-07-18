// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2464 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2464 — a value-equality operator is declared on a mutable reference type.</summary>
    public static readonly DiagnosticDescriptor EqualityOperatorOnMutableClass = Create(
        "SST2464",
        "A value-equality operator should not be declared on a mutable reference type",
        "'operator ==' gives the mutable class '{0}' value equality, so an instance's hash changes when its state is mutated and it can be lost as a dictionary or hash-set key",
        EqualityOperatorOnMutableClassDescription);

    /// <summary>The EqualityOperatorOnMutableClass rule description.</summary>
    private const string EqualityOperatorOnMutableClassDescription =
        "A user-defined equality operator gives a reference type value semantics: two distinct instances compare equal when their "
        + "state matches, and a matching hash sends them to the same bucket. That is only safe while the state cannot change. When the "
        + "class still exposes a settable field or property, an instance can be mutated after it has been stored as a key in a "
        + "'Dictionary' or 'HashSet'; its hash then no longer matches the bucket it was placed in, so the key can never be found again "
        + "and lookups near it can be corrupted. Make the type immutable — readonly fields and get-only or init-only properties — or "
        + "drop the value-equality operator and let instances compare by reference. A struct, a record, and an immutable class are never "
        + "reported.";
}
