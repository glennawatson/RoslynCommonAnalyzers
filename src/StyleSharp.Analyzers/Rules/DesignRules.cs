// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the design rules (SST23xx). These are about the shape of a type's
/// surface — the contracts it signs up to (<c>IDisposable</c>, <c>IEquatable&lt;T&gt;</c>), the
/// conventions its operators and events follow, and what its members hand out.
/// </summary>
internal static class DesignRules
{
    /// <summary>SST2300 — a disposable type does not follow the disposal pattern.</summary>
    public static readonly DiagnosticDescriptor DisposePattern = Create(
        "SST2300",
        "Implement the disposal pattern correctly",
        "'{0}' implements IDisposable but {1}",
        DisposePatternDescription);

    /// <summary>SST2301 — a type that signs the equality contract can still be derived from.</summary>
    public static readonly DiagnosticDescriptor EquatableTypeShouldBeSealed = Create(
        "SST2301",
        "Types implementing IEquatable<T> should be sealed",
        "'{0}' implements IEquatable<{0}> but is not sealed; a derived type cannot honour it",
        EquatableTypeShouldBeSealedDescription);

    /// <summary>SST2302 — an operator is overloaded without its counterpart.</summary>
    public static readonly DiagnosticDescriptor InconsistentOperatorOverloads = Create(
        "SST2302",
        "Overload operators in their complete set",
        "'{0}' overloads '{1}' but not '{2}'",
        InconsistentOperatorOverloadsDescription);

    /// <summary>SST2303 — an enum is marked as flags but its members are not powers of two.</summary>
    public static readonly DiagnosticDescriptor MisusedFlagsAttribute = Create(
        "SST2303",
        "Flags enums should declare bit values",
        "'{0}' is marked [Flags] but its members are not distinct bit values",
        MisusedFlagsAttributeDescription);

    /// <summary>SST2304 — an event does not use the framework's handler shape.</summary>
    public static readonly DiagnosticDescriptor EventHandlerSignature = Create(
        "SST2304",
        "Events should use the standard handler signature",
        "'{0}' does not match the standard event signature (object sender, TEventArgs e)",
        EventHandlerSignatureDescription);

    /// <summary>SST2305 — a collection property can be replaced wholesale by a caller.</summary>
    public static readonly DiagnosticDescriptor CollectionPropertyShouldBeReadOnly = Create(
        "SST2305",
        "Collection properties should not be settable",
        "'{0}' lets a caller replace the whole collection; expose it read-only and let callers change its contents",
        CollectionPropertyShouldBeReadOnlyDescription);

    /// <summary>SST2306 — a method returns null where an empty collection is meant.</summary>
    public static readonly DiagnosticDescriptor ReturnEmptyCollectionNotNull = Create(
        "SST2306",
        "Return an empty collection instead of null",
        "'{0}' returns null instead of an empty collection; every caller must now guard",
        ReturnEmptyCollectionNotNullDescription);

    /// <summary>SST2308 — an obsolete member does not say what to use instead.</summary>
    public static readonly DiagnosticDescriptor ObsoleteWithoutExplanation = Create(
        "SST2308",
        "Obsolete attributes should explain what to use instead",
        "The [Obsolete] on '{0}' has no message; say why and what replaces it",
        ObsoleteWithoutExplanationDescription);

    /// <summary>The DisposePattern rule description.</summary>
    private const string DisposePatternDescription =
        "The disposal pattern exists because two different callers reach a type's cleanup: the code that owns it, and — if it holds "
        + "unmanaged state — the finalizer. An unsealed disposable type needs 'protected virtual void Dispose(bool)' so a derived type can "
        + "add its own cleanup without breaking the base's, and 'Dispose()' should call it and then suppress finalization. A sealed type "
        + "with no finalizer needs none of that ceremony and is not asked for it. What the rule will not accept is the half-built version: "
        + "a public 'Dispose(bool)', a 'Dispose()' that does not chain, or a finalizer that does the work the pattern says belongs in "
        + "'Dispose(bool)'.";

    /// <summary>The EquatableTypeShouldBeSealed rule description.</summary>
    private const string EquatableTypeShouldBeSealedDescription =
        "'IEquatable<T>' promises that equality is decided against exactly 'T'. A derived type breaks that promise the moment it exists: "
        + "'base.Equals(derived)' can answer true while 'derived.Equals(base)' answers false, so equality stops being symmetric and every "
        + "hash-based collection quietly misbehaves. Seal the type, or move equality to a contract a hierarchy can actually keep.";

    /// <summary>The InconsistentOperatorOverloads rule description.</summary>
    private const string InconsistentOperatorOverloadsDescription =
        "Operators come in pairs and groups, and the language enforces some of that but not all of it. A type with '==' and no 'Equals' "
        + "override has two notions of equality that can disagree; one with '<' and no '>=' leaves a caller unable to write the obvious "
        + "comparison. Overload the whole set, so the type answers every question a reader assumes it can.";

    /// <summary>The MisusedFlagsAttribute rule description.</summary>
    private const string MisusedFlagsAttributeDescription =
        "The [Flags] attribute tells everyone — the reader, 'ToString', 'HasFlag' — that the members combine with bitwise or. That only "
        + "works when each member owns its own bit. An enum marked [Flags] whose members run 0, 1, 2, 3 will report 'Three' as 'One, Two', "
        + "and a test for 'Three' will pass for a value that is neither. Either give the members powers of two, or drop the attribute.";

    /// <summary>The EventHandlerSignature rule description.</summary>
    private const string EventHandlerSignatureDescription =
        "Every tool that consumes events — the designer, the binder, the code that forwards one event to another — assumes the "
        + "'(object sender, TEventArgs e)' shape. A custom delegate works until something generic tries to handle it. Use "
        + "'EventHandler<T>' with an arguments type; it costs nothing and keeps the event usable by code you have not written yet.";

    /// <summary>The CollectionPropertyShouldBeReadOnly rule description.</summary>
    private const string CollectionPropertyShouldBeReadOnlyDescription =
        "A settable collection property hands a caller the power to swap the collection out from under everything that already holds a "
        + "reference to it — including the type's own code, and any subscription attached to the old instance. Callers almost never want "
        + "that; they want to add and remove items, which a get-only property already allows. Drop the setter.";

    /// <summary>The ReturnEmptyCollectionNotNull rule description.</summary>
    private const string ReturnEmptyCollectionNotNullDescription =
        "Returning null for 'there is nothing' forces a null check into every caller, and the one that forgets gets a "
        + "NullReferenceException instead of an empty loop. An empty collection reads the same way at every call site — foreach over it, "
        + "count it, chain from it — and 'Array.Empty<T>()' and the empty collection expression cost no allocation at all.";

    /// <summary>The ObsoleteWithoutExplanation rule description.</summary>
    private const string ObsoleteWithoutExplanationDescription =
        "'[Obsolete]' with no message tells a caller their code is wrong and nothing else. The message is the whole value of the "
        + "attribute — it is the one place the author can hand the reader the migration, at exactly the moment they need it.";

    /// <summary>Creates a Warning-severity Design descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Design", description);
}
