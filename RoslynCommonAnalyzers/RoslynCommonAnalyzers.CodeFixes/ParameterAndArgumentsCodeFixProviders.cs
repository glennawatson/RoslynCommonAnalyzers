// Copyright (c) 2023 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommonAnalyzers;

#pragma warning disable SA1507
#pragma warning disable SA1518
#pragma warning disable SA1402
#pragma warning disable SA1649


/// <summary>
/// A code fix provider for the <see cref="RCGS0001ConstructorDeclarationParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0001ConstructorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public class RCGS0001ConstructorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0001ConstructorDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is BaseMethodDeclarationSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        token => Fix(context.Document, root, syntaxNode, token),
                        nameof(RCGS0001ConstructorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private static Task<Document> Fix(Document document, SyntaxNode root, BaseMethodDeclarationSyntax node, CancellationToken cancellationToken)
    {
        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList?.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.ParameterList(parameters).WithOpenParenToken(node.ParameterList!.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}


/// <summary>
/// A code fix provider for the <see cref="RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public class RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is BaseMethodDeclarationSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        token => Fix(context.Document, root, syntaxNode, token),
                        nameof(RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private static Task<Document> Fix(Document document, SyntaxNode root, BaseMethodDeclarationSyntax node, CancellationToken cancellationToken)
    {
        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList?.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.ParameterList(parameters).WithOpenParenToken(node.ParameterList!.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}


/// <summary>
/// A code fix provider for the <see cref="RCGS0003DelegateDeclarationParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0003DelegateDeclarationParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public class RCGS0003DelegateDeclarationParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0003DelegateDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is DelegateDeclarationSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        token => Fix(context.Document, root, syntaxNode, token),
                        nameof(RCGS0003DelegateDeclarationParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private static Task<Document> Fix(Document document, SyntaxNode root, DelegateDeclarationSyntax node, CancellationToken cancellationToken)
    {
        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList?.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.ParameterList(parameters).WithOpenParenToken(node.ParameterList!.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}


/// <summary>
/// A code fix provider for the <see cref="RCGS0004IndexerDeclarationParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0004IndexerDeclarationParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public class RCGS0004IndexerDeclarationParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0004IndexerDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is IndexerDeclarationSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        token => Fix(context.Document, root, syntaxNode, token),
                        nameof(RCGS0004IndexerDeclarationParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private static Task<Document> Fix(Document document, SyntaxNode root, IndexerDeclarationSyntax node, CancellationToken cancellationToken)
    {
        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList?.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.BracketedParameterList(parameters).WithOpenBracketToken(node.ParameterList!.OpenBracketToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}


/// <summary>
/// A code fix provider for the <see cref="RCGS0005InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0005InvocationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public class RCGS0005InvocationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0005InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is InvocationExpressionSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        token => Fix(context.Document, root, syntaxNode, token),
                        nameof(RCGS0005InvocationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private static Task<Document> Fix(Document document, SyntaxNode root, InvocationExpressionSyntax node, CancellationToken cancellationToken)
    {
        var newNode = node.ConvertNodeIfAble(
            node => node.ArgumentList?.Arguments,
            (node, parameters) => node.WithArgumentList(SyntaxFactory.ArgumentList(parameters).WithOpenParenToken(node.ArgumentList!.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}


/// <summary>
/// A code fix provider for the <see cref="RCGS0006ObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0006ObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public class RCGS0006ObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0006ObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is ObjectCreationExpressionSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        token => Fix(context.Document, root, syntaxNode, token),
                        nameof(RCGS0006ObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private static Task<Document> Fix(Document document, SyntaxNode root, ObjectCreationExpressionSyntax node, CancellationToken cancellationToken)
    {
        var newNode = node.ConvertNodeIfAble(
            node => node.ArgumentList?.Arguments,
            (node, parameters) => node.WithArgumentList(SyntaxFactory.ArgumentList(parameters).WithOpenParenToken(node.ArgumentList!.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}


/// <summary>
/// A code fix provider for the <see cref="RCGS0007ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0007ElementAccessExpressionArgumentMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public class RCGS0007ElementAccessExpressionArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0007ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is ElementAccessExpressionSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        token => Fix(context.Document, root, syntaxNode, token),
                        nameof(RCGS0007ElementAccessExpressionArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private static Task<Document> Fix(Document document, SyntaxNode root, ElementAccessExpressionSyntax node, CancellationToken cancellationToken)
    {
        var newNode = node.ConvertNodeIfAble(
            node => node.ArgumentList?.Arguments,
            (node, parameters) => node.WithArgumentList(SyntaxFactory.BracketedArgumentList(parameters).WithOpenBracketToken(node.ArgumentList!.OpenBracketToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}


/// <summary>
/// A code fix provider for the <see cref="RCGS0008AttributeArgumentMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0008AttributeArgumentMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public class RCGS0008AttributeArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0008AttributeArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is AttributeSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        token => Fix(context.Document, root, syntaxNode, token),
                        nameof(RCGS0008AttributeArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private static Task<Document> Fix(Document document, SyntaxNode root, AttributeSyntax node, CancellationToken cancellationToken)
    {
        var newNode = node.ConvertNodeIfAble(
            node => node.ArgumentList?.Arguments,
            (node, parameters) => node.WithArgumentList(SyntaxFactory.AttributeArgumentList(parameters).WithOpenParenToken(node.ArgumentList!.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}


/// <summary>
/// A code fix provider for the <see cref="RCGS0009AnonymousMethodExpressionParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0009AnonymousMethodExpressionParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public class RCGS0009AnonymousMethodExpressionParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0009AnonymousMethodExpressionParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is AnonymousMethodExpressionSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        token => Fix(context.Document, root, syntaxNode, token),
                        nameof(RCGS0009AnonymousMethodExpressionParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private static Task<Document> Fix(Document document, SyntaxNode root, AnonymousMethodExpressionSyntax node, CancellationToken cancellationToken)
    {
        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList?.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.ParameterList(parameters).WithOpenParenToken(node.ParameterList!.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}


/// <summary>
/// A code fix provider for the <see cref="RCGS0010ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesAnalyzer"/> analyzer.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0010ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesCodeFixProvider))]
[Shared]
public class RCGS0010ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0010ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node is ParenthesizedLambdaExpressionSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        token => Fix(context.Document, root, syntaxNode, token),
                        nameof(RCGS0010ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private static Task<Document> Fix(Document document, SyntaxNode root, ParenthesizedLambdaExpressionSyntax node, CancellationToken cancellationToken)
    {
        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList?.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.ParameterList(parameters).WithOpenParenToken(node.ParameterList!.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, newNode)));
    }
}

