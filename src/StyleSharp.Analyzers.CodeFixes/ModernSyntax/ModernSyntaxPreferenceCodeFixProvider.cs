// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Applies compact C# syntax preference fixes (SST2218-SST2219).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ModernSyntaxPreferenceCodeFixProvider))]
[Shared]
public sealed class ModernSyntaxPreferenceCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.UseImplicitLambdaParameterTypes.Id,
        ModernSyntaxRules.SimplifyPropertyAccessor.Id);

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

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            if (CreateReplacement(root, diagnostic, out _, out _) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title(diagnostic.Id),
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: diagnostic.Id),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var replacement = CreateReplacement(editor.OriginalRoot, diagnostic, out var oldNode, out _);
        if (oldNode is null || replacement is null)
        {
            return;
        }

        editor.ReplaceNode(oldNode, replacement);
    }

    /// <summary>Applies one diagnostic fix.</summary>
    /// <param name="document">The document.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        var replacement = CreateReplacement(root, diagnostic, out var oldNode, out _);
        return oldNode is null || replacement is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(oldNode, replacement));
    }

    /// <summary>Gets the code action title.</summary>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The title.</returns>
    private static string Title(string diagnosticId)
        => diagnosticId == ModernSyntaxRules.UseImplicitLambdaParameterTypes.Id
            ? "Remove lambda parameter types"
            : "Use expression-bodied accessor";

    /// <summary>Creates the replacement node for one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The old node.</param>
    /// <param name="replacement">The replacement node.</param>
    /// <returns>The replacement node, or <see langword="null"/>.</returns>
    private static SyntaxNode? CreateReplacement(
        SyntaxNode root,
        Diagnostic diagnostic,
        out SyntaxNode? oldNode,
        out SyntaxNode? replacement)
    {
        oldNode = null;
        replacement = null;
        if (diagnostic.Id == ModernSyntaxRules.UseImplicitLambdaParameterTypes.Id)
        {
            oldNode = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<ParenthesizedLambdaExpressionSyntax>();
            if (oldNode is ParenthesizedLambdaExpressionSyntax lambda && ModernSyntaxPreferenceAnalyzer.CanUseImplicitParameterTypes(lambda))
            {
                replacement = RemoveLambdaParameterTypes(lambda);
            }
        }
        else if (diagnostic.Id == ModernSyntaxRules.SimplifyPropertyAccessor.Id)
        {
            oldNode = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<AccessorDeclarationSyntax>();
            if (oldNode is AccessorDeclarationSyntax accessor
                && ModernSyntaxPreferenceAnalyzer.TryGetAccessorExpression(accessor, out var expression))
            {
                replacement = SimplifyAccessor(accessor, expression);
            }
        }

        return replacement;
    }

    /// <summary>Removes explicit parameter types from a lambda.</summary>
    /// <param name="lambda">The lambda.</param>
    /// <returns>The updated lambda.</returns>
    private static ParenthesizedLambdaExpressionSyntax RemoveLambdaParameterTypes(ParenthesizedLambdaExpressionSyntax lambda)
    {
        var parameters = lambda.ParameterList.Parameters;
        var parametersWithSeparators = parameters.GetWithSeparators();
        var rewritten = new SyntaxNodeOrToken[parametersWithSeparators.Count];
        for (var i = 0; i < parametersWithSeparators.Count; i++)
        {
            rewritten[i] = parametersWithSeparators[i].AsNode() is ParameterSyntax parameter
                ? parameter.WithType(null)
                : parametersWithSeparators[i];
        }

        return lambda.WithParameterList(lambda.ParameterList.WithParameters(SyntaxFactory.SeparatedList<ParameterSyntax>(rewritten)));
    }

    /// <summary>Rewrites an accessor body as an expression body.</summary>
    /// <param name="accessor">The accessor.</param>
    /// <param name="expression">The expression.</param>
    /// <returns>The updated accessor.</returns>
    private static AccessorDeclarationSyntax SimplifyAccessor(AccessorDeclarationSyntax accessor, ExpressionSyntax expression)
    {
        var trailingTrivia = accessor.Body?.CloseBraceToken.TrailingTrivia ?? accessor.SemicolonToken.TrailingTrivia;
        return accessor
            .WithBody(null)
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(expression.WithoutTrivia()))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(trailingTrivia));
    }
}
