// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags an argument to <c>Debug.Assert</c> that costs real work to produce (PSH1417). The call
/// itself carries <c>[Conditional("DEBUG")]</c> and is compiled away in release builds — but the
/// arguments are evaluated <em>before</em> the call, so the work that produced them is not. An
/// assertion whose message interpolates state, or whose condition calls something costly, pays
/// that cost in production for a check that no longer runs.
/// <para>
/// An argument is expensive when it is a non-constant interpolated string (it interpolates state,
/// which allocates and may box) or when it contains a call or an object creation anywhere in its
/// subtree. Anything the compiler folds to a constant is free and never reported, which is what
/// keeps <c>nameof(x)</c> — an invocation in syntax only — and constant interpolated strings
/// clean.
/// </para>
/// <para>
/// There is no code fix: hoisting the work behind <c>#if DEBUG</c>, or restructuring the
/// assertion so the cost is only paid when it fails, changes the shape of the code and is the
/// author's design choice, not a mechanical rewrite.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1417ExpensiveDebugAssertArgumentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string AssertMethodName = "Assert";

    /// <summary>The receiver type name the syntax gate requires.</summary>
    internal const string DebugTypeName = "Debug";

    /// <summary>The metadata name of the debug type that hosts Assert.</summary>
    private const string DebugMetadataName = "System.Diagnostics.Debug";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.ExpensiveDebugAssertArgument);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(DebugMetadataName) is not { } debugType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, debugType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation has the <c>Debug.Assert(...)</c> syntax shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the member name is Assert and the receiver's rightmost identifier is Debug.</returns>
    internal static bool IsDebugAssertShape(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText != AssertMethodName)
        {
            return false;
        }

        var receiver = access.Expression;
        while (receiver is MemberAccessExpressionSyntax nested)
        {
            receiver = nested.Name;
        }

        return receiver is IdentifierNameSyntax { Identifier.ValueText: DebugTypeName };
    }

    /// <summary>Returns whether an expression subtree contains a call or an object creation.</summary>
    /// <param name="expression">The argument expression.</param>
    /// <returns><see langword="true"/> when producing the value runs code.</returns>
    internal static bool ContainsCall(ExpressionSyntax expression)
    {
        if (IsCall(expression))
        {
            return true;
        }

        var state = default(CallScanState);
        DescendantTraversalHelper.VisitDescendants<ExpressionSyntax, CallScanState>(expression, ref state, VisitCall);
        return state.Found;
    }

    /// <summary>Reports PSH1417 for each expensive argument of a <c>Debug.Assert</c> call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="debugType">The <c>System.Diagnostics.Debug</c> type in the current compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol debugType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsDebugAssertShape(invocation)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol { IsStatic: true, Name: AssertMethodName } assert
            || !SymbolEqualityComparer.Default.Equals(assert.ContainingType, debugType))
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i].Expression;
            if (!IsExpensive(context.SemanticModel, argument, context.CancellationToken))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                ApiSelectionRules.ExpensiveDebugAssertArgument,
                argument.GetLocation(),
                argument.ToString()));
        }
    }

    /// <summary>Returns whether an argument costs work to produce at every call.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="argument">The argument expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the argument is not a constant and either interpolates state or runs code.</returns>
    private static bool IsExpensive(SemanticModel model, ExpressionSyntax argument, CancellationToken cancellationToken)
        => !model.GetConstantValue(argument, cancellationToken).HasValue
            && (argument is InterpolatedStringExpressionSyntax || ContainsCall(argument));

    /// <summary>Returns whether a node is itself a call or an object creation.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> when evaluating the node runs code.</returns>
    private static bool IsCall(SyntaxNode node)
        => node is InvocationExpressionSyntax
            or ObjectCreationExpressionSyntax
            or ImplicitObjectCreationExpressionSyntax;

    /// <summary>Classifies one expression encountered during the argument scan.</summary>
    /// <param name="node">The visited expression.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once a call is found.</returns>
    private static bool VisitCall(ExpressionSyntax node, ref CallScanState state)
    {
        if (!IsCall(node))
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Tracks whether the argument scan found a call.</summary>
    private record struct CallScanState
    {
        /// <summary>Gets or sets a value indicating whether a call or object creation was found.</summary>
        public bool Found { get; set; }
    }
}
