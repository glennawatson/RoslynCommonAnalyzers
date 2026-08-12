// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Passes the in-scope cancellation token to a reported call (PSH1317): <c>reader.ReadAsync()</c>
/// becomes <c>reader.ReadAsync(cancellationToken)</c>, and a call that reaches its token slot only
/// past skipped optional parameters names it — <c>Send(message, cancellationToken: token)</c>. The
/// existing arguments and their separators are preserved.
/// </summary>
/// <remarks>
/// The rewritten call is speculatively bound before the fix is offered, so an argument list that would
/// pick a different overload — or not compile at all — is never suggested. A call reached through a
/// conditional access cannot be rebound once detached, so the fix stands down there and the diagnostic
/// is left for a hand edit.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1317PassCancellationTokenCodeFixProvider))]
[Shared]
public sealed class Psh1317PassCancellationTokenCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>One argument's width in an interleaved node-and-separator list: the argument plus its comma.</summary>
    private const int InterleavedStride = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.PassCancellationToken.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Pass the cancellation token", nameof(Psh1317PassCancellationTokenCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported call and builds its argument list with the token passed.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<InvocationExpressionSyntax>() is { } invocation
            && TryBuildArguments(model, invocation) is { } arguments
            ? new NodeReplacement(invocation.ArgumentList, arguments)
            : null;

    /// <summary>Builds the argument list that passes the in-scope token, and proves the call still binds.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The reported call.</param>
    /// <returns>The rewritten argument list, or <see langword="null"/> when the token cannot be passed here.</returns>
    private static ArgumentListSyntax? TryBuildArguments(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        if (ConditionalAccessSpeculation.ReachedThroughConditionalAccess(invocation.Expression)
            || model.Compilation.GetTypeByMetadataName(CancellationTokenScope.TokenMetadataName) is not { } tokenType
            || CancellationTokenOverload.TryFind(model, invocation, tokenType, cache: null, default) is not { } forwardable)
        {
            return null;
        }

        var argument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(forwardable.TokenName));
        var arguments = CanPassPositionally(invocation.ArgumentList.Arguments, forwardable.TokenIndex)
            ? Insert(invocation.ArgumentList, argument, forwardable.TokenIndex)
            : Insert(
                invocation.ArgumentList,
                argument.WithNameColon(SyntaxFactory.NameColon(forwardable.Method.Parameters[forwardable.TokenIndex].Name)),
                invocation.ArgumentList.Arguments.Count);

        return BindsToTarget(model, invocation, arguments, forwardable.Method) ? arguments : null;
    }

    /// <summary>Returns whether the token can take its parameter slot by position.</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="index">The token parameter's index.</param>
    /// <returns><see langword="false"/> when optional parameters were skipped or an argument is already named.</returns>
    private static bool CanPassPositionally(SeparatedSyntaxList<ArgumentSyntax> arguments, int index)
    {
        if (index > arguments.Count)
        {
            return false;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is not null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Inserts an argument at a position, keeping the existing arguments and separators intact.</summary>
    /// <param name="list">The original argument list.</param>
    /// <param name="argument">The argument to insert.</param>
    /// <param name="index">The argument index to insert at.</param>
    /// <returns>The rewritten argument list.</returns>
    private static ArgumentListSyntax Insert(ArgumentListSyntax list, ArgumentSyntax argument, int index)
    {
        if (list.Arguments.Count == 0)
        {
            return list.WithArguments(SyntaxFactory.SingletonSeparatedList(argument));
        }

        var separated = list.Arguments.GetWithSeparators();
        var comma = SyntaxFactory.Token(default, SyntaxKind.CommaToken, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        var items = new SyntaxNodeOrToken[separated.Count + InterleavedStride];
        if (index >= list.Arguments.Count)
        {
            for (var i = 0; i < separated.Count; i++)
            {
                items[i] = separated[i];
            }

            items[separated.Count] = comma;
            items[separated.Count + 1] = argument;
            return list.WithArguments(SyntaxFactory.SeparatedList<ArgumentSyntax>(items));
        }

        var pivot = index * InterleavedStride;
        for (var i = 0; i < pivot; i++)
        {
            items[i] = separated[i];
        }

        items[pivot] = argument;
        items[pivot + 1] = comma;
        for (var i = pivot; i < separated.Count; i++)
        {
            items[i + InterleavedStride] = separated[i];
        }

        return list.WithArguments(SyntaxFactory.SeparatedList<ArgumentSyntax>(items));
    }

    /// <summary>Speculatively binds the rewritten call and confirms it resolves to the method that takes the token.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The original call, used as the speculative binding context.</param>
    /// <param name="arguments">The rewritten argument list.</param>
    /// <param name="target">The method the analyzer resolved.</param>
    /// <returns><see langword="true"/> when the rewritten call binds to that method.</returns>
    private static bool BindsToTarget(SemanticModel model, InvocationExpressionSyntax invocation, ArgumentListSyntax arguments, IMethodSymbol target)
        => model.GetSpeculativeSymbolInfo(
                    invocation.SpanStart,
                    invocation.WithArgumentList(arguments).WithoutTrivia(),
                    SpeculativeBindingOption.BindAsExpression).Symbol
                is IMethodSymbol bound
            && SymbolEqualityComparer.Default.Equals(bound.OriginalDefinition, target.OriginalDefinition);
}
