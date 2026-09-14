// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Applies compact C# syntax preference fixes (SST2218-SST2219).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ModernSyntaxPreferenceCodeFixProvider))]
[Shared]
public sealed class ModernSyntaxPreferenceCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.UseImplicitLambdaParameterTypes.Id,
        ModernSyntaxRules.SimplifyPropertyAccessor.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            static diagnostic => Title(diagnostic.Id),
            static diagnostic => diagnostic.Id,
            CanRewrite,
            TryRewrite);

    /// <summary>Resolves the reported lambda or accessor and builds its simplified replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (diagnostic.Id == ModernSyntaxRules.UseImplicitLambdaParameterTypes.Id)
        {
            return FindAncestor<ParenthesizedLambdaExpressionSyntax>(root, diagnostic) is { } lambda
                && ModernSyntaxPreferenceAnalyzer.CanUseImplicitParameterTypes(lambda)
                ? new NodeReplacement(lambda, ModernSyntaxPreferenceAnalyzer.RemoveLambdaParameterTypes(lambda))
                : null;
        }

        if (diagnostic.Id == ModernSyntaxRules.SimplifyPropertyAccessor.Id)
        {
            return FindAncestor<AccessorDeclarationSyntax>(root, diagnostic) is { } accessor
                && ModernSyntaxPreferenceAnalyzer.TryGetAccessorExpression(accessor, out var expression)
                ? new NodeReplacement(accessor, SimplifyAccessor(accessor, expression))
                : null;
        }

        return null;
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
            return FindAncestor<ParenthesizedLambdaExpressionSyntax>(root, diagnostic) is { } lambda
                && ModernSyntaxPreferenceAnalyzer.CanUseImplicitParameterTypes(lambda);
        }

        if (diagnostic.Id == ModernSyntaxRules.SimplifyPropertyAccessor.Id)
        {
            return FindAncestor<AccessorDeclarationSyntax>(root, diagnostic) is { } accessor
                && ModernSyntaxPreferenceAnalyzer.TryGetAccessorExpression(accessor, out _);
        }

        return false;
    }

    /// <summary>Finds the nearest node of one type enclosing the token a diagnostic starts at.</summary>
    /// <typeparam name="T">The node type to find.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The enclosing node, or <see langword="null"/> when there is none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T? FindAncestor<T>(SyntaxNode root, Diagnostic diagnostic)
        where T : SyntaxNode =>
        root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<T>();

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
