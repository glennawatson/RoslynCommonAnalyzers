// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Swaps a single-character string argument of <c>StringBuilder.Append</c>/<c>Insert</c>
/// for the equivalent char literal (PSH1202). Escaping is handled by
/// <see cref="SyntaxFactory.Literal(char)"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1202StringBuilderAppendCharCodeFixProvider))]
[Shared]
public sealed class Psh1202StringBuilderAppendCharCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.StringBuilderAppendChar.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Use the char overload", nameof(Psh1202StringBuilderAppendCharCodeFixProvider), CanRewrite, TryRewrite);

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        SingleCharacterLiteralFix.TryFind(root, diagnostic, out var _);

    /// <summary>Resolves the reported string literal and builds its char literal replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        SingleCharacterLiteralFix.TryFind(root, diagnostic, out var literal)
            ? new NodeReplacement(literal!, SingleCharacterLiteralFix.ToCharacterLiteral(literal!))
            : null;
}
