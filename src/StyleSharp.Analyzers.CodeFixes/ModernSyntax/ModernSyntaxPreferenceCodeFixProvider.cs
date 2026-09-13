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
            if (!CanRewrite(root, diagnostic))
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
    private static string Title(string diagnosticId) =>
        diagnosticId == ModernSyntaxRules.UseImplicitLambdaParameterTypes.Id
            ? "Remove lambda parameter types"
            : "Use expression-bodied accessor";

    /// <summary>Checks the reported shape without constructing its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the lambda or accessor can be simplified.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (diagnostic.Id == ModernSyntaxRules.UseImplicitLambdaParameterTypes.Id)
        {
            return root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<ParenthesizedLambdaExpressionSyntax>() is { } lambda
                && ModernSyntaxPreferenceAnalyzer.CanUseImplicitParameterTypes(lambda);
        }

        if (diagnostic.Id == ModernSyntaxRules.SimplifyPropertyAccessor.Id)
        {
            return root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<AccessorDeclarationSyntax>() is { } accessor
                && ModernSyntaxPreferenceAnalyzer.TryGetAccessorExpression(accessor, out _);
        }

        return false;
    }

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
        var parametersWithSeparators = lambda.ParameterList.Parameters.GetWithSeparators();
        var rewritten = new SyntaxNodeOrToken[parametersWithSeparators.Count];
        for (var i = 0; i < parametersWithSeparators.Count; i++)
        {
            rewritten[i] = parametersWithSeparators[i].AsNode() is ParameterSyntax parameter
                ? parameter.Update(parameter.AttributeLists, parameter.Modifiers, type: null, parameter.Identifier, parameter.Default)
                : parametersWithSeparators[i];
        }

        var parameterList = lambda.ParameterList.Update(
            lambda.ParameterList.OpenParenToken,
            SyntaxFactory.SeparatedList<ParameterSyntax>(rewritten),
            lambda.ParameterList.CloseParenToken);
        return lambda.Update(
            lambda.AttributeLists,
            lambda.Modifiers,
            lambda.ReturnType,
            parameterList,
            lambda.ArrowToken,
            lambda.Block,
            lambda.ExpressionBody);
    }

    /// <summary>Rewrites an accessor body as an expression body.</summary>
    /// <param name="accessor">The accessor.</param>
    /// <param name="expression">The expression.</param>
    /// <returns>The updated accessor.</returns>
    private static AccessorDeclarationSyntax SimplifyAccessor(AccessorDeclarationSyntax accessor, ExpressionSyntax expression)
    {
        var trailingTrivia = accessor.Body?.CloseBraceToken.TrailingTrivia ?? accessor.SemicolonToken.TrailingTrivia;
        return accessor.Update(
            accessor.AttributeLists,
            accessor.Modifiers,
            accessor.Keyword,
            body: null,
            SyntaxFactory.ArrowExpressionClause(expression.WithoutTrivia()),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.SemicolonToken, trailingTrivia));
    }
}
