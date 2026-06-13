// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Single source of truth for the maintainability (SST14xx) diagnostic descriptors.
/// </summary>
internal static class MaintainabilityRules
{
    /// <summary>SST1400 — an element does not declare an access modifier.</summary>
    public static readonly DiagnosticDescriptor AccessModifierDeclared = Create(
        "SST1400",
        "Access modifier should be declared",
        "'{0}' should declare an explicit access modifier",
        "Types and members declare their accessibility explicitly rather than relying on the implicit default.");

    /// <summary>SST1401 — a non-private, non-constant field is exposed.</summary>
    public static readonly DiagnosticDescriptor FieldsPrivate = Create(
        "SST1401",
        "Fields should be private",
        "Field '{0}' should be private; expose it through a property instead",
        "Fields are an implementation detail and should be private (constants may be any accessibility).");

    /// <summary>SST1402 — a file declares more than one top-level type.</summary>
    public static readonly DiagnosticDescriptor SingleType = Create(
        "SST1402",
        "File should contain a single type",
        "'{0}' should be moved to its own file; a file should declare a single top-level type",
        "Each file declares a single top-level type so types are easy to locate.");

    /// <summary>SST1403 — a file declares more than one namespace.</summary>
    public static readonly DiagnosticDescriptor SingleNamespace = Create(
        "SST1403",
        "File should contain a single namespace",
        "Namespace '{0}' should be the only namespace in the file",
        "Each file declares a single namespace.");

    /// <summary>SST1404 — a code-analysis suppression has no justification.</summary>
    public static readonly DiagnosticDescriptor SuppressionJustified = Create(
        "SST1404",
        "Code analysis suppression should have justification",
        "The suppression should set a non-empty 'Justification'",
        "Every [SuppressMessage] explains why the rule is suppressed.");

    /// <summary>SST1405 — a <c>Debug.Assert</c> call provides no message.</summary>
    public static readonly DiagnosticDescriptor AssertMessage = Create(
        "SST1405",
        "Debug.Assert should provide message text",
        "The Debug.Assert call should provide a message describing the assertion",
        "Debug.Assert calls describe the failed assumption for whoever hits them.");

    /// <summary>SST1406 — a <c>Debug.Fail</c> call provides no message.</summary>
    public static readonly DiagnosticDescriptor FailMessage = Create(
        "SST1406",
        "Debug.Fail should provide message text",
        "The Debug.Fail call should provide a message describing the failure",
        "Debug.Fail calls describe the failure for whoever hits them.");

    /// <summary>SST1407 — mixed-precedence arithmetic is not parenthesized.</summary>
    public static readonly DiagnosticDescriptor ArithmeticPrecedence = Create(
        "SST1407",
        "Arithmetic expressions should declare precedence",
        "Add parentheses to make the arithmetic precedence explicit",
        "Mixed arithmetic and shift operators are parenthesized so the intended precedence is clear.");

    /// <summary>SST1408 — mixed conditional operators are not parenthesized.</summary>
    public static readonly DiagnosticDescriptor ConditionalPrecedence = Create(
        "SST1408",
        "Conditional expressions should declare precedence",
        "Add parentheses to make the conditional precedence explicit",
        "Expressions mixing '&&' and '||' are parenthesized so the intended precedence is clear.");

    /// <summary>SST1410 — an anonymous method has an empty parameter list.</summary>
    public static readonly DiagnosticDescriptor RemoveDelegateParentheses = Create(
        "SST1410",
        "Remove delegate parenthesis when possible",
        "Remove the empty parameter list from the anonymous method",
        "An anonymous method with no parameters omits the empty parameter list.");

    /// <summary>SST1411 — an attribute uses an empty argument list.</summary>
    public static readonly DiagnosticDescriptor RemoveAttributeParentheses = Create(
        "SST1411",
        "Attribute constructor should not use unnecessary parenthesis",
        "Remove the empty argument list from the attribute",
        "An attribute with no arguments omits the empty parentheses.");

    /// <summary>SST1413 — a multi-line initializer omits the trailing comma.</summary>
    public static readonly DiagnosticDescriptor TrailingComma = Create(
        "SST1413",
        "Use a trailing comma in multi-line initializers",
        "Add a trailing comma after the last element",
        "The last element of a multi-line initializer or enum is followed by a trailing comma so reordering is clean.");

