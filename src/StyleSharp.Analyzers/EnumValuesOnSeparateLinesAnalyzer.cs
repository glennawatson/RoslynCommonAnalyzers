// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an enum member that begins on the line a previous member ended (SST1136). One member per
/// line lets the values read as a list and keeps diffs small when members are added or reordered.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnumValuesOnSeparateLinesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.EnumValuesOnSeparateLines);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EnumDeclaration);
    }

    /// <summary>Reports each enum member that shares a line with the previous member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var members = ((EnumDeclarationSyntax)context.Node).Members;
        if (members.Count < 2)
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        var previousEndLine = LineOf(text, members[0].Span.End);
        for (var index = 1; index < members.Count; index++)
        {
            var member = members[index];
            if (LineOf(text, member.SpanStart) == previousEndLine)
            {
                context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.EnumValuesOnSeparateLines, member.Identifier.GetLocation()));
            }

            previousEndLine = LineOf(text, member.Span.End);
        }
    }

    /// <summary>Returns the zero-based line number for a position.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">The position to look up.</param>
    /// <returns>The line number.</returns>
    private static int LineOf(SourceText text, int position) => text.Lines.GetLineFromPosition(position).LineNumber;
}
