// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites a return-only switch statement as a switch expression (SST2201).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2201PreferSwitchExpressionCodeFixProvider))]
[Shared]
public sealed class Sst2201PreferSwitchExpressionCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.PreferSwitchExpression.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (FindSwitch(root, diagnostic.Location.SourceSpan) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Rewrite switch as an expression",
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: nameof(Sst2201PreferSwitchExpressionCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!CreateReplacement(editor.OriginalRoot, diagnostic, out var switchStatement, out var replacement)
            || switchStatement is null
            || replacement is null)
        {
            return;
        }

        editor.ReplaceNode(switchStatement, replacement);
    }

    /// <summary>Applies one switch-expression fix.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        if (!CreateReplacement(root, diagnostic, out var switchStatement, out var replacement)
            || switchStatement is null
            || replacement is null)
        {
            return document;
        }

        return document.WithSyntaxRoot(root.ReplaceNode(switchStatement, replacement));
    }

    /// <summary>Builds the switch-expression return statement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="switchStatement">The switch statement to replace.</param>
    /// <param name="replacement">The replacement return statement.</param>
    /// <returns><see langword="true"/> when the switch can be rewritten.</returns>
    private static bool CreateReplacement(
        SyntaxNode root,
        Diagnostic diagnostic,
        out SwitchStatementSyntax? switchStatement,
        out ReturnStatementSyntax? replacement)
    {
        switchStatement = FindSwitch(root, diagnostic.Location.SourceSpan);
        replacement = null;
        if (switchStatement is null || !Sst2201PreferSwitchExpressionAnalyzer.IsReturnOnlySwitchExpressionCandidate(switchStatement))
        {
            return false;
        }

        SeparatedSyntaxList<SwitchExpressionArmSyntax> arms = default;
        var sections = switchStatement.Sections;
        for (var i = 0; i < sections.Count; i++)
        {
            if (!TryCreateArm(sections[i], out var arm))
            {
                return false;
            }

            arms = arms.Add(arm);
        }

        var switchExpression = SyntaxFactory.SwitchExpression(switchStatement.Expression.WithoutTrivia(), arms);
        replacement = SyntaxFactory.ReturnStatement(switchExpression).WithTriviaFrom(switchStatement);
        return true;
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

    /// <summary>Finds the containing switch statement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <returns>The containing switch statement, or <see langword="null"/>.</returns>
    private static SwitchStatementSyntax? FindSwitch(SyntaxNode root, TextSpan span)
    {
        var node = root.FindToken(span.Start).Parent;
        while (node is not null)
        {
            if (node is SwitchStatementSyntax switchStatement)
            {
                return switchStatement;
            }

            node = node.Parent;
        }

        return null;
    }
}
