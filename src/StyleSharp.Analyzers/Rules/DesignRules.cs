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

    /// <summary>SST2307 — a generic method has a type parameter no argument can pin down.</summary>
    public static readonly DiagnosticDescriptor InferableTypeParameter = Create(
        "SST2307",
        "Generic method type parameters should be inferable from the parameters",
        "'{0}' on '{1}' appears in no parameter, so every caller has to name it",
        InferableTypeParameterDescription);

    /// <summary>SST2308 — an obsolete member does not say what to use instead.</summary>
    public static readonly DiagnosticDescriptor ObsoleteWithoutExplanation = Create(
        "SST2308",
        "Obsolete attributes should explain what to use instead",
        "The [Obsolete] on '{0}' has no message; say why and what replaces it",
        ObsoleteWithoutExplanationDescription);

    /// <summary>SST2309 — an externally visible member hands a caller a default value to bake in.</summary>
    public static readonly DiagnosticDescriptor OptionalParameter = Create(
        "SST2309",
        "Use an overload instead of an optional parameter",
        "'{0}' on '{1}' is optional, so every caller that omits it compiles the default into itself",
        OptionalParameterDescription);

    /// <summary>SST2310 — deprecated code is still here, and is a standing reminder to remove it.</summary>
    public static readonly DiagnosticDescriptor ObsoleteCodeShouldBeRemoved = Create(
        "SST2310",
        "Deprecated code should be removed",
        "'{0}' is deprecated; remove it once its last caller is gone",
        ObsoleteCodeShouldBeRemovedDescription);

    /// <summary>SST2311 — a visible constant is compiled into its callers.</summary>
    public static readonly DiagnosticDescriptor PublicConstantField = Create(
        "SST2311",
        "Visible constants should be static readonly",
        "'{0}' is a visible const; its value is copied into every assembly that reads it, so changing it never reaches a caller already compiled",
        PublicConstantFieldDescription);

    /// <summary>SST2312 — a type is declared outside any namespace.</summary>
    public static readonly DiagnosticDescriptor TypeInGlobalNamespace = Create(
        "SST2312",
        "Types should be declared in a named namespace",
        "Move '{0}' into a named namespace",
        TypeInGlobalNamespaceDescription);

    /// <summary>SST2313 — an enum is stored as a type the project does not allow.</summary>
    public static readonly DiagnosticDescriptor EnumStorageShouldBeAllowed = Create(
        "SST2313",
        "Enums should use an allowed storage type",
        "'{0}' is stored as '{1}'; the allowed enum storage is '{2}'",
        EnumStorageShouldBeAllowedDescription);

    /// <summary>SST2314 — an obsolete member explains itself but cannot be suppressed on its own.</summary>
    public static readonly DiagnosticDescriptor ObsoleteWithoutDiagnosticId = Create(
        "SST2314",
        "Obsolete attributes should carry a DiagnosticId",
        "The [Obsolete] on '{0}' has a message but no DiagnosticId, so every caller gets the same CS0618",
        ObsoleteWithoutDiagnosticIdDescription);

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

    /// <summary>The OptionalParameter rule description.</summary>
    private const string OptionalParameterDescription =
        "A default value is not stored in the method — it is copied into every call site that omits the argument, at the moment that "
        + "caller is compiled. Change the default in a later version and every assembly already built against the old one keeps passing the "
        + "old value, silently. An overload puts the default in one place, inside the method, where changing it reaches everybody.";

    /// <summary>The InferableTypeParameter rule description.</summary>
    private const string InferableTypeParameterDescription =
        "C# infers a method's type arguments from the arguments it is given, and from nothing else — not the return type, not the "
        + "constraints. A type parameter that appears in no parameter therefore cannot be inferred, and every single call site has to spell "
        + "the type out. Take the type parameter as a parameter, or drop it and let the caller pass the value it describes.";

    /// <summary>The ObsoleteWithoutExplanation rule description.</summary>
    private const string ObsoleteWithoutExplanationDescription =
        "'[Obsolete]' with no message tells a caller their code is wrong and nothing else. The message is the whole value of the "
        + "attribute — it is the one place the author can hand the reader the migration, at exactly the moment they need it.";

    /// <summary>The ObsoleteCodeShouldBeRemoved rule description.</summary>
    private const string ObsoleteCodeShouldBeRemovedDescription =
        "Deprecating a member is the first half of removing it. The second half is the one that pays: until the member is gone it is still "
        + "compiled, still tested, still maintained, and still found by everyone reading the type for the first time. This rule is a standing "
        + "reminder — it reports every '[Obsolete]', including one with a message, and it keeps reporting until the code is deleted. That "
        + "makes it a rule for a codebase actively retiring API, and the wrong rule for a library that must keep its obsolete members for "
        + "compatibility: there, set it to 'none' and let SST2308 and SST2314 police the attribute's contents instead.";

    /// <summary>The PublicConstantField rule description.</summary>
    private const string PublicConstantFieldDescription =
        "A 'const' is not read at run time — its value is copied into the call site by the compiler. When that call site is in another "
        + "assembly, the copy is taken at the moment that assembly is built, and it stays there. Ship a new version with a different value "
        + "and every caller compiled against the old one keeps the old number, silently, until it is rebuilt. A 'static readonly' field is "
        + "read from the declaring assembly at run time, so a change reaches everybody. It is not a drop-in replacement, though: the language "
        + "requires a real 'const' for an attribute argument, a 'case' label, and a default parameter value, and a value that feeds one of "
        + "those has to stay a 'const'.";

    /// <summary>The TypeInGlobalNamespace rule description.</summary>
    private const string TypeInGlobalNamespaceDescription =
        "A type in the global namespace is visible from every file in every project that references the assembly, with no way to opt out — a "
        + "consumer cannot 'using' their way around a name they did not ask for, and cannot avoid a collision with their own. A namespace is "
        + "the one tool the language gives for that, and it costs one line.";

    /// <summary>The EnumStorageShouldBeAllowed rule description.</summary>
    private const string EnumStorageShouldBeAllowedDescription =
        "An enum that names no underlying type is stored as 'int', and 'int' is what a reader, a serializer, and an interop signature all "
        + "assume unless told otherwise. Naming a different one is a real decision — 'byte' to pack a struct, 'long' to carry more than "
        + "thirty-two flags, a fixed width to match a wire format — and a decision worth making deliberately rather than by habit. The rule "
        + "does not claim to know which types a project should permit: it reports the storage types that are not on the allowed list, and the "
        + "list is yours to set with 'stylesharp.allowed_enum_storage'. It defaults to 'int' alone, which is the strict reading; a project "
        + "that packs deliberately should widen it rather than suppress the rule.";

    /// <summary>The ObsoleteWithoutDiagnosticId rule description.</summary>
    private const string ObsoleteWithoutDiagnosticIdDescription =
        "A message tells a caller what to do; a 'DiagnosticId' lets them do it. Without one, every deprecation in every library collapses "
        + "into the same CS0618, so a caller cannot suppress one migration they have already scheduled without suppressing all of them, and "
        + "cannot make one of them an error while the rest stay warnings. Give the attribute an id of your own and a 'UrlFormat', and the "
        + "warning arrives with a name the caller can act on and a link to the instructions.";

    /// <summary>Creates a Warning-severity Design descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Design", description);
}
