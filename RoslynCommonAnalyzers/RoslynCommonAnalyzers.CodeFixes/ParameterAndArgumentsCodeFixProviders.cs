namespace RoslynCommonAnalyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0001ParameterMustBeOnUniqueLinesCodeFixProvider)), Shared]
public class RCGS0001ParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0001ConstructorDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                        token => Fix(context.Document, root, syntaxNode),
                        nameof(RCGS0001ParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private async Task<Document> Fix(Document document, SyntaxNode root, BaseMethodDeclarationSyntax node, CancellationToken cancellationToken)
    {
        // Produce a new solution that has all references to that type renamed, including the declaration.
        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.ParameterList(parameters).WithOpenParenToken(node.ParameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;

    }
}
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0002ParameterMustBeOnUniqueLinesCodeFixProvider)), Shared]
public class RCGS0002ParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                        token => Fix(context.Document, root, syntaxNode),
                        nameof(RCGS0002ParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private async Task<Document> Fix(Document document, SyntaxNode root, BaseMethodDeclarationSyntax node, CancellationToken cancellationToken)
    {
        // Produce a new solution that has all references to that type renamed, including the declaration.
        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.ParameterList(parameters).WithOpenParenToken(node.ParameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;

    }
}
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0003ParameterMustBeOnUniqueLinesCodeFixProvider)), Shared]
public class RCGS0003ParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0003DelegateDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                        token => Fix(context.Document, root, syntaxNode),
                        nameof(RCGS0003ParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private async Task<Document> Fix(Document document, SyntaxNode root, DelegateDeclarationSyntax node, CancellationToken cancellationToken)
    {
        // Produce a new solution that has all references to that type renamed, including the declaration.
        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.ParameterList(parameters).WithOpenParenToken(node.ParameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;

    }
}
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0004ParameterMustBeOnUniqueLinesCodeFixProvider)), Shared]
public class RCGS0004ParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0004IndexerDeclarationParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                        token => Fix(context.Document, root, syntaxNode),
                        nameof(RCGS0004ParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private async Task<Document> Fix(Document document, SyntaxNode root, IndexerDeclarationSyntax node, CancellationToken cancellationToken)
    {
        // Produce a new solution that has all references to that type renamed, including the declaration.
        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.ParameterList(parameters).WithOpenParenToken(node.ParameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;

    }
}
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0005ArgumentMustBeOnUniqueLinesCodeFixProvider)), Shared]
public class RCGS0005ArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0005InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                        token => Fix(context.Document, root, syntaxNode),
                        nameof(RCGS0005ArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private async Task<Document> Fix(Document document, SyntaxNode root, InvocationExpressionSyntax node, CancellationToken cancellationToken)
    {
        // Produce a new solution that has all references to that type renamed, including the declaration.
        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        var newNode = node.ConvertNodeIfAble(
            node => node.ArgumentList.Arguments,
            (node, parameters) => node.WithArgumentList(SyntaxFactory.ArgumentList(parameters).WithOpenParenToken(node.ParameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;

    }
}
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0006ArgumentMustBeOnUniqueLinesCodeFixProvider)), Shared]
public class RCGS0006ArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0006ObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                        token => Fix(context.Document, root, syntaxNode),
                        nameof(RCGS0006ArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private async Task<Document> Fix(Document document, SyntaxNode root, ObjectCreationExpressionSyntax node, CancellationToken cancellationToken)
    {
        // Produce a new solution that has all references to that type renamed, including the declaration.
        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        var newNode = node.ConvertNodeIfAble(
            node => node.ArgumentList.Arguments,
            (node, parameters) => node.WithArgumentList(SyntaxFactory.ArgumentList(parameters).WithOpenParenToken(node.ParameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;

    }
}
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0007ArgumentMustBeOnUniqueLinesCodeFixProvider)), Shared]
public class RCGS0007ArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0007ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                        token => Fix(context.Document, root, syntaxNode),
                        nameof(RCGS0007ArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private async Task<Document> Fix(Document document, SyntaxNode root, ElementAccessExpressionSyntax node, CancellationToken cancellationToken)
    {
        // Produce a new solution that has all references to that type renamed, including the declaration.
        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        var newNode = node.ConvertNodeIfAble(
            node => node.ArgumentList.Arguments,
            (node, parameters) => node.WithArgumentList(SyntaxFactory.ArgumentList(parameters).WithOpenParenToken(node.ParameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;

    }
}
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0008ArgumentMustBeOnUniqueLinesCodeFixProvider)), Shared]
public class RCGS0008ArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0008ElementBindingExpressionArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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

            if (node is ElementBindingExpressionSyntax syntaxNode)
            {
                // In this case there is no justification at all
                context.RegisterCodeFix(
                    CodeAction.Create(
                        CodeFixResources.RCGS0001CodeFixTitle,
                        token => Fix(context.Document, root, syntaxNode),
                        nameof(RCGS0008ArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private async Task<Document> Fix(Document document, SyntaxNode root, ElementBindingExpressionSyntax node, CancellationToken cancellationToken)
    {
        // Produce a new solution that has all references to that type renamed, including the declaration.
        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        var newNode = node.ConvertNodeIfAble(
            node => node.ArgumentList.Arguments,
            (node, parameters) => node.WithArgumentList(SyntaxFactory.ArgumentList(parameters).WithOpenParenToken(node.ParameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;

    }
}
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0009ArgumentMustBeOnUniqueLinesCodeFixProvider)), Shared]
public class RCGS0009ArgumentMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0009AttributeArgumentMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                        token => Fix(context.Document, root, syntaxNode),
                        nameof(RCGS0009ArgumentMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private async Task<Document> Fix(Document document, SyntaxNode root, AttributeSyntax node, CancellationToken cancellationToken)
    {
        // Produce a new solution that has all references to that type renamed, including the declaration.
        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        var newNode = node.ConvertNodeIfAble(
            node => node.ArgumentList.Arguments,
            (node, parameters) => node.WithArgumentList(SyntaxFactory.ArgumentList(parameters).WithOpenParenToken(node.ParameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;

    }
}
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0010ParameterMustBeOnUniqueLinesCodeFixProvider)), Shared]
public class RCGS0010ParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0010AnonymousMethodExpressionParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                        token => Fix(context.Document, root, syntaxNode),
                        nameof(RCGS0010ParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private async Task<Document> Fix(Document document, SyntaxNode root, AnonymousMethodExpressionSyntax node, CancellationToken cancellationToken)
    {
        // Produce a new solution that has all references to that type renamed, including the declaration.
        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.ParameterList(parameters).WithOpenParenToken(node.ParameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;

    }
}
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RCGS0011ParameterMustBeOnUniqueLinesCodeFixProvider)), Shared]
public class RCGS0011ParameterMustBeOnUniqueLinesCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RCGS0011ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
                        token => Fix(context.Document, root, syntaxNode),
                        nameof(RCGS0011ParameterMustBeOnUniqueLinesCodeFixProvider) + "-Add"),
                    diagnostic);
                return;
            }
        }
    }

    private async Task<Document> Fix(Document document, SyntaxNode root, ParenthesizedLambdaExpressionSyntax node, CancellationToken cancellationToken)
    {
        // Produce a new solution that has all references to that type renamed, including the declaration.
        var originalSolution = document.Project.Solution;
        var optionSet = originalSolution.Workspace.Options;

        var newNode = node.ConvertNodeIfAble(
            node => node.ParameterList.Parameters,
            (node, parameters) => node.WithParameterList(SyntaxFactory.ParameterList(parameters).WithOpenParenToken(node.ParameterList.OpenParenToken.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))))
                ?? node;

    }
}
