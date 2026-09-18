// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>Recognizes reference policy text in string comparisons and assertions.</summary>
internal static class ContentSecurityPolicyComparison
{
    /// <summary>Returns whether a literal is reference text consumed directly by a comparison.</summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="literal">The candidate policy literal.</param>
    /// <returns>True for recognized assertion and string-comparison operands.</returns>
    internal static bool IsReferenceText(in SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literal)
    {
        ExpressionSyntax expression = literal;
        while (expression.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized;
        }

        return expression.Parent switch
        {
            BinaryExpressionSyntax binary => IsStringEquality(context, binary),
            ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax call } } => IsComparisonInvocation(context, call),
            MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax call } access when access.Expression == expression => IsComparisonInvocation(context, call),
            _ => false,
        };
    }

    /// <summary>Distinguishes string equality from user-defined operators that may consume a policy.</summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="binary">The operation consuming the literal.</param>
    /// <returns>True for bound string equality and inequality operators.</returns>
    private static bool IsStringEquality(in SyntaxNodeAnalysisContext context, BinaryExpressionSyntax binary) =>
        binary.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
        && context.SemanticModel.GetSymbolInfo(binary, context.CancellationToken).Symbol is IMethodSymbol
        {
            ContainingType.SpecialType: SpecialType.System_String,
        };

    /// <summary>Confirms that the invoked comparison belongs to a string or assertion API.</summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="invocation">The call consuming the comparison text.</param>
    /// <returns>True for a recognized comparison API.</returns>
    private static bool IsComparisonInvocation(in SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var name = SyntaxNames.GetMemberName(invocation.Expression);
        if (!IsComparisonName(name))
        {
            return false;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return false;
        }

        return method.ContainingType.SpecialType == SpecialType.System_String
            ? IsStringComparisonName(name)
            : name switch
            {
                "Should" => method.ContainingType is
                {
                    Name: "AssertionExtensions",
                    ContainingNamespace: { Name: "FluentAssertions", ContainingNamespace.IsGlobalNamespace: true },
                },
                "Substring" => method.ContainingType is
                {
                    Name: "Contains",
                    ContainingNamespace:
                    {
                        Name: "Framework",
                        ContainingNamespace: { Name: "NUnit", ContainingNamespace.IsGlobalNamespace: true },
                    },
                },
                _ => IsAssertionNamespace(method.ContainingNamespace),
            };
    }

    /// <summary>Recognizes comparison methods before resolving their declaring symbols.</summary>
    /// <param name="name">The invoked method name.</param>
    /// <returns>True when the method name can represent an assertion or comparison.</returns>
    private static bool IsComparisonName(string? name) =>
        name is "Should" or "Substring"
        || IsStringComparisonName(name) || IsEqualityAssertionName(name) || IsFluentComparisonName(name)
        || IsFluentEquivalenceName(name) || IsShouldlyComparisonName(name);

    /// <summary>Recognizes string search and comparison operations.</summary>
    /// <param name="name">The invoked method name.</param>
    /// <returns>True for a string comparison name.</returns>
    private static bool IsStringComparisonName(string? name) =>
        name is "Contains" or "StartsWith" or "EndsWith" or "IndexOf" or "LastIndexOf" or "Equals" or "Compare" or "CompareOrdinal";

    /// <summary>Recognizes equality assertion operations.</summary>
    /// <param name="name">The invoked method name.</param>
    /// <returns>True for an equality assertion name.</returns>
    private static bool IsEqualityAssertionName(string? name) =>
        name is "That" or "Equal" or "NotEqual" or "AreEqual" or "AreNotEqual" or "IsEqualTo" or "IsNotEqualTo" or "EqualTo";

    /// <summary>Recognizes fluent string assertion operations.</summary>
    /// <param name="name">The invoked method name.</param>
    /// <returns>True for a fluent string assertion name.</returns>
    private static bool IsFluentComparisonName(string? name) =>
        name is "DoesNotContain" or "Contain" or "NotContain" or "Be" or "NotBe" or "StartWith" or "EndWith";

    /// <summary>Recognizes fluent string comparisons that disregard casing.</summary>
    /// <param name="name">The invoked method name.</param>
    /// <returns>True for a fluent string equivalence assertion.</returns>
    private static bool IsFluentEquivalenceName(string? name) =>
        name is "BeEquivalentTo" or "NotBeEquivalentTo" or "ContainEquivalentOf" or "NotContainEquivalentOf";

    /// <summary>Recognizes Shouldly string assertion operations.</summary>
    /// <param name="name">The invoked method name.</param>
    /// <returns>True for a Shouldly string assertion name.</returns>
    private static bool IsShouldlyComparisonName(string? name) =>
        name is "ShouldBe" or "ShouldNotBe" or "ShouldContain" or "ShouldNotContain";

    /// <summary>Identifies assertion APIs by their declaring namespace rather than the caller's spelling.</summary>
    /// <param name="namespaceSymbol">The namespace declaring the invoked comparison.</param>
    /// <returns>True for supported assertion framework namespaces.</returns>
    private static bool IsAssertionNamespace(INamespaceSymbol namespaceSymbol)
    {
        for (var current = namespaceSymbol; !current.IsGlobalNamespace; current = current.ContainingNamespace)
        {
            if (current is
                { Name: "Assertions", ContainingNamespace: { Name: "TUnit", ContainingNamespace.IsGlobalNamespace: true } }
                or { Name: "Framework", ContainingNamespace: { Name: "NUnit", ContainingNamespace.IsGlobalNamespace: true } }
                or
                {
                    Name: "UnitTesting",
                    ContainingNamespace:
                    {
                        Name: "TestTools",
                        ContainingNamespace:
                        {
                            Name: "VisualStudio",
                            ContainingNamespace: { Name: "Microsoft", ContainingNamespace.IsGlobalNamespace: true },
                        },
                    },
                }
                or { Name: "Xunit" or "FluentAssertions" or "Shouldly", ContainingNamespace.IsGlobalNamespace: true })
            {
                return true;
            }
        }

        return false;
    }
}
