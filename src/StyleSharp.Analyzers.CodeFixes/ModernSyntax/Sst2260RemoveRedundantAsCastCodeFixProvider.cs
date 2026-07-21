// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a redundant <c>as</c> cast (SST2260): <c>value as string</c> becomes <c>value</c>. The operand of an
/// <c>as</c> expression always binds at least as tightly as the <c>as</c> expression it replaces, so no
/// parentheses are ever needed; the expression's trivia carries through.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2260RemoveRedundantAsCastCodeFixProvider))]
[Shared]
public sealed class Sst2260RemoveRedundantAsCastCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.RemoveRedundantAsCast.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Remove the redundant 'as' cast", nameof(Sst2260RemoveRedundantAsCastCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported <c>as</c> expression and rewrites it to its operand.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } expression)
            {
                return new NodeReplacement(expression, expression.Left.WithTriviaFrom(expression));
            }
        }

        return null;
    }
}
