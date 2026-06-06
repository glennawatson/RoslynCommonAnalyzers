// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Single source of truth for the maintainability (SST14xx) diagnostic descriptors,
/// the StyleSharp counterparts of StyleCop's SA14xx maintainability rules.
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

    /// <summary>SST1414 — a tuple type in a member signature has an unnamed element (mirrors SA1414).</summary>
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
