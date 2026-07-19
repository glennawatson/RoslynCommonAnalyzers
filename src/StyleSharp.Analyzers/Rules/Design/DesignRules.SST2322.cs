// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2322 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2322 — a readonly field hands out a mutable collection callers can still change.</summary>
    public static readonly DiagnosticDescriptor ReadonlyMutableCollectionField = Create(
        "SST2322",
        "Readonly fields should not expose a mutable collection",
        "'{0}' is readonly, but its collection is mutable, so any caller can still add, remove, or clear its items; expose a read-only view or hand back a copy",
        ReadonlyMutableCollectionFieldDescription);

    /// <summary>The ReadonlyMutableCollectionField rule description.</summary>
    private const string ReadonlyMutableCollectionFieldDescription =
        "'readonly' freezes the reference, not the contents. A visible 'readonly' field whose type is a mutable collection — a 'List<T>', a "
        + "'Dictionary<,>', a 'HashSet<T>', a 'Collection<T>', an array — looks settled but is not: the field can never be reassigned, and yet "
        + "any caller that reads it can add, remove, or clear its items, reaching straight past the type that owns the data. Expose the "
        + "collection through a read-only type such as 'IReadOnlyList<T>' or 'IReadOnlyCollection<T>', or hand back a copy, so a reader can "
        + "look without rewriting. A private field keeps the collection under the type's own control and is left alone, as is a static one, "
        + "which is a separate concern.";
}
