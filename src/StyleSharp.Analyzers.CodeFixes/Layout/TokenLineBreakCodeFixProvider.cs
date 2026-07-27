// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves the single line break to the configured side of a wrapped operator token: a binary operator
/// (SST1526), an expression-body <c>=&gt;</c> (SST1527), or a wrapped initializer <c>=</c> (SST1528).
/// The target side rides on the diagnostic's <see cref="LayoutHelpers.BreakBeforeProperty"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TokenLineBreakCodeFixProvider))]
[Shared]
public sealed class TokenLineBreakCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.BinaryOperatorNewLine.Id,
        LayoutRules.ArrowTokenNewLine.Id,
        LayoutRules.EqualsTokenNewLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => TextChangeCodeFix.RegisterAsync(context, "Move the line break to the other side", nameof(TokenLineBreakCodeFixProvider), TryAppendChanges);

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => TryAppendChanges(text, root, diagnostic, changes);

    /// <summary>Appends the break-moving changes when the token carries exactly one break on the wrong side.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The change set to append to.</param>
    /// <returns><see langword="true"/> when changes were appended.</returns>
    private static bool TryAppendChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        var breakBefore = LayoutHelpers.HasLineBreakBefore(token);
        var breakAfter = LayoutHelpers.HasLineBreakAfter(token);
        if (breakBefore == breakAfter)
        {
            return false;
        }

        var wantBreakBefore = diagnostic.Properties.TryGetValue(LayoutHelpers.BreakBeforeProperty, out var value) && value == "true";
        if (wantBreakBefore ? !breakAfter : !breakBefore)
        {
            return false;
        }

        return LayoutFixHelpers.TryAppendTokenBreakMove(text, token, breakBefore, wantBreakBefore, LayoutFixHelpers.DetectNewLine(text), changes);
    }
}
