// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Names the created type in a target-typed object creation (SST2254), rewriting <c>new(...)</c> to
/// <c>new Type(...)</c> while keeping the arguments, the initializer, and the surrounding trivia.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2254ExplicitObjectCreationTypeCodeFixProvider))]
[Shared]
public sealed class Sst2254ExplicitObjectCreationTypeCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(ModernSyntaxRules.UseExplicitObjectCreationType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Name the created type explicitly",
            nameof(Sst2254ExplicitObjectCreationTypeCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the target-typed creation and rewrites it to an explicitly-typed creation.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no safe rewrite exists.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<ImplicitObjectCreationExpressionSyntax>() is not { } creation)
        {
            return null;
        }

        var typeInfo = model.GetTypeInfo(creation);
        var type = typeInfo.Type ?? typeInfo.ConvertedType;
        if (!Sst2254ExplicitObjectCreationTypeAnalyzer.IsExpressibleTypeName(type))
        {
            return null;
        }

        var typeName = type!.ToMinimalDisplayString(model, creation.SpanStart);
        var replacement = SyntaxFactory.ObjectCreationExpression(
                creation.NewKeyword.WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.ParseTypeName(typeName),
                creation.ArgumentList,
                creation.Initializer)
            .WithTriviaFrom(creation);

        return new NodeReplacement(creation, replacement);
    }
}
