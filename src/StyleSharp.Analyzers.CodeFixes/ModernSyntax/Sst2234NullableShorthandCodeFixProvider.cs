// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites an explicit <c>Nullable&lt;T&gt;</c> spelling as the <c>T?</c> shorthand (SST2234).
/// When the spelling is qualified (<c>System.Nullable&lt;int&gt;</c>) the whole qualified name is
/// replaced so no dangling qualifier remains.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2234NullableShorthandCodeFixProvider))]
[Shared]
public sealed class Sst2234NullableShorthandCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.UseNullableShorthand.Id);

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
            if (!TryGetSpelling(root, diagnostic, out var spelling, out var argument))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use the T? shorthand",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, spelling!, argument!)),
                    equivalenceKey: nameof(Sst2234NullableShorthandCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetSpelling(editor.OriginalRoot, diagnostic, out var spelling, out var argument))
        {
            return;
        }

        editor.ReplaceNode(spelling!, BuildShorthand(spelling!, argument!));
    }

    /// <summary>Rewrites the reported spelling in the document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="spelling">The full name node to replace.</param>
    /// <param name="argument">The nullable value type argument.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, TypeSyntax spelling, TypeSyntax argument)
        => document.WithSyntaxRoot(root.ReplaceNode(spelling, BuildShorthand(spelling, argument)));

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
    private static NullableTypeSyntax BuildShorthand(TypeSyntax spelling, TypeSyntax argument)
        => SyntaxFactory.NullableType(argument.WithoutTrivia()).WithTriviaFrom(spelling);
}
