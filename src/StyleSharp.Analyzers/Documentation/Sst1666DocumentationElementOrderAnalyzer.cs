// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a documentation comment whose elements are written out of the conventional order (SST1666):
/// summary, type parameters, parameters, returns, value, exceptions, then the longer prose.
/// </summary>
/// <remarks>
/// The order itself lives in <see cref="DocumentationElementOrder"/> so the analyzer and its code fix cannot
/// disagree about it. One pass over the comment's content settles the question, and a comment whose elements
/// are already in order allocates nothing.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1666DocumentationElementOrderAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DocumentationRules.DocumentationElementOrder);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.SingleLineDocumentationCommentTrivia,
            SyntaxKind.MultiLineDocumentationCommentTrivia);
    }

    /// <summary>Reports the first out-of-order element in one documentation comment.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var documentation = (DocumentationCommentTriviaSyntax)context.Node;
        if (!DocumentationElementOrder.TryFindFirstOutOfOrder(documentation, out var outOfOrder, out var shouldPrecede))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            DocumentationRules.DocumentationElementOrder,
            outOfOrder.SyntaxTree,
            outOfOrder.Span,
            DocumentationElementOrder.NameOf(outOfOrder) ?? string.Empty,
            shouldPrecede));
    }
}
