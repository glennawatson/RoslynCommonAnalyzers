// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports an independent <c>if</c> statement that follows a closing brace on the same line (SST1146).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1146ConditionalOnNewLineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ReadabilityRules.ConditionalOnNewLine);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement);
    }

    /// <summary>Reports when the previous closing brace and the <c>if</c> keyword share a line.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var ifKeyword = ((IfStatementSyntax)context.Node).IfKeyword;
        var previous = ifKeyword.GetPreviousToken();
        if (!previous.IsKind(SyntaxKind.CloseBraceToken))
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (text.Lines.GetLinePosition(previous.SpanStart).Line != text.Lines.GetLinePosition(ifKeyword.SpanStart).Line)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.ConditionalOnNewLine, ifKeyword.GetLocation()));
    }
}
