// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a single-character string argument with the equivalent char literal
/// (PSH1201), dropping the now-redundant <c>StringComparison.Ordinal</c> argument for
/// <c>StartsWith</c>/<c>EndsWith</c>/<c>IndexOf</c>/<c>LastIndexOf</c> — the char
/// overloads are ordinal and take no comparison. Escaping is handled by
/// <see cref="SyntaxFactory.Literal(char)"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1201UseCharOverloadCodeFixProvider))]
[Shared]
public sealed class Psh1201UseCharOverloadCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseCharOverload.Id);

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
            if (!TryGetLiteral(root, diagnostic, out var literal))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use the char overload",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, literal!)),
                    equivalenceKey: nameof(Psh1201UseCharOverloadCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetLiteral(editor.OriginalRoot, diagnostic, out var literal)
            || literal!.Parent is not ArgumentSyntax { Parent: ArgumentListSyntax arguments })
        {
            return;
        }

        editor.ReplaceNode(arguments, Rewrite(arguments, literal));
    }

    /// <summary>Replaces the reported string argument with the char overload's argument list.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="literal">The reported single-character string literal.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, LiteralExpressionSyntax literal)
        => literal.Parent is ArgumentSyntax { Parent: ArgumentListSyntax arguments }
            ? document.WithSyntaxRoot(root.ReplaceNode(arguments, Rewrite(arguments, literal)))
            : document;

    /// <summary>Finds the reported single-character string literal for a diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="literal">The reported literal when found.</param>
    /// <returns><see langword="true"/> when the literal was found.</returns>
    private static bool TryGetLiteral(SyntaxNode root, Diagnostic diagnostic, out LiteralExpressionSyntax? literal)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is ExpressionSyntax expression
            && StringLiteralHelper.TryGetSingleCharacterLiteral(expression, out literal, out _))
        {
            return true;
        }

        literal = null;
        return false;
    }

    /// <summary>Builds the char-overload argument list: the char literal alone, comparison argument dropped.</summary>
    /// <param name="arguments">The original argument list.</param>
    /// <param name="literal">The reported single-character string literal.</param>
    /// <returns>The rewritten argument list.</returns>
    private static ArgumentListSyntax Rewrite(ArgumentListSyntax arguments, LiteralExpressionSyntax literal)
    {
        var charLiteral = SyntaxFactory.LiteralExpression(
            SyntaxKind.CharacterLiteralExpression,
            SyntaxFactory.Literal(literal.Token.ValueText[0])).WithTriviaFrom(literal);

        var firstArgument = arguments.Arguments[0].WithExpression(charLiteral);
        return arguments.WithArguments(SyntaxFactory.SingletonSeparatedList(firstArgument));
    }
}
