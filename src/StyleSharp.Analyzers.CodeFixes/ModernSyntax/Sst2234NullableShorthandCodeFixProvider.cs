// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites an explicit <c>Nullable&lt;T&gt;</c> spelling as the <c>T?</c> shorthand (SST2234).
/// When the spelling is qualified (<c>System.Nullable&lt;int&gt;</c>) the whole qualified name is
/// replaced so no dangling qualifier remains.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2234NullableShorthandCodeFixProvider))]
[Shared]
public sealed class Sst2234NullableShorthandCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseNullableShorthand.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            static (root, diagnostic) => TryGetSpelling(root, diagnostic, out _, out _) ? "Use the T? shorthand" : null,
            static _ => nameof(Sst2234NullableShorthandCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported spelling and builds its shorthand replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the spelling was not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetSpelling(root, diagnostic, out var spelling, out var argument)
            ? new NodeReplacement(spelling!, BuildShorthand(spelling!, argument!))
            : null;

    /// <summary>Resolves the diagnostic to the replaceable spelling and its type argument.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="spelling">The outermost name node to replace.</param>
    /// <param name="argument">The nullable value type argument.</param>
    /// <returns><see langword="true"/> when the spelling was found.</returns>
    private static bool TryGetSpelling(SyntaxNode root, Diagnostic diagnostic, out TypeSyntax? spelling, out TypeSyntax? argument)
    {
        spelling = null;
        argument = null;
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<GenericNameSyntax>() is not { TypeArgumentList.Arguments: { Count: 1 } arguments } generic)
        {
            return false;
        }

        TypeSyntax outermost = generic;
        while (outermost.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax)
        {
            outermost = (TypeSyntax)outermost.Parent;
        }

        spelling = outermost;
        argument = arguments[0];
        return true;
    }

    /// <summary>Builds the <c>T?</c> shorthand carrying the original spelling's trivia.</summary>
    /// <param name="spelling">The name node being replaced.</param>
    /// <param name="argument">The nullable value type argument.</param>
    /// <returns>The shorthand type syntax.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NullableTypeSyntax BuildShorthand(TypeSyntax spelling, TypeSyntax argument)
    {
        var elementType = argument.WithoutTrivia();
        return SyntaxFactory.NullableType(
            elementType.WithLeadingTrivia(spelling.GetLeadingTrivia()),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.QuestionToken, spelling.GetTrailingTrivia()));
    }
}
