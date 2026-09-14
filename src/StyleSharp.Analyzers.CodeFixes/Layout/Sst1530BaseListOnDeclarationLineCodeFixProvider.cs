// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Joins a base list to the end of its type declaration line (SST1530).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1530BaseListOnDeclarationLineCodeFixProvider))]
[Shared]
public sealed class Sst1530BaseListOnDeclarationLineCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.BaseListOnDeclarationLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TextChangeCodeFix.RegisterAsync(
            context,
            static (root, diagnostic) => root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is BaseListSyntax ? "Join the base list to the declaration" : null,
            nameof(Sst1530BaseListOnDeclarationLineCodeFixProvider),
            RegisterTextChanges);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes) =>
        AppendChange(text, root, diagnostic.Location.SourceSpan, changes);

    /// <summary>Appends the single change that pulls the base list up onto the declaration line.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The base list span.</param>
    /// <param name="changes">The change set to append to.</param>
    private static void AppendChange(SourceText text, SyntaxNode root, TextSpan span, List<TextChange> changes)
    {
        if (root.FindToken(span.Start).Parent is not BaseListSyntax baseList)
        {
            return;
        }

        var colon = baseList.ColonToken;
        var previousEnd = colon.GetPreviousToken().Span.End;
        if (!LayoutFixHelpers.IsWhitespaceBetween(text, previousEnd, colon.SpanStart))
        {
            return;
        }

        changes.Add(new(TextSpan.FromBounds(previousEnd, colon.SpanStart), " "));
    }
}
