// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>base.Equals(obj)</c> used as an early-out fast path inside an <c>Equals</c> override when the
/// base call binds to a base class that overrides <c>Equals</c> with value semantics (SST2435). Returning
/// <c>true</c> early — through <c>if (base.Equals(obj)) return true;</c> or a <c>base.Equals(obj) || ...</c>
/// short-circuit — then skips this type's own fields, so two instances whose base fields match compare equal
/// even when their derived fields differ.
/// </summary>
/// <remarks>
/// The shape is settled syntactically before any bind: a <c>base.Equals</c> call with one argument, inside an
/// <c>Equals</c> method, used as an early-return-true guard or the left side of <c>||</c>. Only then is the
/// call bound, to confirm the base implementation is a real value-equality override rather than
/// <see cref="object"/>'s reference identity — that reference-identity case is a valid shortcut and is left to
/// SST1447.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2435ValueEqualityFastPathAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The equality member name.</summary>
    private const string EqualsName = "Equals";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.ValueEqualityUsedAsFastPath);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports one base value-equality call used as an equality fast path.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax, Name.Identifier.ValueText: EqualsName }
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        if (!IsFastPath(invocation) || !IsInsideEqualsMethod(invocation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol
            {
                Name: EqualsName,
                ContainingType: { SpecialType: not SpecialType.System_Object } baseType,
            })
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.ValueEqualityUsedAsFastPath,
            invocation.GetLocation(),
            baseType.Name));
    }

    /// <summary>Returns whether the base call is used as an early-out fast path.</summary>
    /// <param name="invocation">The base equality invocation.</param>
    /// <returns><see langword="true"/> for an early-return-true guard or a left-side <c>||</c> operand.</returns>
    private static bool IsFastPath(InvocationExpressionSyntax invocation)
    {
        SyntaxNode node = invocation;
        while (node.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            node = parenthesized;
        }

        if (node.Parent is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.LogicalOrExpression) && binary.Left == node)
        {
            return true;
        }

        return node.Parent is IfStatementSyntax ifStatement && ifStatement.Condition == node && IsReturnTrue(ifStatement.Statement);
    }

    /// <summary>Returns whether a statement is (or wraps) a single <c>return true;</c>.</summary>
    /// <param name="statement">The <c>if</c> statement's body.</param>
    /// <returns><see langword="true"/> when it returns the literal <c>true</c>.</returns>
    private static bool IsReturnTrue(StatementSyntax statement)
    {
        var target = statement is BlockSyntax { Statements: { Count: 1 } statements } ? statements[0] : statement;
        return target is ReturnStatementSyntax { Expression: LiteralExpressionSyntax literal } && literal.IsKind(SyntaxKind.TrueLiteralExpression);
    }

    /// <summary>Returns whether the node sits inside an <c>Equals</c> method rather than a lambda or local function.</summary>
    /// <param name="node">The invocation being inspected.</param>
    /// <returns><see langword="true"/> when the nearest member is an <c>Equals</c> method.</returns>
    private static bool IsInsideEqualsMethod(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax method:
                    return method.Identifier.ValueText == EqualsName;

                case AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or BaseTypeDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }
}
