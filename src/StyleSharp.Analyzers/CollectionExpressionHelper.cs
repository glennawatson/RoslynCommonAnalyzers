// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Shared target-type and context checks for collection-expression analyzers.</summary>
internal static class CollectionExpressionHelper
{
    /// <summary>The numeric C# 12 language-version value.</summary>
    public const int CSharp12 = 1200;

    /// <summary>Resolves the conservative set of generic collection target definitions.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns>The accepted target definitions.</returns>
    public static INamedTypeSymbol[] ResolveTargets(Compilation compilation)
    {
        var targets = new INamedTypeSymbol[6];
        var count = 0;
        Add(compilation, targets, ref count, "System.Collections.Generic.List`1");
        Add(compilation, targets, ref count, "System.Collections.Generic.IList`1");
        Add(compilation, targets, ref count, "System.Collections.Generic.IEnumerable`1");
        Add(compilation, targets, ref count, "System.Collections.Generic.IReadOnlyList`1");
        Add(compilation, targets, ref count, "System.Collections.Generic.ICollection`1");
        Add(compilation, targets, ref count, "System.Collections.Generic.IReadOnlyCollection`1");

        if (count == targets.Length)
        {
            return targets;
        }

        var resized = new INamedTypeSymbol[count];
        Array.Copy(targets, resized, count);
        return resized;
    }

    /// <summary>Returns whether collection expressions are enabled for the syntax tree.</summary>
    /// <param name="node">A node in the syntax tree.</param>
    /// <returns><see langword="true"/> for C# 12 or later.</returns>
    public static bool IsLanguageSupported(SyntaxNode node)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= CSharp12;

    /// <summary>Returns whether the expression has an explicit target type and that type is accepted.</summary>
    /// <param name="context">The syntax analysis context.</param>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="targets">The accepted named target definitions.</param>
    /// <returns><see langword="true"/> when replacement with a collection expression is conservative.</returns>
    public static bool HasAcceptedTarget(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        INamedTypeSymbol[] targets)
    {
        if (!HasExplicitTarget(expression))
        {
            return false;
        }

        var converted = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).ConvertedType;
        return converted is IArrayTypeSymbol { Rank: 1 }
            || (converted is INamedTypeSymbol named && ContainsTarget(targets, named.OriginalDefinition));
    }

    /// <summary>Returns whether an expression appears in a context with an explicit target type.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <returns><see langword="true"/> when the context supplies a target type.</returns>
    private static bool HasExplicitTarget(ExpressionSyntax expression) =>
        expression.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } }
            ? declaration.Type is not IdentifierNameSyntax { Identifier.Text: "var" }
            : expression.Parent is AssignmentExpressionSyntax
                or ArrowExpressionClauseSyntax
                or ReturnStatementSyntax
                or ArgumentSyntax;

    /// <summary>Adds a well-known target definition when it exists.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <param name="targets">The target set.</param>
    /// <param name="count">The number of populated targets in <paramref name="targets"/>.</param>
    /// <param name="metadataName">The metadata name.</param>
    private static void Add(Compilation compilation, INamedTypeSymbol[] targets, ref int count, string metadataName)
    {
        if (compilation.GetTypeByMetadataName(metadataName) is not { } type)
        {
            return;
        }

        targets[count] = type;
        count++;
    }

    /// <summary>Returns whether the accepted target array contains the supplied original definition.</summary>
    /// <param name="targets">The accepted target definitions.</param>
    /// <param name="candidate">The candidate original definition.</param>
    /// <returns><see langword="true"/> when the candidate is accepted.</returns>
    private static bool ContainsTarget(INamedTypeSymbol[] targets, INamedTypeSymbol candidate)
    {
        for (var i = 0; i < targets.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(targets[i], candidate))
            {
                return true;
            }
        }

        return false;
    }
}
