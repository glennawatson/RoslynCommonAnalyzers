// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a static <c>Regex</c> call written inside a loop body (PSH1421). Each call re-resolves the
/// pattern through the bounded process-wide cache; one instance built outside the loop resolves it once.
/// </summary>
/// <remarks>
/// The rule resolves <c>Regex</c> once per compilation and does nothing at all when the type is absent, so a
/// project that never references the regular-expression assembly pays only that one lookup.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1421CacheRegexOutsideLoopAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the regular-expression type.</summary>
    private const string RegexMetadataName = "System.Text.RegularExpressions.Regex";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(ApiSelectionRules.CacheRegexOutsideLoop);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(RegexMetadataName) is not { } regex)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, regex), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether a node is written inside a loop body within its own member.</summary>
    /// <param name="node">The call to locate.</param>
    /// <returns><see langword="true"/> when a <c>for</c>, <c>foreach</c>, <c>while</c>, or <c>do</c> encloses it.</returns>
    /// <remarks>
    /// The walk stops at the enclosing member or at a nested function, because a call inside a lambda declared
    /// in a loop runs when the delegate does, not once per iteration.
    /// </remarks>
    internal static bool IsInsideLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case ForStatementSyntax:
                case ForEachStatementSyntax:
                case ForEachVariableStatementSyntax:
                case WhileStatementSyntax:
                case DoStatementSyntax:
                    return true;

                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case MemberDeclarationSyntax:
                    return false;

                default:
                    continue;
            }
        }

        return false;
    }

    /// <summary>Returns whether the call's pattern argument is a compile-time constant.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="invocation">The static <c>Regex</c> call.</param>
    /// <returns><see langword="true"/> when the pattern can be hoisted into a cached instance.</returns>
    /// <remarks>
    /// Every static overload takes the input first and the pattern second. A pattern built at run time has
    /// nothing constant to hoist, so only a constant one is reported outside a loop.
    /// </remarks>
    private static bool HasConstantPattern(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return false;
        }

        var pattern = arguments[1].Expression;
        return pattern is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression }
            || context.SemanticModel.GetConstantValue(pattern, context.CancellationToken) is { HasValue: true, Value: string };
    }

    /// <summary>Reports one static <c>Regex</c> call whose pattern is resolved again on every call.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="regex">The resolved regular-expression type.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol regex)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: SimpleNameSyntax name })
        {
            return;
        }

        var inLoop = IsInsideLoop(invocation);
        if (!inLoop && !HasConstantPattern(context, invocation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, regex))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.CacheRegexOutsideLoop,
            invocation.SyntaxTree,
            invocation.Span,
            name.Identifier.ValueText,
            inLoop ? ApiSelectionRules.RegexCalledPerIteration : ApiSelectionRules.RegexConstantPattern));
    }
}
