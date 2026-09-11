// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces a reported symbol-name string literal with a <c>nameof</c> expression (SST1463), so the
/// argument follows the symbol through a rename.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1463NameofLiteralCodeFixProvider))]
[Shared]
public sealed class Sst1463NameofLiteralCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.UseNameofForSymbolName.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Use nameof", nameof(Sst1463NameofLiteralCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported literal and builds its <c>nameof</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        // A positional argument has the same span as its expression, so the tie has to resolve inward
        // or the argument comes back instead of the literal.
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
            || literal.Token.Value is not string { Length: > 0 } name
            || !SyntaxFacts.IsValidIdentifier(name))
        {
            return null;
        }

        var replacement = SyntaxFactory.ParseExpression($"nameof({name})");
        return replacement.ContainsDiagnostics
            ? null
            : new NodeReplacement(literal, replacement.WithTriviaFrom(literal));
    }
}
