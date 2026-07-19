// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites an equality assertion against a boolean literal (SST2503) into the framework's dedicated boolean
/// assertion: <c>Assert.Equal(true, x)</c> becomes <c>Assert.True(x)</c> and <c>Assert.AreEqual(false, x)</c>
/// becomes <c>Assert.IsFalse(x)</c>. The literal is dropped and the value under test is kept. The fix resolves the
/// target boolean assertion in the compilation and is offered only when it exists, so the rewritten call always
/// binds.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2503BooleanLiteralAssertionCodeFixProvider))]
[Shared]
public sealed class Sst2503BooleanLiteralAssertionCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(TestingRules.BooleanLiteralAssertion.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use the dedicated boolean assertion",
            nameof(Sst2503BooleanLiteralAssertionCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported assertion and rewrites it to the boolean assertion.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no compiling fix applies here.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not { } node
            || (node as InvocationExpressionSyntax ?? node.FirstAncestorOrSelf<InvocationExpressionSyntax>()) is not { } invocation)
        {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count != 2)
        {
            return null;
        }

        var literalIndex = Sst2503BooleanLiteralAssertionAnalyzer.GetBooleanLiteralArgumentIndex(arguments);
        if (literalIndex < 0)
        {
            return null;
        }

        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return null;
        }

        var literalIsTrue = arguments[literalIndex].Expression.IsKind(SyntaxKind.TrueLiteralExpression);
        if (Sst2503BooleanLiteralAssertionAnalyzer.TryGetBooleanAssertion(method, literalIsTrue) is not { } targetMethod)
        {
            return null;
        }

        var actualIndex = 1 - literalIndex;
        var rewritten = Rewrite(invocation, actualIndex, targetMethod);
        return new NodeReplacement(invocation, rewritten, current => Rewrite((InvocationExpressionSyntax)current, actualIndex, targetMethod));
    }

    /// <summary>Builds the boolean assertion call from the equality call, keeping only the value under test.</summary>
    /// <param name="invocation">The equality assertion invocation.</param>
    /// <param name="actualIndex">The index of the argument that is the value under test.</param>
    /// <param name="targetMethod">The boolean assertion method name to call.</param>
    /// <returns>The rewritten invocation.</returns>
    private static InvocationExpressionSyntax Rewrite(InvocationExpressionSyntax invocation, int actualIndex, string targetMethod)
    {
        var replacementName = SyntaxFactory.IdentifierName(targetMethod);
        var expression = invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.WithName(replacementName.WithTriviaFrom(access.Name)),
            MemberBindingExpressionSyntax binding => binding.WithName(replacementName.WithTriviaFrom(binding.Name)),
            SimpleNameSyntax simple => (ExpressionSyntax)replacementName.WithTriviaFrom(simple),
            var other => other,
        };

        var actualExpression = invocation.ArgumentList.Arguments[actualIndex].Expression.WithoutTrivia();
        var newArguments = invocation.ArgumentList.WithArguments(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(actualExpression)));
        return invocation.WithExpression(expression).WithArgumentList(newArguments);
    }
}
