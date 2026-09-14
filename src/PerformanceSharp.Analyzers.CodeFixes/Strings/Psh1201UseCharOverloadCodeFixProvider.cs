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
public sealed class Psh1201UseCharOverloadCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseCharOverload.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Use the char overload", nameof(Psh1201UseCharOverloadCodeFixProvider), CanRewrite, TryRewrite);

    /// <summary>Replaces the reported string argument with the char overload's argument list.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="literal">The reported single-character string literal.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, LiteralExpressionSyntax literal) =>
        literal.Parent is ArgumentSyntax { Parent: ArgumentListSyntax arguments }
            ? document.WithSyntaxRoot(root.ReplaceNode(arguments, Rewrite(arguments, literal)))
            : document;

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        SingleCharacterLiteralFix.TryFind(root, diagnostic, out var literal)
            && literal!.Parent is ArgumentSyntax { Parent: ArgumentListSyntax };

    /// <summary>Resolves the reported string argument and builds the char overload's argument list.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        SingleCharacterLiteralFix.TryFind(root, diagnostic, out var literal)
            && literal!.Parent is ArgumentSyntax { Parent: ArgumentListSyntax arguments }
            ? new NodeReplacement(arguments, Rewrite(arguments, literal))
            : null;

    /// <summary>Builds the char-overload argument list: the char literal alone, comparison argument dropped.</summary>
    /// <param name="arguments">The original argument list.</param>
    /// <param name="literal">The reported single-character string literal.</param>
    /// <returns>The rewritten argument list.</returns>
    private static ArgumentListSyntax Rewrite(ArgumentListSyntax arguments, LiteralExpressionSyntax literal)
    {
        var firstArgument = arguments.Arguments[0].WithExpression(SingleCharacterLiteralFix.ToCharacterLiteral(literal));
        return arguments.WithArguments(SyntaxFactory.SingletonSeparatedList(firstArgument));
    }
}
