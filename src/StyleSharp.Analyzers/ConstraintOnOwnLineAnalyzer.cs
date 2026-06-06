// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a generic type-constraint clause (<c>where T : …</c>) that shares its line with the
/// declaration or a previous constraint (SST1127). One constraint per line keeps long generic
/// signatures readable.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstraintOnOwnLineAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.ConstraintOnOwnLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.TypeParameterConstraintClause);
    }

    /// <summary>Reports a constraint clause that does not start its own line.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var clause = (TypeParameterConstraintClauseSyntax)context.Node;
        var previous = clause.WhereKeyword.GetPreviousToken();
        if (previous.IsKind(SyntaxKind.None))
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (LineOf(text, previous.Span.End) != LineOf(text, clause.WhereKeyword.SpanStart))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.ConstraintOnOwnLine, clause.GetLocation()));
    }

    /// <summary>Returns the zero-based line number for a position.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">The position to look up.</param>
    /// <returns>The line number.</returns>
    private static int LineOf(SourceText text, int position) => text.Lines.GetLineFromPosition(position).LineNumber;
}
