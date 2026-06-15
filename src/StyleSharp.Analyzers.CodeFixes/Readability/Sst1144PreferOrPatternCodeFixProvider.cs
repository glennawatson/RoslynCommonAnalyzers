// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Merges stacked case labels into a single <c>case A or B:</c> pattern label (SST1144).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1144PreferOrPatternCodeFixProvider))]
[Shared]
public sealed class Sst1144PreferOrPatternCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.PreferOrPattern.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<SwitchSectionSyntax>() is not { } section)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Combine into an 'or' pattern",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, section)),
                    equivalenceKey: nameof(Sst1144PreferOrPatternCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<SwitchSectionSyntax>() is not { } section)
        {
            return;
        }

        editor.ReplaceNode(section, Merge(section));
    }

    /// <summary>Replaces the switch section with its combined <c>or</c>-pattern form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="section">The switch section to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SwitchSectionSyntax section)
        => document.WithSyntaxRoot(root.ReplaceNode(section, Merge(section)));

    /// <summary>Builds the section with its labels merged into one <c>or</c>-pattern label.</summary>
    /// <param name="section">The switch section to rewrite.</param>
    /// <returns>The rewritten section.</returns>
    private static SwitchSectionSyntax Merge(SwitchSectionSyntax section)
    {
        var labels = section.Labels;
        var combined = LabelPattern(labels[0]);
        for (var i = 1; i < labels.Count; i++)
        {
            combined = SyntaxFactory.BinaryPattern(
                SyntaxKind.OrPattern,
                combined.WithTrailingTrivia(SyntaxFactory.Space),
                LabelPattern(labels[i]).WithLeadingTrivia(SyntaxFactory.Space));
        }

        var caseKeyword = SyntaxFactory.Token(SyntaxKind.CaseKeyword)
            .WithLeadingTrivia(labels[0].GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.Space);
        var label = SyntaxFactory.CasePatternSwitchLabel(caseKeyword, combined, null, LabelColon(labels[^1]));
        return section.WithLabels(SyntaxFactory.SingletonList<SwitchLabelSyntax>(label));
    }

    /// <summary>Returns the pattern that a switch label contributes to the combined <c>or</c> pattern.</summary>
    /// <param name="label">The switch label.</param>
    /// <returns>The label's pattern (a constant pattern for a value label).</returns>
    private static PatternSyntax LabelPattern(SwitchLabelSyntax label) => label switch
    {
        CasePatternSwitchLabelSyntax pattern => pattern.Pattern.WithoutTrivia(),
        CaseSwitchLabelSyntax value => SyntaxFactory.ConstantPattern(value.Value.WithoutTrivia()),
        _ => SyntaxFactory.ConstantPattern(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))
    };

    /// <summary>Returns the colon token of a switch label (keeping its trailing trivia).</summary>
    /// <param name="label">The switch label.</param>
    /// <returns>The colon token.</returns>
    private static SyntaxToken LabelColon(SwitchLabelSyntax label) => label switch
    {
        CasePatternSwitchLabelSyntax pattern => pattern.ColonToken,
        CaseSwitchLabelSyntax value => value.ColonToken,
        _ => SyntaxFactory.Token(SyntaxKind.ColonToken)
    };
}
