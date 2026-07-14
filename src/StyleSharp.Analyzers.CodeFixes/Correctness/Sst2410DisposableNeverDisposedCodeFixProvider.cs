// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Turns a reported local declaration into a <c>using</c> declaration (SST2410), so the value is
/// disposed when its scope ends. A type that is only asynchronously disposable becomes an
/// <c>await using</c> declaration, and only inside an <c>async</c> body — where <c>await</c> would not
/// compile, no fix is offered rather than a broken one.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2410DisposableNeverDisposedCodeFixProvider))]
[Shared]
public sealed class Sst2410DisposableNeverDisposedCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.DisposableNeverDisposed.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Dispose with a using declaration", nameof(Sst2410DisposableNeverDisposedCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported local and builds its using-declaration replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no fix compiles here.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (TryGetStatement(root, diagnostic) is not { } statement
            || RequiresAwait(model, statement) is not { } needsAwait)
        {
            return null;
        }

        var replacement = Rewrite(statement, needsAwait);
        return new NodeReplacement(statement, replacement, current => Rewrite((LocalDeclarationStatementSyntax)current, needsAwait));
    }

    /// <summary>Finds the reported local declaration, and only one this fix can safely rewrite.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The declaration to rewrite, or <see langword="null"/> when the shape no longer matches.</returns>
    private static LocalDeclarationStatementSyntax? TryGetStatement(SyntaxNode root, Diagnostic diagnostic)
    {
        // A using declaration covers every variable it declares, and cannot carry another modifier,
        // so only a single-variable, unmodified declaration is rewritten.
        return root.FindNode(diagnostic.Location.SourceSpan) is VariableDeclaratorSyntax declarator
            && declarator.Parent?.Parent is LocalDeclarationStatementSyntax statement
            && statement.UsingKeyword.IsKind(SyntaxKind.None)
            && statement.Modifiers.Count == 0
            && statement.Declaration.Variables.Count == 1
                ? statement
                : null;
    }

    /// <summary>Decides which disposal keyword the local needs, if one compiles here at all.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="statement">The reported local declaration.</param>
    /// <returns>
    /// <see langword="true"/> for <c>await using</c>, <see langword="false"/> for <c>using</c>, or
    /// <see langword="null"/> when the type is only asynchronously disposable outside an async body.
    /// </returns>
    private static bool? RequiresAwait(SemanticModel model, LocalDeclarationStatementSyntax statement)
    {
        var initializer = statement.Declaration.Variables[0].Initializer;
        if (initializer is null || model.GetTypeInfo(initializer.Value).Type is not { } created)
        {
            return null;
        }

        var asyncDisposable = model.Compilation.GetTypeByMetadataName("System.IAsyncDisposable");
        if (asyncDisposable is not null && Implements(created, asyncDisposable) && IsInAsyncBody(statement))
        {
            return true;
        }

        var disposable = model.Compilation.GetTypeByMetadataName("System.IDisposable");
        return disposable is not null && Implements(created, disposable) ? false : null;
    }

    /// <summary>Returns whether a type implements the given interface.</summary>
    /// <param name="type">The created type.</param>
    /// <param name="interfaceType">The disposal interface.</param>
    /// <returns><see langword="true"/> when the type implements it.</returns>
    private static bool Implements(ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], interfaceType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the statement sits in a body where <c>await</c> compiles.</summary>
    /// <param name="statement">The reported local declaration.</param>
    /// <returns><see langword="true"/> when the nearest enclosing body is async.</returns>
    private static bool IsInAsyncBody(SyntaxNode statement)
    {
        for (var node = statement.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case AnonymousFunctionExpressionSyntax anonymous:
                    return !anonymous.AsyncKeyword.IsKind(SyntaxKind.None);
                case LocalFunctionStatementSyntax local:
                    return local.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case BaseMethodDeclarationSyntax method:
                    return method.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case AccessorDeclarationSyntax accessor:
                    return accessor.Modifiers.Any(SyntaxKind.AsyncKeyword);
                default:
                    continue;
            }
        }

        return false;
    }

    /// <summary>Rewrites the local as a using declaration, keeping the statement's leading trivia in front.</summary>
    /// <param name="statement">The local declaration to rewrite.</param>
    /// <param name="needsAwait">Whether the declaration must be awaited.</param>
    /// <returns>The rewritten declaration.</returns>
    private static LocalDeclarationStatementSyntax Rewrite(LocalDeclarationStatementSyntax statement, bool needsAwait)
    {
        var leading = statement.GetLeadingTrivia();
        var usingKeyword = SyntaxFactory.Token(SyntaxKind.UsingKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        var declaration = statement.Declaration.WithoutLeadingTrivia();

        var updated = needsAwait
            ? statement
                .WithAwaitKeyword(SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithLeadingTrivia(leading).WithTrailingTrivia(SyntaxFactory.Space))
                .WithUsingKeyword(usingKeyword)
                .WithDeclaration(declaration)
            : statement
                .WithUsingKeyword(usingKeyword.WithLeadingTrivia(leading))
                .WithDeclaration(declaration);

        return updated.WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }
}
