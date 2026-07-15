// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a discarded configured-await read (SST2446) from <c>ReadAsync</c> to <c>ReadExactlyAsync</c>, which
/// fills the buffer completely or throws, so the discarded count no longer hides a short read. The fix is
/// offered only where the read-exactly API resolves and only for the configured-await shape, whose two read
/// overloads map one for one onto the read-exactly overloads; a read stored in a local is reported without a
/// fix, since changing its initializer would change the local's type.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2446DiscardedStreamReadCodeFixProvider))]
[Shared]
public sealed class Sst2446DiscardedStreamReadCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The metadata name of the stream type.</summary>
    private const string StreamMetadataName = "System.IO.Stream";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.DiscardedStreamRead.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Read the buffer fully with ReadExactlyAsync", nameof(Sst2446DiscardedStreamReadCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported read and rewrites it to the read-exactly call.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no compiling fix applies here.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not { } node
            || (node as InvocationExpressionSyntax ?? node.FirstAncestorOrSelf<InvocationExpressionSyntax>()) is not { } invocation
            || Sst2446DiscardedStreamReadAnalyzer.GetInvokedName(invocation) != Sst2446DiscardedStreamReadAnalyzer.ReadAsyncName)
        {
            return null;
        }

        if (!IsConfiguredAwaitDiscard(invocation) || !StreamHasReadExactly(model))
        {
            return null;
        }

        var rewritten = ReplaceInvokedName(invocation);
        return new NodeReplacement(invocation, rewritten, current => ReplaceInvokedName((InvocationExpressionSyntax)current));
    }

    /// <summary>Returns whether the read sits directly under a discarded configured await.</summary>
    /// <param name="invocation">The read invocation.</param>
    /// <returns><see langword="true"/> for <c>await read.ConfigureAwait(...);</c> as a statement.</returns>
    private static bool IsConfiguredAwaitDiscard(InvocationExpressionSyntax invocation)
    {
        var afterRead = Climb(invocation);
        if (afterRead.Parent is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ConfigureAwait" } access
            || access.Expression != afterRead
            || access.Parent is not InvocationExpressionSyntax configureCall)
        {
            return false;
        }

        var afterConfigure = Climb(configureCall);
        return afterConfigure.Parent is AwaitExpressionSyntax { Parent: ExpressionStatementSyntax };
    }

    /// <summary>Climbs past enclosing parentheses.</summary>
    /// <param name="expression">The expression to climb from.</param>
    /// <returns>The outermost parenthesized ancestor, or the expression itself.</returns>
    private static ExpressionSyntax Climb(ExpressionSyntax expression)
    {
        while (expression.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized;
        }

        return expression;
    }

    /// <summary>Returns whether the compilation's stream type exposes the read-exactly API.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <returns><see langword="true"/> when a read-exactly method exists to bind to.</returns>
    private static bool StreamHasReadExactly(SemanticModel model)
    {
        if (model.Compilation.GetTypeByMetadataName(StreamMetadataName) is not { } streamType)
        {
            return false;
        }

        var members = streamType.GetMembers(Sst2446DiscardedStreamReadAnalyzer.ReadExactlyAsyncName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Replaces the invoked read method's name with the read-exactly name.</summary>
    /// <param name="invocation">The read invocation.</param>
    /// <returns>The rewritten invocation.</returns>
    private static InvocationExpressionSyntax ReplaceInvokedName(InvocationExpressionSyntax invocation)
    {
        var replacement = SyntaxFactory.IdentifierName(Sst2446DiscardedStreamReadAnalyzer.ReadExactlyAsyncName);
        var expression = invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.WithName(replacement.WithTriviaFrom(access.Name)),
            MemberBindingExpressionSyntax binding => binding.WithName(replacement.WithTriviaFrom(binding.Name)),
            SimpleNameSyntax simple => (ExpressionSyntax)replacement.WithTriviaFrom(simple),
            var other => other,
        };

        return invocation.WithExpression(expression);
    }
}
