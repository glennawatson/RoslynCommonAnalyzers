// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Adds <c>TaskCreationOptions.RunContinuationsAsynchronously</c> to a reported
/// <c>TaskCompletionSource</c> creation (PSH1302): appended as a new argument when the bound
/// constructor takes no options, substituted when the existing options constant is zero, and
/// or-combined otherwise. The enum is written as a simple name when it resolves at the creation
/// site and fully qualified when it does not, so the fix never breaks the build.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1302RunContinuationsAsynchronouslyCodeFixProvider))]
[Shared]
public sealed class Psh1302RunContinuationsAsynchronouslyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The simple name of the options enum type.</summary>
    private const string OptionsTypeName = "TaskCreationOptions";

    /// <summary>The name of the flag member the fix introduces.</summary>
    private const string FlagMemberName = "RunContinuationsAsynchronously";

    /// <summary>The fully qualified spelling used when the simple name does not resolve.</summary>
    private const string QualifiedFlagExpression = "global::System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.RunContinuationsAsynchronously.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Run continuations asynchronously", nameof(Psh1302RunContinuationsAsynchronouslyCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported creation and builds its flagged replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is BaseObjectCreationExpressionSyntax creation
            && Rewrite(model, creation, CancellationToken.None) is { } rewritten
            ? new NodeReplacement(creation, rewritten)
            : null;

    /// <summary>Builds the creation with the flag supplied, or <see langword="null"/> when it cannot be placed safely.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="creation">The reported creation expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The rewritten creation expression.</returns>
    private static BaseObjectCreationExpressionSyntax? Rewrite(
        SemanticModel model,
        BaseObjectCreationExpressionSyntax creation,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(creation, cancellationToken).Symbol is not IMethodSymbol constructor)
        {
            return null;
        }

        var flag = BuildFlagExpression(model, creation);
        if (FindOptionsArgument(creation, constructor) is not { } optionsArgument)
        {
            var argument = SyntaxFactory.Argument(flag);
            var argumentList = creation.ArgumentList is { } existing
                ? existing.AddArguments(argument)
                : SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(argument));
            return creation.WithArgumentList(argumentList);
        }

        var constant = model.GetConstantValue(optionsArgument.Expression, cancellationToken);
        var replacement = constant is { HasValue: true, Value: 0 }
            ? flag
            : SyntaxFactory.BinaryExpression(SyntaxKind.BitwiseOrExpression, optionsArgument.Expression.WithoutTrivia(), flag);
        return creation.ReplaceNode(optionsArgument.Expression, replacement.WithTriviaFrom(optionsArgument.Expression));
    }

    /// <summary>Returns the argument bound to the constructor's options parameter, when one is supplied.</summary>
    /// <param name="creation">The creation expression.</param>
    /// <param name="constructor">The bound constructor.</param>
    /// <returns>The options argument, or <see langword="null"/> when the constructor takes none.</returns>
    private static ArgumentSyntax? FindOptionsArgument(BaseObjectCreationExpressionSyntax creation, IMethodSymbol constructor)
    {
        var parameters = constructor.Parameters;
        for (var ordinal = 0; ordinal < parameters.Length; ordinal++)
        {
            if (parameters[ordinal].Type.Name == OptionsTypeName)
            {
                return FindArgumentForOrdinal(creation, parameters[ordinal].Name, ordinal);
            }
        }

        return null;
    }

    /// <summary>Returns the argument at a parameter ordinal, honoring named arguments.</summary>
    /// <param name="creation">The creation expression.</param>
    /// <param name="parameterName">The parameter's name, for named-argument matching.</param>
    /// <param name="ordinal">The parameter ordinal to resolve.</param>
    /// <returns>The matching argument, or <see langword="null"/> when it is not supplied.</returns>
    private static ArgumentSyntax? FindArgumentForOrdinal(BaseObjectCreationExpressionSyntax creation, string parameterName, int ordinal)
    {
        if (creation.ArgumentList is not { } argumentList)
        {
            return null;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (argument.NameColon is { } nameColon)
            {
                if (nameColon.Name.Identifier.ValueText == parameterName)
                {
                    return argument;
                }

                continue;
            }

            if (i == ordinal)
            {
                return argument;
            }
        }

        return null;
    }

    /// <summary>Builds the flag expression, simple when the enum's simple name resolves at the creation site.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="creation">The creation expression whose position anchors the lookup.</param>
    /// <returns>The flag member access expression.</returns>
    private static ExpressionSyntax BuildFlagExpression(SemanticModel model, BaseObjectCreationExpressionSyntax creation)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(creation.SpanStart, name: OptionsTypeName))
        {
            if (candidate is INamedTypeSymbol { TypeKind: TypeKind.Enum } named
                && named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(OptionsTypeName),
                    SyntaxFactory.IdentifierName(FlagMemberName));
            }
        }

        return SyntaxFactory.ParseExpression(QualifiedFlagExpression);
    }
}
