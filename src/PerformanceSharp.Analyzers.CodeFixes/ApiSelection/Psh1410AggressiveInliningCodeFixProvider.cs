// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Adds <c>[MethodImpl(MethodImplOptions.AggressiveInlining)]</c> to a reported forwarder
/// (PSH1410). The attribute goes on its own line above the member and takes over the member's
/// leading trivia — its doc comment and any surrounding directives move, rather than being
/// copied, so a member that already carries an attribute keeps exactly one of each. The
/// attribute is always spelled fully qualified, so the same member gets the same syntax in
/// every compilation of the file.
/// </summary>
/// <remarks>
/// The spelling is unconditional on purpose. A multi-targeted project compiles one linked file
/// once per framework, and Roslyn has to reconcile the results into a single document; where
/// they differ it writes conflict markers into the source instead. Anything that decides the
/// spelling per compilation diverges — the semantic model because what binds depends on the
/// framework, and the using directives because a <c>using</c> inside an <c>#if</c> is a node in
/// one compilation and inactive text in another. The qualified form needs no import, so it is
/// correct everywhere and identical everywhere.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1410AggressiveInliningCodeFixProvider))]
[Shared]
public sealed class Psh1410AggressiveInliningCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The attribute text, always fully qualified so every compilation emits the same syntax.</summary>
    private const string AttributeText =
        "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.InlineTrivialForwarders.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Add AggressiveInlining", nameof(Psh1410AggressiveInliningCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported member and builds it with the attribute prepended.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>() is not { } declaration
            || !Psh1410AggressiveInliningAnalyzer.IsEligibleForwarder(declaration))
        {
            return null;
        }

        var leading = declaration.GetLeadingTrivia();
        var attributeList = ((MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration($"{AttributeText} void P();")!).AttributeLists[0]
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(LineEndingHelper.GetLineBreak(declaration), GetIndentation(leading));

        // The lists are taken from the already-stripped member, not the original. A member that already
        // carries an attribute holds its doc comment and any directives on that first list, so inserting
        // ahead of the original lists would leave a second copy of both above the one this fix writes.
        var stripped = declaration.WithLeadingTrivia(default(SyntaxTriviaList));
        return new NodeReplacement(declaration, stripped.WithAttributeLists(stripped.AttributeLists.Insert(0, attributeList)));
    }

    /// <summary>Returns the indentation whitespace at the end of a member's leading trivia.</summary>
    /// <param name="leading">The member's leading trivia.</param>
    /// <returns>The indentation trivia, or elastic space when none.</returns>
    private static SyntaxTrivia GetIndentation(SyntaxTriviaList leading)
        => leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? leading[leading.Count - 1]
            : SyntaxFactory.Whitespace(string.Empty);
}
