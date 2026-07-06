// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// A code fix provider for the <see cref="Sst1127ConstraintOnOwnLineAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1127ConstraintOnOwnLineCodeFixProvider))]
[Shared]
public sealed class Sst1127ConstraintOnOwnLineCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.ConstraintOnOwnLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            if (!TryBuildChange(text, root, diagnostic, out var change))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Place the constraint on its own line",
                    _ => Task.FromResult(context.Document.WithText(text.WithChanges(change))),
                    equivalenceKey: nameof(Sst1127ConstraintOnOwnLineCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryBuildChange(text, root, diagnostic, out var change))
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Returns the indentation for a constraint clause moved to its own line.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="clause">The constraint clause.</param>
    /// <returns>The indentation text.</returns>
    private static string GetConstraintIndent(SourceText text, TypeParameterConstraintClauseSyntax clause)
    {
        if (clause.Parent is { } owner)
        {
            return LayoutFixHelpers.IndentOfLine(text, owner.GetFirstToken().SpanStart) + LayoutFixHelpers.IndentStep;
        }

        var previous = clause.WhereKeyword.GetPreviousToken();
        return LayoutFixHelpers.IndentOfLine(text, previous.SpanStart) + LayoutFixHelpers.IndentStep;
    }

    /// <summary>Builds the text change that moves a reported constraint clause to its own line.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="change">The text change.</param>
    /// <returns><see langword="true"/> when a safe text change was built.</returns>
    private static bool TryBuildChange(SourceText text, SyntaxNode root, Diagnostic diagnostic, out TextChange change)
    {
        change = default;
        if (root.FindNode(diagnostic.Location.SourceSpan) is not TypeParameterConstraintClauseSyntax clause)
        {
            return false;
        }

        var previous = clause.WhereKeyword.GetPreviousToken();
        if (previous.IsKind(SyntaxKind.None)
            || !LayoutFixHelpers.IsWhitespaceBetween(text, previous.Span.End, clause.WhereKeyword.SpanStart))
        {
            return false;
        }

        var newLine = LayoutFixHelpers.DetectNewLine(text);
        var indent = GetConstraintIndent(text, clause);
        change = new TextChange(TextSpan.FromBounds(previous.Span.End, clause.WhereKeyword.SpanStart), newLine + indent);
        return true;
    }
}
