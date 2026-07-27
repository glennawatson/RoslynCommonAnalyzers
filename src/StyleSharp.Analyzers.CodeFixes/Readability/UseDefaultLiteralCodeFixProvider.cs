// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces <c>default(T)</c> with the bare <c>default</c> literal (SST1188).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseDefaultLiteralCodeFixProvider))]
[Shared]
public sealed class UseDefaultLiteralCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseDefaultLiteral.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use the 'default' literal", nameof(UseDefaultLiteralCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported node and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not DefaultExpressionSyntax defaultExpression)
        {
            return null;
        }

        var literal = SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression, SyntaxFactory.Token(SyntaxKind.DefaultKeyword))
            .WithTriviaFrom(defaultExpression);

        return new NodeReplacement(defaultExpression, literal);
    }
}