    /// <summary>SST1412 — files should be stored as UTF-8 with a byte order mark (opt-in; mutually exclusive with SST1450).</summary>
    public static readonly DiagnosticDescriptor Utf8WithBom = CreateOptIn(
        "SST1412",
        "Store files as UTF-8 with a byte order mark",
        "This file should be saved as UTF-8 with a byte order mark",
        "Source files are stored as UTF-8 with a byte order mark. Off by default — enable either this or SST1450, not both.");

    /// <summary>SST1450 — files should be stored as UTF-8 without a byte order mark (StyleSharp original; opt-in; mutually exclusive with SST1412).</summary>
    public static readonly DiagnosticDescriptor Utf8WithoutBom = CreateOptIn(
        "SST1450",
        "Store files as UTF-8 without a byte order mark",
        "This file should be saved as UTF-8 without a byte order mark",
        "Source files are stored as UTF-8 without a byte order mark. Off by default — enable either this or SST1412, not both.");

    /// <summary>SST1414 — a tuple type in a member signature has an unnamed element.</summary>
    public static readonly DiagnosticDescriptor TupleSignatureElementNames = Create(
        "SST1414",
        "Tuple types in signatures should have element names",
        "Name the elements of this tuple type",
        "A tuple type that appears in a member signature names each element so callers do not depend on positional 'ItemN' access.");

    /// <summary>SST1416 — a public member is declared in a type that is not externally visible (StyleSharp original; opt-in).</summary>
    public static readonly DiagnosticDescriptor NoPublicOnInternalType = CreateOptIn(
        "SST1416",
        "Do not declare public members in a non-public type",
        "Member '{0}' is public but its type is not externally visible; declare it internal",
        "A public member of a type that is not externally visible is misleading — its effective accessibility is limited by the type. Off by default — public-on-internal is a common habit.");

    /// <summary>SST1418 — a binary expression is an operand of <c>??</c> without parentheses (StyleSharp original; extends SST1407/SST1408).</summary>
    public static readonly DiagnosticDescriptor NullCoalescingPrecedence = Create(
        "SST1418",
        "Declare precedence when mixing the null-coalescing operator",
        "Parenthesize this expression to make its precedence with '??' explicit",
        "A binary expression used as an operand of the '??' operator is parenthesized so the precedence is explicit.");

    /// <summary>SST1417 — a namespace does not match the file's folder structure (StyleSharp original; opt-in).</summary>
    public static readonly DiagnosticDescriptor NamespaceMatchesFolder = CreateOptIn(
        "SST1417",
        "Namespace should match the folder structure",
        "Namespace '{0}' does not match the folder structure; expected '{1}'",
        "A type's namespace mirrors the file's folder path under the project root. Off by default — set 'stylesharp.namespace_root' in .editorconfig to override the root namespace.");

    /// <summary>SST1415 — an argument-exception constructor uses a string literal where <c>nameof</c> would track renames (StyleSharp original).</summary>
    public static readonly DiagnosticDescriptor UseNameofForParameter = Create(
        "SST1415",
        "Use nameof for parameter references",
        "Use 'nameof({0})' instead of the string literal \"{0}\"",
        "A string literal that names a parameter in an argument-exception constructor is written with 'nameof' so it follows renames.");

    /// <summary>SST1421 — a property has a setter but no getter.</summary>
    public static readonly DiagnosticDescriptor NoWriteOnlyProperty = Create(
        "SST1421",
        "Write-only properties should not be used",
        "Add a getter or replace this write-only property with a method",
        "A property exposes a readable value; write-only operations are represented by methods.");

    /// <summary>SST1423 — a switch statement exceeds the configured section count.</summary>
    public static readonly DiagnosticDescriptor TooManySwitchSections = Create(
        "SST1423",
        "Switch statements should not have too many sections",
        "Reduce this switch statement from {0} sections to at most {1}",
        "Large switch statements are split into smaller abstractions. The maximum section count is configurable and defaults to 30.");

