// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites a return-only switch statement as a switch expression (SST2201).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2201PreferSwitchExpressionCodeFixProvider))]
[Shared]
public sealed class Sst2201PreferSwitchExpressionCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.PreferSwitchExpression.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Rewrite switch as an expression",
            nameof(Sst2201PreferSwitchExpressionCodeFixProvider),
            static (root, diagnostic) => DiagnosticAncestor.Find<SwitchStatementSyntax>(root, diagnostic.Location.SourceSpan) is not null,
            TryRewrite);

    /// <summary>Resolves the reported switch statement and builds the switch-expression return statement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the switch cannot be rewritten.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var switchStatement = DiagnosticAncestor.Find<SwitchStatementSyntax>(root, diagnostic.Location.SourceSpan);
        if (switchStatement is null || !Sst2201PreferSwitchExpressionAnalyzer.IsReturnOnlySwitchExpressionCandidate(switchStatement))
        {
            return null;
        }

        SeparatedSyntaxList<SwitchExpressionArmSyntax> arms = default;
        var sections = switchStatement.Sections;
        for (var i = 0; i < sections.Count; i++)
        {
            if (!TryCreateArm(sections[i], out var arm))
            {
                return null;
            }

            arms = arms.Add(arm);
        }

        var switchExpression = SyntaxFactory.SwitchExpression(switchStatement.Expression.WithoutTrivia(), arms);
        return new NodeReplacement(
            switchStatement,
            SyntaxFactory.ReturnStatement(
                default,
                SyntaxFactory.Token(switchStatement.GetLeadingTrivia(), SyntaxKind.ReturnKeyword, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker)),
                switchExpression,
                SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.SemicolonToken, switchStatement.GetTrailingTrivia())));
    }

    /// <summary>Creates a switch expression arm from a switch section.</summary>
    /// <param name="section">The switch section to rewrite.</param>
    /// <param name="arm">The switch expression arm.</param>
    /// <returns><see langword="true"/> when the section has a supported shape.</returns>
    private static bool TryCreateArm(SwitchSectionSyntax section, out SwitchExpressionArmSyntax arm)
    {
        arm = null!;
        var pattern = section.Labels[0] switch
        {
            DefaultSwitchLabelSyntax => SyntaxFactory.DiscardPattern(),
            CaseSwitchLabelSyntax caseLabel => SyntaxFactory.ConstantPattern(caseLabel.Value.WithoutTrivia()),
            CasePatternSwitchLabelSyntax patternLabel => patternLabel.Pattern.WithoutTrivia(),
            _ => null
        };

        var expression = section.Statements[0] switch
        {
            ReturnStatementSyntax { Expression: { } value } => value,
            ThrowStatementSyntax { Expression: { } value } => SyntaxFactory.ThrowExpression(value.WithoutTrivia()),
            _ => null
        };

        if (pattern is null || expression is null)
        {
            return false;
        }

        arm = SyntaxFactory.SwitchExpressionArm(pattern, expression.WithoutTrivia());
        return true;
    }
}
