// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Applies conservative modern syntax replacements for SST2202 through SST2204.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ModernSyntaxStyleCodeFixProvider))]
[Shared]
public sealed class ModernSyntaxStyleCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The number of arguments in <c>Substring(start)</c>.</summary>
    private const int SubstringStartOnlyArgumentCount = 1;

    /// <summary>The number of arguments in <c>Substring(start, length)</c>.</summary>
    private const int SubstringStartAndLengthArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.UseTargetTypedNew.Id,
        ModernSyntaxRules.UseIndexOperator.Id,
        ModernSyntaxRules.UseRangeOperator.Id);

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
            var title = diagnostic.Id switch
            {
                "SST2202" => "Remove repeated creation type",
                "SST2203" => "Index from the end directly",
                "SST2204" => "Slice with range syntax",
                _ => null
            };

            if (title is null || CreateReplacement(root, diagnostic, out _) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: diagnostic.Id),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var replacement = CreateReplacement(editor.OriginalRoot, diagnostic, out var oldNode);
        if (oldNode is null || replacement is null)
        {
            return;
        }

        editor.ReplaceNode(oldNode, replacement);
    }

    /// <summary>Applies one modern-syntax fix.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        var replacement = CreateReplacement(root, diagnostic, out var oldNode);
        return oldNode is null || replacement is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(oldNode, replacement));
    }

    /// <summary>Creates the syntax replacement for the supplied diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="oldNode">The node to replace.</param>
    /// <returns>The replacement node, or <see langword="null"/>.</returns>
    private static SyntaxNode? CreateReplacement(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode)
        => diagnostic.Id switch
        {
            "SST2202" => CreateTargetTypedNewReplacement(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2203" => CreateIndexReplacement(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2204" => CreateRangeReplacement(root, diagnostic.Location.SourceSpan, out oldNode),
            _ => NullReplacement(out oldNode)
        };

    /// <summary>Creates a target-typed <c>new</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The object creation node.</param>
    /// <returns>The implicit object creation, or <see langword="null"/>.</returns>
    private static ImplicitObjectCreationExpressionSyntax? CreateTargetTypedNewReplacement(
        SyntaxNode root,
        TextSpan span,
        out SyntaxNode? oldNode)
    {
        oldNode = FindAncestor<ObjectCreationExpressionSyntax>(root, span);
        if (oldNode is not ObjectCreationExpressionSyntax { ArgumentList: { } argumentList } objectCreation)
        {
            oldNode = null;
            return null;
        }

        return SyntaxFactory.ImplicitObjectCreationExpression(
                objectCreation.NewKeyword.WithTrailingTrivia(),
                argumentList,
                objectCreation.Initializer)
            .WithTriviaFrom(objectCreation);
    }

    /// <summary>Creates a from-end index replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The old argument node.</param>
    /// <returns>The from-end argument, or <see langword="null"/>.</returns>
    private static ArgumentSyntax? CreateIndexReplacement(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        if (FindAncestor<ArgumentSyntax>(root, span) is not { Expression: BinaryExpressionSyntax binary } argument
            || !binary.IsKind(SyntaxKind.SubtractExpression))
        {
            oldNode = null;
            return null;
        }

        var hatExpression = SyntaxFactory.PrefixUnaryExpression(
            SyntaxKind.IndexExpression,
            binary.Right.WithoutTrivia());

        oldNode = argument;
        return argument.WithExpression(hatExpression).WithTriviaFrom(argument);
    }

    /// <summary>Creates a string range replacement for a <c>Substring</c> invocation.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The invocation node.</param>
    /// <returns>The range element access, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? CreateRangeReplacement(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = FindAncestor<InvocationExpressionSyntax>(root, span);
        if (oldNode is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } invocation
            || invocation.ArgumentList.Arguments.Count is not SubstringStartOnlyArgumentCount and not SubstringStartAndLengthArgumentCount)
        {
            oldNode = null;
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var start = arguments[0].Expression.WithoutTrivia().ToString();
        var text = arguments.Count == SubstringStartOnlyArgumentCount
            ? $"{memberAccess.Expression.WithoutTrivia()}[{start}..]"
            : $"{memberAccess.Expression.WithoutTrivia()}[{start}..({start} + {arguments[1].Expression.WithoutTrivia()})]";
        return SyntaxFactory.ParseExpression(text).WithTriviaFrom(invocation);
    }

    /// <summary>Finds the node at a span or one of its ancestors.</summary>
    /// <typeparam name="T">The ancestor node type to find.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <returns>The matching node, or <see langword="null"/>.</returns>
    private static T? FindAncestor<T>(SyntaxNode root, TextSpan span)
        where T : SyntaxNode
    {
        var node = root.FindToken(span.Start).Parent;
        while (node is not null)
        {
            if (node is T matched)
            {
                return matched;
            }

            node = node.Parent;
        }

        return null;
    }

    /// <summary>Returns a null replacement while assigning the out parameter.</summary>
    /// <param name="oldNode">The old node.</param>
    /// <returns><see langword="null"/>.</returns>
    private static SyntaxNode? NullReplacement(out SyntaxNode? oldNode)
    {
        oldNode = null;
        return null;
    }
}
