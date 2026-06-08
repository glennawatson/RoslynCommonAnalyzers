// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a multi-line object, collection, array initializer, anonymous object, or enum
/// whose last element is not followed by a trailing comma (SST1413). A trailing comma keeps
/// later reordering and diffing clean.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1413TrailingCommaAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The brace-delimited list kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ObjectInitializerExpression,
        SyntaxKind.CollectionInitializerExpression,
        SyntaxKind.ArrayInitializerExpression,
        SyntaxKind.AnonymousObjectCreationExpression,
        SyntaxKind.EnumDeclaration);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.TrailingComma);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Reports a multi-line list whose last element lacks a trailing comma.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!TryGetList(context.Node, out var open, out var close, out var elementCount, out var separatorCount, out var lastElement))
        {
            return;
        }

        if (elementCount == 0 || separatorCount >= elementCount)
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (LayoutHelpers.StartLine(text, open) == LayoutHelpers.StartLine(text, close))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.TrailingComma, lastElement.GetLocation()));
    }

    /// <summary>Extracts the brace pair and element/separator counts of a supported list node.</summary>
    /// <param name="node">The list node.</param>
    /// <param name="open">The opening brace.</param>
    /// <param name="close">The closing brace.</param>
    /// <param name="elementCount">The number of elements.</param>
    /// <param name="separatorCount">The number of separators (a trailing comma makes this equal the element count).</param>
    /// <param name="lastElement">The last element node.</param>
    /// <returns><see langword="true"/> when the node is a supported list.</returns>
    private static bool TryGetList(SyntaxNode node, out SyntaxToken open, out SyntaxToken close, out int elementCount, out int separatorCount, out SyntaxNode lastElement)
    {
        switch (node)
        {
            case InitializerExpressionSyntax initializer:
            {
                open = initializer.OpenBraceToken;
                close = initializer.CloseBraceToken;
                elementCount = initializer.Expressions.Count;
                separatorCount = initializer.Expressions.SeparatorCount;
                lastElement = elementCount > 0 ? initializer.Expressions[elementCount - 1] : node;
                return true;
            }

            case AnonymousObjectCreationExpressionSyntax anonymous:
            {
                open = anonymous.OpenBraceToken;
                close = anonymous.CloseBraceToken;
                elementCount = anonymous.Initializers.Count;
                separatorCount = anonymous.Initializers.SeparatorCount;
                lastElement = elementCount > 0 ? anonymous.Initializers[elementCount - 1] : node;
                return true;
            }

            case EnumDeclarationSyntax @enum:
            {
                open = @enum.OpenBraceToken;
                close = @enum.CloseBraceToken;
                elementCount = @enum.Members.Count;
                separatorCount = @enum.Members.SeparatorCount;
                lastElement = elementCount > 0 ? @enum.Members[elementCount - 1] : node;
                return true;
            }

            default:
            {
                open = default;
                close = default;
                elementCount = 0;
                separatorCount = 0;
                lastElement = node;
                return false;
            }
        }
    }
}