    /// <summary>SST1419 — a modifier has no effect in its declaration context.</summary>
    public static readonly DiagnosticDescriptor NoRedundantModifier = Create(
        "SST1419",
        "Remove redundant modifiers",
        "Remove the redundant '{0}' modifier",
        "Modifiers are omitted when the declaration context already guarantees the same behavior.");

    /// <summary>SST1420 — a property trivially wraps a private backing field.</summary>
    public static readonly DiagnosticDescriptor PreferAutoProperty = Create(
        "SST1420",
        "Trivial properties should be auto-implemented",
        "Convert this property to an auto-property and remove its backing field",
        "A property whose accessors only read and write a private single-use field is auto-implemented.");

    /// <summary>SST1422 — a private field acts only as method-local temporary storage (opt-in).</summary>
    public static readonly DiagnosticDescriptor PrivateFieldUsedAsLocal = CreateOptIn(
        "SST1422",
        "Private fields used only as locals should be local variables",
        "Move field '{0}' into the method that uses it",
        "A field that is reset before every use and referenced by one method does not represent object state. Off by default because conversion is a refactoring.");

    /// <summary>SST1424 — a private field is never assigned outside construction (opt-in).</summary>
    public static readonly DiagnosticDescriptor FieldShouldBeReadonly = CreateOptIn(
        "SST1424",
        "Fields that are never reassigned should be readonly",
        "Make field '{0}' readonly",
        "A private instance field assigned only by its initializer or constructors is declared readonly. Off by default to avoid overlap with IDE0044.");

    /// <summary>SST1425 — a class/struct primary-constructor parameter is reassigned after capture.</summary>
    public static readonly DiagnosticDescriptor NoReassignedPrimaryConstructorParameter = Create(
        "SST1425",
        "Do not reassign captured primary-constructor parameters",
        "Do not reassign the captured primary-constructor parameter '{0}'",
        "Reassigning a class or struct primary-constructor parameter mutates its hidden captured field, which is surprising. Use a separate local or explicit field instead.");

    /// <summary>SST1426 — a <c>#pragma warning disable</c> silences an analyzer warning that a scoped <c>[SuppressMessage]</c> should handle (StyleSharp original).</summary>
    public static readonly DiagnosticDescriptor PreferSuppressMessageOverPragma = Create(
        "SST1426",
        "Use [SuppressMessage] instead of #pragma warning disable",
        "Suppress '{0}' with a scoped [SuppressMessage] attribute instead of '#pragma warning disable'",
        "Analyzer warnings use a scoped [SuppressMessage] attribute, not a #pragma warning disable directive. Compiler (CS) warnings are exempt because only a pragma can disable them.");

    /// <summary>SST1427 — a <c>protected</c> member of a sealed type has no effect, since the type cannot be derived.</summary>
    public static readonly DiagnosticDescriptor NoProtectedInSealed = Create(
        "SST1427",
        "Protected members of sealed types should not be used",
        "Make this member private; 'protected' has no effect in a sealed type",
        "A sealed type cannot be derived from, so a 'protected' member is reachable only as if it were private. Mark it private (or drop 'protected' from 'protected internal').");

    /// <summary>SST1428 — an abstract type exposes a <c>public</c> constructor that only derived types can call.</summary>
    public static readonly DiagnosticDescriptor NoPublicConstructorOnAbstractType = Create(
        "SST1428",
        "Abstract types should not declare public constructors",
        "Make this constructor 'protected'; only a derived type can call it",
        "An abstract type cannot be instantiated directly, so a 'public' constructor is misleading. A 'protected' (or private) constructor states that only derived types call it.");

    /// <summary>SST1429 — a <c>catch</c> clause swallows the base <see cref="System.Exception"/> with an empty body.</summary>
    public static readonly DiagnosticDescriptor NoEmptyCatchOfBaseException = Create(
        "SST1429",
        "Empty catch clauses should not swallow the base exception",
        "Handle, rethrow, or narrow this catch; an empty catch of the base exception hides failures",
        "A 'catch (Exception) { }' (or bare 'catch { }') with an empty body silently discards every error, including ones the code is not prepared for. Handle it, rethrow, or catch a narrower type.");

