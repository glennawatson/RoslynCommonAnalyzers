// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Collapses a multi-line object or collection initializer onto a single line (SST1531).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1531InitializerOnSingleLineCodeFixProvider))]
[Shared]
public sealed class Sst1531InitializerOnSingleLineCodeFixProvider : CodeFixProvider
{
    /// <summary>The gaps at the initializer's two braces, rewritten on top of one gap per expression.</summary>
    private const int InitializerBraceGapCount = 2;

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.InitializerOnSingleLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TextChangeCodeFix.RegisterAsync(
            context,
            static (root, diagnostic) => root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is InitializerExpressionSyntax ? "Collapse onto a single line" : null,
            nameof(Sst1531InitializerOnSingleLineCodeFixProvider),
            RegisterTextChanges);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not InitializerExpressionSyntax initializer)
        {
            return;
        }

        AppendCollapse(text, initializer, changes);
    }

    /// <summary>Appends the changes that collapse each wrapped gap of the initializer to its canonical spacing.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="initializer">The initializer to collapse.</param>
    /// <param name="changes">The change set to append to.</param>
    private static void AppendCollapse(SourceText text, InitializerExpressionSyntax initializer, List<TextChange> changes)
    {
        var close = initializer.CloseBraceToken;
        var pending = new List<TextChange>(initializer.Expressions.Count + InitializerBraceGapCount);
        var token = initializer.OpenBraceToken.GetPreviousToken();
        while (!token.IsKind(SyntaxKind.None))
        {
            var next = token.GetNextToken();
            LayoutHelpers.ClassifyGap(text, token.Span.End, next.SpanStart, out var hasLineBreak, out var isClean);
            if (hasLineBreak && !isClean)
            {
                return;
            }

            if (hasLineBreak)
            {
                pending.Add(new(TextSpan.FromBounds(token.Span.End, next.SpanStart), next.IsKind(SyntaxKind.CommaToken) ? string.Empty : " "));
            }

            if (next.Equals(close))
            {
                break;
            }

            token = next;
        }

        changes.AddRange(pending);
    }
}
