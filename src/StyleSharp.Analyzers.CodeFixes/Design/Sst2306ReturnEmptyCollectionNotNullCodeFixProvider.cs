// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
public sealed class Sst2306ReturnEmptyCollectionNotNullCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DesignRules.ReturnEmptyCollectionNotNull.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Return an empty collection",
            nameof(Sst2306ReturnEmptyCollectionNotNullCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces one reported null with the analyzer's empty-collection expression.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="nullLiteral">The reported null literal.</param>
    /// <param name="replacementText">The empty-collection expression the analyzer suggested.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ExpressionSyntax nullLiteral, string replacementText)
        => document.WithSyntaxRoot(root.ReplaceNode(nullLiteral, CreateReplacement(nullLiteral, SyntaxFactory.ParseExpression(replacementText))));

    /// <summary>Resolves the reported null literal, proves the replacement compiles there, and builds the swap.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The document's semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches or the replacement would not bind.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (!diagnostic.Properties.TryGetValue(Sst2306ReturnEmptyCollectionNotNullAnalyzer.ReplacementKey, out var replacementText)
            || replacementText is not { Length: > 0 }
            || root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax nullLiteral
            || !Sst2306ReturnEmptyCollectionNotNullAnalyzer.IsNullLiteral(nullLiteral))
        {
            return null;
        }

        var replacement = SyntaxFactory.ParseExpression(replacementText);
        if (!Compiles(model, nullLiteral, replacement))
        {
            return null;
        }

        return new NodeReplacement(nullLiteral, CreateReplacement(nullLiteral, replacement));
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
    private static ExpressionSyntax CreateReplacement(ExpressionSyntax nullLiteral, ExpressionSyntax replacement)
        => replacement
            .WithTriviaFrom(nullLiteral)
            .WithAdditionalAnnotations(Formatter.Annotation);
}
