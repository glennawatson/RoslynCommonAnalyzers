// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>goto</c> that jumps to a label (SST2014). A jump between switch sections —
/// <c>goto case</c> and <c>goto default</c> — is not reported: the language offers no other way to say it,
/// and saying it that way is idiomatic.
/// </summary>
/// <remarks>
/// <para>
/// The switch exclusion is free rather than checked. C# gives the three forms three different syntax kinds, so
/// registering <see cref="SyntaxKind.GotoStatement"/> alone never sees a <c>goto case</c> or a
/// <c>goto default</c> in the first place. There is no code fix: replacing a jump means restructuring the
/// control flow around it, and which structure was meant — a loop, an extracted method, an early return — is
/// not something the jump records.
/// </para>
/// <para>
/// From C# 15 a jump out of an enclosing loop is exempt: <c>break outer;</c> expresses it directly and the
/// SDK reports that shape with a fix attached, so reporting it here too would offer strictly less. Every
/// other jump is still reported at every language version.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2014AvoidGotoAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ModernizationRules.AvoidGoto);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.GotoStatement);
    }

    /// <summary>Reports one jump to a label.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var statement = (GotoStatementSyntax)context.Node;
        if (LanguageVersions.SupportsCSharp15(statement) && EscapesEnclosingLoop(context, statement))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernizationRules.AvoidGoto, statement.GetLocation()));
    }

    /// <summary>Gets whether a jump leaves a loop that encloses it, which a labelled break now expresses.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="statement">The goto statement.</param>
    /// <returns><see langword="true"/> when the target label sits outside the innermost enclosing loop.</returns>
    private static bool EscapesEnclosingLoop(SyntaxNodeAnalysisContext context, GotoStatementSyntax statement)
    {
        if (statement.Expression is null || EnclosingLoop(statement) is not { } loop)
        {
            return false;
        }

        if (context.SemanticModel.GetSymbolInfo(statement.Expression, context.CancellationToken).Symbol is not ILabelSymbol label
            || label.DeclaringSyntaxReferences.Length == 0)
        {
            return false;
        }

        var target = label.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken);
        return !loop.Span.Contains(target.Span);
    }

    /// <summary>Gets the innermost loop containing a statement, stopping at the enclosing function.</summary>
    /// <param name="statement">The goto statement.</param>
    /// <returns>The enclosing loop, or <see langword="null"/> when the jump is not inside one.</returns>
    private static SyntaxNode? EnclosingLoop(GotoStatementSyntax statement)
    {
        for (var current = statement.Parent; current is not null; current = current.Parent)
        {
            if (current is ForStatementSyntax
                or ForEachStatementSyntax
                or ForEachVariableStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax)
            {
                return current;
            }

            if (current is MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return null;
            }
        }

        return null;
    }
}
