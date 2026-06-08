// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a member attribute list that shares its line with the following attribute or with the
/// element it decorates (SST1134) — <c>[A] [B] void M()</c> or <c>[Obsolete] void M()</c>. Each
/// attribute on its own line keeps declarations scannable. Attributes on parameters and accessors
/// are inline by convention and are not inspected.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1134AttributesOnSeparateLinesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.AttributesOnSeparateLines);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AttributeList);
    }

    /// <summary>Reports a member attribute list that shares a line with what follows it.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var list = (AttributeListSyntax)context.Node;
        if (list.Parent is not MemberDeclarationSyntax)
        {
            return;
        }

        var next = list.CloseBracketToken.GetNextToken();
        if (next.IsKind(SyntaxKind.None))
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (LineOf(text, list.CloseBracketToken.Span.End) != LineOf(text, next.SpanStart))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.AttributesOnSeparateLines, list.GetLocation()));
    }

    /// <summary>Returns the zero-based line number for a position.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">The position to look up.</param>
    /// <returns>The line number.</returns>
    private static int LineOf(SourceText text, int position) => text.Lines.GetLineFromPosition(position).LineNumber;
}
