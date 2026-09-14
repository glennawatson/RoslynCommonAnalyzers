// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces <c>Assembly.GetExecutingAssembly()</c> with <c>typeof(EnclosingType).Assembly</c>
/// (PSH1404), using the nearest enclosing type declaration's own name — including its type
/// parameters for generic types, which are valid inside the type. No fix is offered inside
/// top-level statements, where no declared type name is in scope.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1404PreferTypeofAssemblyCodeFixProvider))]
[Shared]
public sealed class Psh1404PreferTypeofAssemblyCodeFixProvider : CodeFixProvider
{
    /// <summary>The name of the assembly property read off the typeof expression.</summary>
    private const string AssemblyPropertyName = "Assembly";

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.PreferTypeofAssembly.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            static _ => nameof(Psh1404PreferTypeofAssemblyCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported assembly lookup and builds the <c>typeof(T).Assembly</c> access that replaces it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the call has no enclosing type to name.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetInvocation(root, diagnostic) is { } invocation && Rewrite(invocation) is { } replacement
            ? new NodeReplacement(invocation, replacement)
            : null;

    /// <summary>Returns the reported invocation when the diagnostic location covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? TryGetInvocation(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && Psh1404PreferTypeofAssemblyAnalyzer.IsGetExecutingAssemblyShape(invocation)
            ? invocation
            : null;

    /// <summary>Words the action with the enclosing type the replacement names.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when the call has no enclosing type.</returns>
    private static string? TryCreateTitle(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetInvocation(root, diagnostic)?.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } typeDeclaration
            ? $"Use typeof({Psh1404PreferTypeofAssemblyAnalyzer.GetEnclosingTypeDisplayName(typeDeclaration)}).Assembly"
            : null;

    /// <summary>Builds the <c>typeof(EnclosingType).Assembly</c> replacement for the reported invocation.</summary>
    /// <param name="invocation">The reported invocation.</param>
    /// <returns>The replacement expression, or <see langword="null"/> when no enclosing type declaration exists.</returns>
    private static MemberAccessExpressionSyntax? Rewrite(InvocationExpressionSyntax invocation)
    {
        if (invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } typeDeclaration)
        {
            return null;
        }

        var typeName = Psh1404PreferTypeofAssemblyAnalyzer.GetEnclosingTypeDisplayName(typeDeclaration);
        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.TypeOfExpression(
                SyntaxFactory.Token(invocation.GetLeadingTrivia(), SyntaxKind.TypeOfKeyword, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker)),
                SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                SyntaxFactory.ParseTypeName(typeName),
                SyntaxFactory.Token(SyntaxKind.CloseParenToken)),
            SyntaxFactory.Token(SyntaxKind.DotToken),
            SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker),
                AssemblyPropertyName,
                invocation.GetTrailingTrivia())));
    }
}
