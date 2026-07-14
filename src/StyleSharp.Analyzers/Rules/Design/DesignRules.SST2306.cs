// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2306 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2306 — a method returns null where an empty collection is meant.</summary>
    public static readonly DiagnosticDescriptor ReturnEmptyCollectionNotNull = Create(
        "SST2306",
        "Return an empty collection instead of null",
        "'{0}' returns null instead of an empty collection; every caller must now guard",
        ReturnEmptyCollectionNotNullDescription);

    /// <summary>The ReturnEmptyCollectionNotNull rule description.</summary>
    private const string ReturnEmptyCollectionNotNullDescription =
        "Returning null for 'there is nothing' forces a null check into every caller, and the one that forgets gets a "
        + "NullReferenceException instead of an empty loop. An empty collection reads the same way at every call site — foreach over it, "
        + "count it, chain from it — and 'Array.Empty<T>()' and the empty collection expression cost no allocation at all.";
}
