// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites the <c>x % 2 == 1</c> parity test (SST2416) into a form that is correct for negative values.
/// When the operand's type provides the generic-math parity helpers it becomes <c>T.IsOddInteger(x)</c> /
/// <c>T.IsEvenInteger(x)</c>; otherwise it falls back to <c>x % 2 != 0</c> / <c>x % 2 == 0</c>, which holds
/// on every target framework.
/// </summary>
/// <remarks>
/// Only the <c>% 2</c> parity shape is rewritten; a general <c>% N == K</c> has no single obvious repair, so
/// no fix is offered there and the message stands on its own.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2416SignedRemainderTestCodeFixProvider))]
[Shared]
public sealed class Sst2416SignedRemainderTestCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The divisor of the parity test the fix rewrites.</summary>
    private const int Divisor = 2;

    /// <summary>The remainder an odd value leaves when divided by two.</summary>
    private const int OddRemainder = 1;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.SignedRemainderTest.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Test parity in a way that is correct for negative values",
            nameof(Sst2416SignedRemainderTestCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported parity test and rewrites it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape is not the parity test.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BinaryExpressionSyntax>() is not { } comparison
            || !IsEqualityComparison(comparison)
            || !TryGetParity(comparison, model, out var modulo, out var isOddTest))
        {
            return null;
        }

        var replacement = BuildReplacement(model, comparison, modulo, isOddTest).WithTriviaFrom(comparison);
        return new NodeReplacement(comparison, replacement);
    }

    /// <summary>Returns whether the comparison is a <c>% 2</c> parity test, and which parity it asserts.</summary>
    /// <param name="comparison">The equality comparison.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="modulo">The remainder expression.</param>
    /// <param name="isOddTest">Whether the comparison asserts the operand is odd.</param>
    /// <returns><see langword="true"/> for <c>x % 2 == 1</c> or <c>x % 2 != 1</c>.</returns>
    private static bool TryGetParity(BinaryExpressionSyntax comparison, SemanticModel model, out BinaryExpressionSyntax modulo, out bool isOddTest)
    {
        modulo = null!;
        isOddTest = false;
        ExpressionSyntax other;
        if (comparison.Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ModuloExpression } left)
        {
            modulo = left;
            other = comparison.Right;
        }
        else if (comparison.Right is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ModuloExpression } right)
        {
            modulo = right;
            other = comparison.Left;
        }
        else
        {
            return false;
        }

        if (!IsConstant(model, modulo.Right, Divisor) || !IsConstant(model, other, OddRemainder))
        {
            return false;
        }

        isOddTest = comparison.IsKind(SyntaxKind.EqualsExpression);
        return true;
    }

    /// <summary>Returns whether a node is an equality or inequality comparison.</summary>
    /// <param name="node">The candidate comparison.</param>
    /// <returns><see langword="true"/> for <c>==</c> and <c>!=</c>.</returns>
    private static bool IsEqualityComparison(ExpressionSyntax node)
        => node.IsKind(SyntaxKind.EqualsExpression) || node.IsKind(SyntaxKind.NotEqualsExpression);

    /// <summary>Builds the replacement expression, preferring the generic-math parity helper.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="comparison">The original comparison.</param>
    /// <param name="modulo">The remainder expression.</param>
    /// <param name="isOddTest">Whether the comparison asserts the operand is odd.</param>
    /// <returns>The replacement expression.</returns>
    private static ExpressionSyntax BuildReplacement(SemanticModel model, BinaryExpressionSyntax comparison, BinaryExpressionSyntax modulo, bool isOddTest)
    {
        var dividend = modulo.Left;
        var type = model.GetTypeInfo(dividend, System.Threading.CancellationToken.None).Type;
        var helper = isOddTest ? "IsOddInteger" : "IsEvenInteger";
        if (type is not null && HasStaticParityHelper(type, helper))
        {
            var typeName = SyntaxFactory.ParseTypeName(type.ToMinimalDisplayString(model, comparison.SpanStart));
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, typeName, SyntaxFactory.IdentifierName(helper)),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(dividend.WithoutTrivia()))));
        }

        var kind = isOddTest ? SyntaxKind.NotEqualsExpression : SyntaxKind.EqualsExpression;
        var tokenKind = isOddTest ? SyntaxKind.ExclamationEqualsToken : SyntaxKind.EqualsEqualsToken;
        var operatorToken = SyntaxFactory.Token(comparison.OperatorToken.LeadingTrivia, tokenKind, comparison.OperatorToken.TrailingTrivia);
        var zero = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0));
        return SyntaxFactory.BinaryExpression(kind, modulo, operatorToken, zero);
    }

    /// <summary>Returns whether a type declares an accessible static one-argument parity helper.</summary>
    /// <param name="type">The operand's type.</param>
    /// <param name="name">The helper name.</param>
    /// <returns><see langword="true"/> when the helper resolves.</returns>
    private static bool HasStaticParityHelper(ITypeSymbol type, string name)
    {
        foreach (var member in type.GetMembers(name))
        {
            if (member is IMethodSymbol { IsStatic: true, MethodKind: MethodKind.Ordinary, DeclaredAccessibility: Accessibility.Public } method
                && method.Parameters.Length == 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an expression is a specific integer constant.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The expression.</param>
    /// <param name="expected">The expected value.</param>
    /// <returns><see langword="true"/> when the constant equals the expected value.</returns>
    private static bool IsConstant(SemanticModel model, ExpressionSyntax expression, int expected)
    {
        var constant = model.GetConstantValue(expression);
        return constant is { HasValue: true, Value: int value } && value == expected;
    }
}
