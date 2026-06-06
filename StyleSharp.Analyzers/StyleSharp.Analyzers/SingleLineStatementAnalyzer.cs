// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an embedded statement block (the body of an <c>if</c>, loop, <c>using</c>,
/// <c>lock</c>, <c>try</c>/<c>catch</c>, or a bare nested block) that is collapsed onto a
/// single line with its content (SST1501). Element bodies, accessor bodies, and lambda
/// bodies are governed by SST1502/SST1504 and are excluded here.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleLineStatementAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.StatementOnOwnLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Block);
    }

    /// <summary>Reports a non-empty embedded block whose braces and content share one line.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var block = (BlockSyntax)context.Node;
        if (block.Statements.Count == 0 || IsElementOrFunctionBody(block.Parent))
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (LayoutHelpers.StartLine(text, block.OpenBraceToken) != LayoutHelpers.StartLine(text, block.CloseBraceToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.StatementOnOwnLine, block.OpenBraceToken.GetLocation()));
    }

    /// <summary>Returns whether the block is the body of a member, accessor, local function, or lambda.</summary>
    /// <param name="parent">The block's parent node.</param>
    /// <returns><see langword="true"/> when the block is an element or function body (excluded from SST1501).</returns>
    private static bool IsElementOrFunctionBody(SyntaxNode? parent)
        => parent is BaseMethodDeclarationSyntax
            or AccessorDeclarationSyntax
            or AnonymousFunctionExpressionSyntax
            or LocalFunctionStatementSyntax;
}
