// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Merges two switch sections that share a body into one (SST2414), stacking the later section's labels onto
/// the earlier one. The merge is always behaviour-preserving: labels match a value regardless of their
/// position, and the analyzer has already excluded any switch a <c>goto case</c> could jump into.
/// </summary>
/// <remarks>
/// A <c>switch</c> expression joins its two arms with an <c>or</c> pattern instead, which is equally safe:
/// a pattern evaluates nothing, so the order of every test survives. The duplicated <c>if</c>-chain shape is
/// reported without a fix, because merging conditions safely depends on them being side-effect-free.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2414DuplicateBranchImplementationCodeFixProvider))]
[Shared]
public sealed class Sst2414DuplicateBranchImplementationCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The characters an arm adds around its pattern: <c> =&gt; </c> and the trailing comma.</summary>
    private const int ArmWidth = 5;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.DuplicateBranchImplementation.Id);

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

        var options = context.Document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(root.SyntaxTree);
        await ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Merge the duplicated sections",
            nameof(Sst2414DuplicateBranchImplementationCodeFixProvider),
            (current, reported) => TryRewrite(current, options, reported)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var options = editor.OriginalDocument.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(editor.OriginalRoot.SyntaxTree);
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, (current, reported) => TryRewrite(current, options, reported));
    }

    /// <summary>Resolves the reported section and merges it into the earlier matching one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, AnalyzerConfigOptions options, Diagnostic diagnostic)
    {
        var reported = root.FindNode(diagnostic.Location.SourceSpan);
        if (reported?.FirstAncestorOrSelf<SwitchExpressionArmSyntax>() is { Parent: SwitchExpressionSyntax switchExpression } arm)
        {
            return TryMergeArms(switchExpression, options, arm);
        }

        if (reported?.FirstAncestorOrSelf<SwitchSectionSyntax>() is not { Parent: SwitchStatementSyntax switchStatement } duplicate)
        {
            return null;
        }

        var sections = switchStatement.Sections;
        var duplicateIndex = sections.IndexOf(duplicate);
        var partnerIndex = FindPartner(sections, duplicateIndex);
        if (partnerIndex < 0)
        {
            return null;
        }

        return new NodeReplacement(switchStatement, Merge(switchStatement, partnerIndex, duplicateIndex));
    }

    /// <summary>Joins a duplicated switch-expression arm into the earlier arm that produces the same value.</summary>
    /// <param name="switchExpression">The switch expression.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <param name="duplicate">The reported arm.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the arms cannot be joined.</returns>
    /// <remarks>
    /// A pattern evaluates nothing, so joining two arms with <c>or</c> keeps the order of every test. A
    /// <c>when</c> clause does run code, and two arms guarded by different clauses do not describe one case,
    /// so neither arm may carry one.
    /// </remarks>
    private static NodeReplacement? TryMergeArms(SwitchExpressionSyntax switchExpression, AnalyzerConfigOptions options, SwitchExpressionArmSyntax duplicate)
    {
        var arms = switchExpression.Arms;
        var duplicateIndex = arms.IndexOf(duplicate);
        if (duplicateIndex <= 0 || duplicate.WhenClause is not null)
        {
            return null;
        }

        for (var i = 0; i < duplicateIndex; i++)
        {
            var partner = arms[i];
            if (partner.WhenClause is not null
                || !SyntaxFactory.AreEquivalent(partner.Expression, duplicate.Expression, topLevel: false)
                || DirectiveBoundaries.Separate(partner, duplicate))
            {
                continue;
            }

            var joined = LayOutPattern(partner, options, duplicate.Pattern);
            var merged = partner.WithPattern(joined.WithTriviaFrom(partner.Pattern));
            return new NodeReplacement(switchExpression, switchExpression.WithArms(arms.Replace(partner, merged).RemoveAt(duplicateIndex)));
        }

        return null;
    }

    /// <summary>Builds the joined pattern, wrapping its alternatives when one line would run past the maximum.</summary>
    /// <param name="partner">The arm the duplicate joins.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <param name="addition">The duplicate arm's pattern.</param>
    /// <returns>The joined pattern laid out to fit the line budget.</returns>
    /// <remarks>
    /// Every <c>or</c> leads a continuation line one indent step in from the arm, so a merge that outgrows
    /// the line reads as a list of alternatives instead of trading the duplication for an over-long line.
    /// </remarks>
    private static PatternSyntax LayOutPattern(SwitchExpressionArmSyntax partner, AnalyzerConfigOptions options, PatternSyntax addition)
    {
        var text = partner.SyntaxTree.GetText();
        var indent = LayoutFixHelpers.IndentOfLine(text, partner.SpanStart);
        var joined = SyntaxFactory.BinaryPattern(SyntaxKind.OrPattern, partner.Pattern, OrKeyword(SyntaxFactory.TriviaList(SyntaxFactory.Space)), addition);
        var flat = Respace(joined, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        if (indent.Length + flat.Span.Length + ArmWidth + partner.Expression.Span.Length <= SizeLimitOptions.ReadMaxLineLength(options))
        {
            return flat;
        }

        var newLine = LayoutFixHelpers.DetectNewLine(text);
        return Respace(joined, SyntaxFactory.TriviaList(SyntaxFactory.EndOfLine(newLine), SyntaxFactory.Whitespace(indent + LayoutFixHelpers.IndentStep)));
    }

    /// <summary>Rewrites the trivia around every <c>or</c> in a chain of alternatives.</summary>
    /// <param name="pattern">The pattern to lay out.</param>
    /// <param name="operatorLeading">The trivia each <c>or</c> keyword leads with.</param>
    /// <returns>The pattern with its alternatives spaced alike.</returns>
    private static PatternSyntax Respace(PatternSyntax pattern, SyntaxTriviaList operatorLeading)
        => pattern is BinaryPatternSyntax { RawKind: (int)SyntaxKind.OrPattern } alternatives
            ? SyntaxFactory.BinaryPattern(
                SyntaxKind.OrPattern,
                Respace(alternatives.Left, operatorLeading),
                OrKeyword(operatorLeading),
                alternatives.Right.WithoutTrivia())
            : pattern.WithoutTrivia();

    /// <summary>Builds an <c>or</c> keyword that keeps a single trailing space.</summary>
    /// <param name="leading">The trivia the keyword leads with.</param>
    /// <returns>The keyword token.</returns>
    private static SyntaxToken OrKeyword(SyntaxTriviaList leading)
        => SyntaxFactory.Token(leading, SyntaxKind.OrKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));

    /// <summary>Finds the earlier section whose body matches the duplicate's.</summary>
    /// <param name="sections">The switch's sections.</param>
    /// <param name="duplicateIndex">The reported section's position.</param>
    /// <returns>The partner's position, or <c>-1</c>.</returns>
    private static int FindPartner(SyntaxList<SwitchSectionSyntax> sections, int duplicateIndex)
    {
        if (duplicateIndex <= 0)
        {
            return -1;
        }

        var duplicate = sections[duplicateIndex].Statements;
        for (var i = 0; i < duplicateIndex; i++)
        {
            if (sections[i].Statements.Count > 0
                && AreEquivalentStatements(sections[i].Statements, duplicate)
                && !DirectiveBoundaries.Separate(sections[i], sections[duplicateIndex]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Stacks the duplicate section's labels onto its partner and removes the duplicate.</summary>
    /// <param name="switchStatement">The switch statement.</param>
    /// <param name="partnerIndex">The partner's position.</param>
    /// <param name="duplicateIndex">The duplicate's position.</param>
    /// <returns>The merged switch statement.</returns>
    private static SwitchStatementSyntax Merge(SwitchStatementSyntax switchStatement, int partnerIndex, int duplicateIndex)
    {
        var sections = switchStatement.Sections;
        var partner = sections[partnerIndex];
        var duplicate = sections[duplicateIndex];
        var mergedLabels = partner.Labels.AddRange(duplicate.Labels);
        var merged = partner.WithLabels(mergedLabels).WithAdditionalAnnotations(Formatter.Annotation);
        var updated = sections.Replace(partner, merged).RemoveAt(duplicateIndex);
        return switchStatement.WithSections(updated);
    }

    /// <summary>Returns whether two statement lists run the same statements in the same order.</summary>
    /// <param name="first">The first statement list.</param>
    /// <param name="second">The second statement list.</param>
    /// <returns><see langword="true"/> when the lists match, ignoring trivia.</returns>
    private static bool AreEquivalentStatements(SyntaxList<StatementSyntax> first, SyntaxList<StatementSyntax> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Count; index++)
        {
            if (!SyntaxFactory.AreEquivalent(first[index], second[index], topLevel: false))
            {
                return false;
            }
        }

        return true;
    }
}
