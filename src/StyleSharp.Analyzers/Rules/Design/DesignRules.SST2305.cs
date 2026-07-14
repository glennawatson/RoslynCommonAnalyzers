// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2305 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2305 — a collection property can be replaced wholesale by a caller.</summary>
    public static readonly DiagnosticDescriptor CollectionPropertyShouldBeReadOnly = Create(
        "SST2305",
        "Collection properties should not be settable",
        "'{0}' lets a caller replace the whole collection; expose it read-only and let callers change its contents",
        CollectionPropertyShouldBeReadOnlyDescription);

    /// <summary>The CollectionPropertyShouldBeReadOnly rule description.</summary>
    private const string CollectionPropertyShouldBeReadOnlyDescription =
        "A settable collection property hands a caller the power to swap the collection out from under everything that already holds a "
        + "reference to it — including the type's own code, and any subscription attached to the old instance. Callers almost never want "
        + "that; they want to add and remove items, which a get-only property already allows. Drop the setter.";
}
