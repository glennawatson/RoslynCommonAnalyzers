// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces a <c>null</c> returned where a collection is declared (SST2306) with the empty collection
/// the analyzer worked out for that return type.
/// </summary>
/// <remarks>
/// The replacement text rides on the diagnostic's properties, and the fix will not offer it until it has
/// been proven to compile at that exact spot: the parsed expression is speculatively bound against the
/// document's semantic model, so a suggestion naming an API this compilation does not have — or a name
/// this file has not imported — is dropped rather than written. An empty collection expression has no
/// API to bind and is gated on the language version instead. A diagnostic that carries no replacement
/// (a return type with no provable empty value) is reported and left for the reader.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2306ReturnEmptyCollectionNotNullCodeFixProvider))]
[Shared]
public sealed class Sst2306ReturnEmptyCollectionNotNullCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DesignRules.ReturnEmptyCollectionNotNull.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        SemanticModel? model = null;
        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryGetReplacementSource(root, diagnostic, out var nullLiteral, out var replacementText))
            {
                continue;
            }

            if (replacementText == "[]")
            {
                if (!Sst2306ReturnEmptyCollectionNotNullAnalyzer.SupportsCollectionExpressions(nullLiteral.SyntaxTree))
                {
                    continue;
                }
            }
            else
            {
                // Named replacements still need speculative binding: a visible type does not prove
                // that the suggested expression resolves in this scope.
                model ??= await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                if (model is null || !Compiles(model, nullLiteral, SyntaxFactory.ParseExpression(replacementText)))
                {
                    continue;
                }
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Return an empty collection",
                    cancellationToken =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return Task.FromResult(Apply(context.Document, root, nullLiteral, replacementText));
                    },
                    nameof(Sst2306ReturnEmptyCollectionNotNullCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces one reported null with the analyzer's empty-collection expression.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="nullLiteral">The reported null literal.</param>
    /// <param name="replacementText">The empty-collection expression the analyzer suggested.</param>
    /// <returns>The updated document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Document Apply(Document document, SyntaxNode root, ExpressionSyntax nullLiteral, string replacementText) =>
        document.WithSyntaxRoot(root.ReplaceNode(nullLiteral, CreateReplacement(nullLiteral, SyntaxFactory.ParseExpression(replacementText))));

    /// <summary>Resolves the null literal and replacement text without parsing the replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="nullLiteral">The reported null literal.</param>
    /// <param name="replacementText">The suggested empty-collection expression.</param>
    /// <returns>Whether the diagnostic still has a null literal and a replacement.</returns>
    private static bool TryGetReplacementSource(SyntaxNode root, Diagnostic diagnostic, out ExpressionSyntax nullLiteral, out string replacementText)
    {
        nullLiteral = null!;
        replacementText = string.Empty;
        if (!diagnostic.Properties.TryGetValue(Sst2306ReturnEmptyCollectionNotNullAnalyzer.ReplacementKey, out var text)
            || text is not { Length: > 0 }
            || root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax expression
            || !ExpressionShapes.IsNullLiteral(expression))
        {
            return false;
        }

        nullLiteral = expression;
        replacementText = text;
        return true;
    }

    /// <summary>Resolves the reported null literal, proves the replacement compiles there, and builds the swap.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The document's semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches or the replacement would not bind.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (!TryGetReplacementSource(root, diagnostic, out var nullLiteral, out var replacementText))
        {
            return null;
        }

        var replacement = SyntaxFactory.ParseExpression(replacementText);
        return !Compiles(model, nullLiteral, replacement) ? null : new NodeReplacement(nullLiteral, CreateReplacement(nullLiteral, replacement));
    }

    /// <summary>Returns whether the replacement expression really compiles where the null is being removed.</summary>
    /// <param name="model">The document's semantic model.</param>
    /// <param name="nullLiteral">The reported null literal.</param>
    /// <param name="replacement">The parsed replacement expression.</param>
    /// <returns><see langword="true"/> when the expression binds at that position.</returns>
    /// <remarks>
    /// A collection expression names nothing and so has nothing to bind; the language version that
    /// introduced it is the only thing that can make it fail, and that is what is checked. Everything
    /// else names a type — <c>Array</c>, <c>List&lt;T&gt;</c>, <c>HashSet&lt;T&gt;</c> — and is
    /// speculatively bound, which is what proves both that the compilation has the API and that this file
    /// can reach it by the name being written.
    /// </remarks>
    private static bool Compiles(SemanticModel model, ExpressionSyntax nullLiteral, ExpressionSyntax replacement)
    {
        if (replacement is CollectionExpressionSyntax)
        {
            return Sst2306ReturnEmptyCollectionNotNullAnalyzer.SupportsCollectionExpressions(nullLiteral.SyntaxTree);
        }

        var type = model.GetSpeculativeTypeInfo(nullLiteral.SpanStart, replacement, SpeculativeBindingOption.BindAsExpression).Type;
        return type is not null && type.TypeKind != TypeKind.Error;
    }

    /// <summary>Carries the null literal's trivia onto its replacement.</summary>
    /// <param name="nullLiteral">The reported null literal.</param>
    /// <param name="replacement">The parsed replacement expression.</param>
    /// <returns>The replacement expression, annotated for formatting.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionSyntax CreateReplacement(ExpressionSyntax nullLiteral, ExpressionSyntax replacement) =>
        replacement
            .WithTriviaFrom(nullLiteral)
            .WithAdditionalAnnotations(Formatter.Annotation);
}
