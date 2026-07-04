// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported guard clause into its throw helper call (PSH1409):
/// <c>ArgumentNullException.ThrowIfNull(x);</c>, <c>ArgumentException.ThrowIfNullOrEmpty(x);</c>,
/// <c>ObjectDisposedException.ThrowIf(condition, this);</c> — <c>typeof(...)</c> in static
/// contexts — and the <c>ArgumentOutOfRangeException.ThrowIf*</c> comparisons. The receiver
/// spelling comes from the analyzer's alias-aware resolution: a project aliasing helper names
/// to polyfills (the Primitives model) gets the alias, so the fix compiles down to net462;
/// otherwise the thrown exception's own spelling is reused.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1409ThrowHelperCodeFixProvider))]
[Shared]
public sealed class Psh1409ThrowHelperCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.UseThrowHelpers.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use the throw helper", nameof(Psh1409ThrowHelperCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported guard and builds its helper-call statement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not IfStatementSyntax ifStatement
            || Psh1409ThrowHelperAnalyzer.TryClassify(ifStatement) is not { } shape
            || Psh1409ThrowHelperAnalyzer.TryGetHelperReceiver(model, ifStatement.SpanStart, shape) is not { } receiverSpelling)
        {
            return null;
        }

        var arguments = BuildArguments(ifStatement, shape);
        var call = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression(receiverSpelling),
                SyntaxFactory.IdentifierName(shape.HelperName)),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));

        return new NodeReplacement(ifStatement, SyntaxFactory.ExpressionStatement(call).WithTriviaFrom(ifStatement));
    }

    /// <summary>Builds the helper's argument list.</summary>
    /// <param name="ifStatement">The reported guard.</param>
    /// <param name="shape">The classified guard.</param>
    /// <returns>The arguments.</returns>
    private static ImmutableArray<ArgumentSyntax> BuildArguments(IfStatementSyntax ifStatement, Psh1409ThrowHelperAnalyzer.GuardShape shape)
    {
        if (shape.Kind == Psh1409ThrowHelperAnalyzer.GuardKind.Disposed)
        {
            return ImmutableArrays.Of(
                SyntaxFactory.Argument(shape.Value.WithoutTrivia()),
                SyntaxFactory.Argument(BuildInstanceExpression(ifStatement)).WithLeadingTrivia(SyntaxFactory.Space));
        }

        return shape.Operand is null
            ? ImmutableArrays.Of(SyntaxFactory.Argument(shape.Value.WithoutTrivia()))
            : ImmutableArrays.Of(
                SyntaxFactory.Argument(shape.Value.WithoutTrivia()),
                SyntaxFactory.Argument(shape.Operand.WithoutTrivia()).WithLeadingTrivia(SyntaxFactory.Space));
    }

    /// <summary>Builds the disposal helper's instance argument: <c>this</c>, or <c>typeof(...)</c> in static contexts.</summary>
    /// <param name="ifStatement">The reported guard.</param>
    /// <returns>The instance expression.</returns>
    private static ExpressionSyntax BuildInstanceExpression(IfStatementSyntax ifStatement)
    {
        if (!IsStaticContext(ifStatement) || ifStatement.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } type)
        {
            return SyntaxFactory.ThisExpression();
        }

        return SyntaxFactory.TypeOfExpression(SyntaxFactory.ParseTypeName(type.Identifier.ValueText + type.TypeParameterList));
    }

    /// <summary>Returns whether the guard sits in a static member.</summary>
    /// <param name="ifStatement">The reported guard.</param>
    /// <returns><see langword="true"/> when <c>this</c> is unavailable.</returns>
    private static bool IsStaticContext(IfStatementSyntax ifStatement)
    {
        for (SyntaxNode? current = ifStatement.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case LocalFunctionStatementSyntax local when local.Modifiers.Any(SyntaxKind.StaticKeyword):
                    return true;
                case AnonymousFunctionExpressionSyntax anonymous when anonymous.Modifiers.Any(SyntaxKind.StaticKeyword):
                    return true;
                case BaseMethodDeclarationSyntax method:
                    return method.Modifiers.Any(SyntaxKind.StaticKeyword);
                case BasePropertyDeclarationSyntax property:
                    return property.Modifiers.Any(SyntaxKind.StaticKeyword);
                case BaseTypeDeclarationSyntax or CompilationUnitSyntax:
                    return true;
                default:
                    continue;
            }
        }

        return true;
    }
}
