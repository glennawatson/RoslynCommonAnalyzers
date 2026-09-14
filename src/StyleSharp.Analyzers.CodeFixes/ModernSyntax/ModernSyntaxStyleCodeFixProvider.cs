// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Applies conservative modern syntax replacements for SST2202 through SST2204.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ModernSyntaxStyleCodeFixProvider))]
[Shared]
public sealed class ModernSyntaxStyleCodeFixProvider : CodeFixProvider
{
    /// <summary>The number of arguments in <c>Substring(start)</c>.</summary>
    private const int SubstringStartOnlyArgumentCount = 1;

    /// <summary>The number of arguments in <c>Substring(start, length)</c>.</summary>
    private const int SubstringStartAndLengthArgumentCount = 2;

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.UseTargetTypedNew.Id,
        ModernSyntaxRules.UseIndexOperator.Id,
        ModernSyntaxRules.UseRangeOperator.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            static (root, diagnostic) => CanRewrite(root, diagnostic) ? TitleFor(diagnostic.Id) : null,
            static diagnostic => diagnostic.Id,
            TryRewrite);

    /// <summary>Resolves the reported node and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        diagnostic.Id switch
        {
            "SST2202" => CreateTargetTypedNewReplacement(root, diagnostic.Location.SourceSpan),
            "SST2203" => CreateIndexReplacement(root, diagnostic.Location.SourceSpan),
            "SST2204" => CreateRangeReplacement(root, diagnostic.Location.SourceSpan),
            _ => null
        };

    /// <summary>Checks the original shape without constructing the replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>Whether the replacement can be built.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) => diagnostic.Id switch
    {
        "SST2202" => DiagnosticAncestor.Find<ObjectCreationExpressionSyntax>(root, diagnostic.Location.SourceSpan) is { ArgumentList: not null },
        "SST2203" => DiagnosticAncestor.Find<ArgumentSyntax>(root, diagnostic.Location.SourceSpan) is { Expression: BinaryExpressionSyntax binary }
            && binary.IsKind(SyntaxKind.SubtractExpression),
        "SST2204" => DiagnosticAncestor.Find<InvocationExpressionSyntax>(root, diagnostic.Location.SourceSpan) is
        {
            Expression: MemberAccessExpressionSyntax,
            ArgumentList.Arguments.Count: SubstringStartOnlyArgumentCount or SubstringStartAndLengthArgumentCount,
        },
        _ => false,
    };

    /// <summary>Words the action for the reported modern-syntax rule.</summary>
    /// <param name="diagnosticId">The reported rule id.</param>
    /// <returns>The code action title, or <see langword="null"/> for a rule this fix does not handle.</returns>
    private static string? TitleFor(string diagnosticId) => diagnosticId switch
    {
        "SST2202" => "Remove repeated creation type",
        "SST2203" => "Index from the end directly",
        "SST2204" => "Slice with range syntax",
        _ => null,
    };

    /// <summary>Creates a target-typed <c>new</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <returns>The object creation paired with its implicit form, or <see langword="null"/>.</returns>
    private static NodeReplacement? CreateTargetTypedNewReplacement(SyntaxNode root, TextSpan span) =>
        DiagnosticAncestor.Find<ObjectCreationExpressionSyntax>(root, span) is { ArgumentList: { } argumentList } objectCreation
            ? new NodeReplacement(
                objectCreation,
                SyntaxFactory.ImplicitObjectCreationExpression(
                    objectCreation.NewKeyword.WithTrailingTrivia(),
                    argumentList,
                    objectCreation.Initializer))
            : null;

    /// <summary>Creates a from-end index replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <returns>The argument paired with its from-end form, or <see langword="null"/>.</returns>
    private static NodeReplacement? CreateIndexReplacement(SyntaxNode root, TextSpan span)
    {
        if (DiagnosticAncestor.Find<ArgumentSyntax>(root, span) is not { Expression: BinaryExpressionSyntax binary } argument
            || !binary.IsKind(SyntaxKind.SubtractExpression))
        {
            return null;
        }

        var hatExpression = SyntaxFactory.PrefixUnaryExpression(
            SyntaxKind.IndexExpression,
            binary.Right.WithoutTrivia());

        return new NodeReplacement(
            argument,
            argument.Update(
                argument.NameColon,
                argument.RefKindKeyword,
                argument.NameColon is null && argument.RefKindKeyword.RawKind == 0
                    ? hatExpression.WithTriviaFrom(argument)
                    : hatExpression.WithTrailingTrivia(argument.GetTrailingTrivia())));
    }

    /// <summary>Creates a string range replacement for a <c>Substring</c> invocation.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <returns>The invocation paired with its range element access, or <see langword="null"/>.</returns>
    private static NodeReplacement? CreateRangeReplacement(SyntaxNode root, TextSpan span)
    {
        if (DiagnosticAncestor.Find<InvocationExpressionSyntax>(root, span) is not { Expression: MemberAccessExpressionSyntax memberAccess } invocation
            || invocation.ArgumentList.Arguments.Count is not SubstringStartOnlyArgumentCount and not SubstringStartAndLengthArgumentCount)
        {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var start = arguments[0].Expression.WithoutTrivia().ToString();
        var text = arguments.Count == SubstringStartOnlyArgumentCount
            ? $"{memberAccess.Expression.WithoutTrivia()}[{start}..]"
            : $"{memberAccess.Expression.WithoutTrivia()}[{start}..({start} + {arguments[1].Expression.WithoutTrivia()})]";
        return new NodeReplacement(invocation, SyntaxFactory.ParseExpression(text).WithTriviaFrom(invocation));
    }
}
