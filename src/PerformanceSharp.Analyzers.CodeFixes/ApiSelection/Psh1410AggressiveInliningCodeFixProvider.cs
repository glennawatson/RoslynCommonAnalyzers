// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Adds <c>[MethodImpl(MethodImplOptions.AggressiveInlining)]</c> to a reported forwarder
/// (PSH1410). The attribute goes on its own line above the member, taking over the member's
/// leading trivia, and is spelled fully qualified when the System.Runtime.CompilerServices
/// import does not make the short form resolve.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1410AggressiveInliningCodeFixProvider))]
[Shared]
public sealed class Psh1410AggressiveInliningCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The short attribute text.</summary>
    private const string SimpleAttributeText = "[MethodImpl(MethodImplOptions.AggressiveInlining)]";

    /// <summary>The fully qualified attribute text.</summary>
    private const string QualifiedAttributeText =
        "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";

    /// <summary>The simple name looked up to pick the spelling.</summary>
    private const string MethodImplAttributeName = "MethodImplAttribute";

    /// <summary>The namespace the short spelling requires.</summary>
    private const string CompilerServicesNamespace = "System.Runtime.CompilerServices";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.InlineTrivialForwarders.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (TryRewrite(root, model, diagnostic) is not { } rewrite)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add AggressiveInlining",
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(rewrite.Original, rewrite.Replacement))),
                    equivalenceKey: nameof(Psh1410AggressiveInliningCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryRewrite(editor.OriginalRoot, editor.SemanticModel, diagnostic) is not { } rewrite)
        {
            return;
        }

        editor.ReplaceNode(rewrite.Original, rewrite.Replacement);
    }

    /// <summary>Resolves the reported member and builds it with the attribute prepended.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The original member and its replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static (BaseMethodDeclarationSyntax Original, BaseMethodDeclarationSyntax Replacement)? TryRewrite(
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>() is not { } declaration
            || !Psh1410AggressiveInliningAnalyzer.IsEligibleForwarder(declaration))
        {
            return null;
        }

        var text = ResolvesMethodImpl(model, declaration.SpanStart) ? SimpleAttributeText : QualifiedAttributeText;
        var leading = declaration.GetLeadingTrivia();
        var attributeList = ((MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration($"{text} void P();")!).AttributeLists[0]
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"), GetIndentation(leading));

        var replacement = declaration
            .WithLeadingTrivia(default(SyntaxTriviaList))
            .WithAttributeLists(declaration.AttributeLists.Insert(0, attributeList));
        return (declaration, replacement);
    }

    /// <summary>Returns the indentation whitespace at the end of a member's leading trivia.</summary>
    /// <param name="leading">The member's leading trivia.</param>
    /// <returns>The indentation trivia, or elastic space when none.</returns>
    private static SyntaxTrivia GetIndentation(SyntaxTriviaList leading)
        => leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? leading[leading.Count - 1]
            : SyntaxFactory.Whitespace(string.Empty);

    /// <summary>Returns whether the attribute type resolves by short name at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <returns><see langword="true"/> when the short spelling binds.</returns>
    private static bool ResolvesMethodImpl(SemanticModel model, int position)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: MethodImplAttributeName))
        {
            if (candidate is INamedTypeSymbol named && named.ContainingNamespace.ToDisplayString() == CompilerServicesNamespace)
            {
                return true;
            }
        }

        return false;
    }
}
