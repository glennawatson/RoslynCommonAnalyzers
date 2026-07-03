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
public sealed class Psh1404PreferTypeofAssemblyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The name of the assembly property read off the typeof expression.</summary>
    private const string AssemblyPropertyName = "Assembly";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.PreferTypeofAssembly.Id);

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
            if (TryGetInvocation(root, diagnostic) is not { } invocation
                || invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } typeDeclaration)
            {
                continue;
            }

            var typeName = Psh1404PreferTypeofAssemblyAnalyzer.GetEnclosingTypeDisplayName(typeDeclaration);
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use typeof({typeName}).Assembly",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, invocation)),
                    equivalenceKey: nameof(Psh1404PreferTypeofAssemblyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetInvocation(editor.OriginalRoot, diagnostic) is not { } invocation
            || Rewrite(invocation) is not { } replacement)
        {
            return;
        }

        editor.ReplaceNode(invocation, replacement);
    }

    /// <summary>Replaces the reported invocation with its <c>typeof(EnclosingType).Assembly</c> form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The reported invocation to rewrite.</param>
    /// <returns>The updated document, unchanged when no enclosing type declaration exists.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        => Rewrite(invocation) is { } replacement
            ? document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement))
            : document;

    /// <summary>Returns the reported invocation when the diagnostic location covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? TryGetInvocation(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && Psh1404PreferTypeofAssemblyAnalyzer.IsGetExecutingAssemblyShape(invocation)
            ? invocation
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
            SyntaxFactory.TypeOfExpression(SyntaxFactory.ParseTypeName(typeName)),
            SyntaxFactory.IdentifierName(AssemblyPropertyName)).WithTriviaFrom(invocation);
    }
}
