// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported u8-built byte array field into a
/// <c>static ReadOnlySpan&lt;byte&gt;</c> expression-bodied property returning the literal
/// (PSH1013). The modifiers carry over minus <c>readonly</c>, which does not apply to
/// properties, and the span type is spelled fully qualified when the System import does not
/// make it resolve. The analyzer already proved every use still compiles as a span.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1013Utf8SpanPropertyCodeFixProvider))]
[Shared]
public sealed class Psh1013Utf8SpanPropertyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The simple name of the span type.</summary>
    private const string ReadOnlySpanTypeName = "ReadOnlySpan";

    /// <summary>The fully qualified spelling used when the simple name does not resolve.</summary>
    private const string QualifiedReadOnlySpanName = "global::System.ReadOnlySpan";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.UseUtf8SpanProperty.Id);

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
                    "Use a ReadOnlySpan<byte> property",
                    cancellationToken => Task.FromResult(
                        context.Document.WithSyntaxRoot(root.ReplaceNode(rewrite.Original, rewrite.Replacement))),
                    equivalenceKey: nameof(Psh1013Utf8SpanPropertyCodeFixProvider)),
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

    /// <summary>Resolves the reported field and builds its span property replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The original field and its replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static (FieldDeclarationSyntax Original, MemberDeclarationSyntax Replacement)? TryRewrite(
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<FieldDeclarationSyntax>() is not { } field
            || !Psh1013Utf8SpanPropertyAnalyzer.HasCandidateShape(field)
            || Psh1013Utf8SpanPropertyAnalyzer.TryGetUtf8Source(field.Declaration.Variables[0].Initializer!.Value) is not { } literal)
        {
            return null;
        }

        var spanSpelling = ResolvesReadOnlySpan(model, field.SpanStart) ? ReadOnlySpanTypeName : QualifiedReadOnlySpanName;
        var text = new StringBuilder();
        foreach (var modifier in field.Modifiers)
        {
            if (!modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
            {
                text.Append(modifier.Text).Append(' ');
            }
        }

        text.Append(spanSpelling).Append("<byte> ")
            .Append(field.Declaration.Variables[0].Identifier.Text)
            .Append(" => ").Append(literal.ToString()).Append(';');

        var property = SyntaxFactory.ParseMemberDeclaration(text.ToString());
        return property is null ? null : (field, property.WithTriviaFrom(field));
    }

    /// <summary>Returns whether the span type resolves by simple name at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <returns><see langword="true"/> when the simple spelling binds.</returns>
    private static bool ResolvesReadOnlySpan(SemanticModel model, int position)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: ReadOnlySpanTypeName))
        {
            if (candidate is INamedTypeSymbol { IsGenericType: true, ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } })
            {
                return true;
            }
        }

        return false;
    }
}