    /// <summary>SST1430 — <c>throw ex;</c> in a catch resets the captured stack trace instead of preserving it.</summary>
    public static readonly DiagnosticDescriptor PreserveStackTraceOnRethrow = Create(
        "SST1430",
        "Rethrow with 'throw;' to preserve the stack trace",
        "Use 'throw;' instead of 'throw {0};' so the original stack trace is kept",
        "Re-throwing the caught exception with 'throw ex;' overwrites its stack trace with the rethrow location. A bare 'throw;' keeps the original trace intact.");

    /// <summary>SST1431 — a <c>static</c> member of a generic type never mentions the type's type parameters.</summary>
    public static readonly DiagnosticDescriptor StaticMemberShouldUseTypeParameter = Create(
        "SST1431",
        "Static members of a generic type should use a type parameter",
        "Reference a type parameter, or move '{0}' off the generic type",
        "A static member whose signature ignores its generic type's type parameters forces callers to pick an arbitrary type argument, and usually belongs on a non-generic type.");

    /// <summary>SST1432 — a class declares only static members yet is not marked <c>static</c> (opt-in).</summary>
    public static readonly DiagnosticDescriptor MakeClassStatic = CreateOptIn(
        "SST1432",
        "Classes with only static members should be static",
        "Mark '{0}' as 'static'; it declares only static members",
        "A class whose members are all static can be marked 'static' to forbid instantiation. Off by default, since marking a published, instantiable type 'static' is a breaking change.");

    /// <summary>SST1433 — a public parameterless constructor with an empty body restates the compiler default.</summary>
    public static readonly DiagnosticDescriptor NoRedundantConstructor = Create(
        "SST1433",
        "Redundant constructors should be removed",
        "Remove this redundant constructor; the compiler supplies an equivalent default constructor",
        "A type's only constructor being a public, parameterless, empty constructor restates the default constructor the compiler would emit anyway, so it can be removed.");

    /// <summary>SST1434 — a finalizer has an empty body and only slows down garbage collection.</summary>
    public static readonly DiagnosticDescriptor NoEmptyFinalizer = Create(
        "SST1434",
        "Empty finalizers should be removed",
        "Remove this empty finalizer; it only burdens the garbage collector",
        "An empty finalizer does no cleanup yet forces the runtime to track the object on the finalization queue, so it should be removed.");

    /// <summary>SST1435 — a namespace declaration contains no members.</summary>
    public static readonly DiagnosticDescriptor NoEmptyNamespace = Create(
        "SST1435",
        "Empty namespace declarations should be removed",
        "Remove this empty namespace declaration",
        "A namespace declaration with no members serves no purpose and can be deleted.");

    /// <summary>SST1436 — a class, struct, or record declares no members (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoEmptyType = CreateOptIn(
        "SST1436",
        "Empty types should not be declared",
        "Add members to '{0}' or remove it; an empty type is rarely intentional",
        "A class, struct, or record with no members is usually an oversight. Off by default, because empty types are sometimes used as markers, DTOs, or extension points.");

    /// <summary>SST1437 — an interface declares no members (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoEmptyInterface = CreateOptIn(
        "SST1437",
        "Empty interfaces should not be declared",
        "Add members to '{0}' or remove it; an empty interface adds no contract",
        "An interface with no members defines no contract and is often better expressed with an attribute. Off by default, because marker interfaces are an accepted pattern.");

    /// <summary>SST1438 — a method has an empty body (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoEmptyMethod = CreateOptIn(
        "SST1438",
        "Methods should not be empty",
        "Give '{0}' a body or document why it is intentionally empty",
        "An empty method body usually signals unfinished work. Off by default, because empty overrides, virtual hooks, and interface implementations are legitimately empty.");

    /// <summary>SST1439 — a loop or guard statement has an empty embedded block.</summary>
    public static readonly DiagnosticDescriptor NoEmptyNestedBlock = Create(
        "SST1439",
        "Nested code blocks should not be left empty",
        "Fill or remove this empty block; an empty loop or guard body is usually a mistake",
        "An empty block as the body of a loop ('while', 'for', 'foreach', 'do') or guard ('if', 'lock', 'fixed', 'using') usually means missing code or a stray semicolon.");

    /// <summary>Creates a Warning-severity Maintainability descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Maintainability",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");

    /// <summary>Creates a Maintainability descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Maintainability",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
