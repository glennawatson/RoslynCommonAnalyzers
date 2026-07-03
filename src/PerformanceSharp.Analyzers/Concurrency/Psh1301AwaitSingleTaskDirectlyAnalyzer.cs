// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>Task.WhenAll</c>/<c>Task.WaitAll</c> calls that coordinate exactly one task (PSH1301):
/// a single argument whose static type is <c>Task</c> or <c>Task&lt;T&gt;</c> — a params expansion
/// of one task, never a single array or enumerable argument. <c>WaitAll</c> is always reported
/// (it returns void, so <c>task.Wait()</c> is equivalent); <c>WhenAll</c> is reported only where
/// dropping the wrapper preserves semantics — the argument is a non-generic <c>Task</c>, or the
/// whole call is awaited as a standalone expression statement, so the <c>Task&lt;T[]&gt;</c>
/// result is never observed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1301AwaitSingleTaskDirectlyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the awaited task combinator.</summary>
    internal const string WhenAllMethodName = "WhenAll";

    /// <summary>The name of the blocking task combinator.</summary>
    internal const string WaitAllMethodName = "WaitAll";

    /// <summary>The metadata name of the non-generic task type.</summary>
    private const string TaskMetadataName = "System.Threading.Tasks.Task";

    /// <summary>The metadata name of the generic task type.</summary>
    private const string TaskOfTMetadataName = "System.Threading.Tasks.Task`1";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.AwaitSingleTaskDirectly);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var taskType = start.Compilation.GetTypeByMetadataName(TaskMetadataName);
            if (taskType is null)
            {
                return;
            }

            var taskOfTType = start.Compilation.GetTypeByMetadataName(TaskOfTMetadataName);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, taskType, taskOfTType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation has the single-argument <c>X.WhenAll(t)</c>/<c>X.WaitAll(t)</c> syntax shape.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="isWaitAll"><see langword="true"/> when the invoked member is <c>WaitAll</c>.</param>
    /// <returns><see langword="true"/> when the syntax-only combinator shape matches.</returns>
    internal static bool IsSingleArgumentCombinatorShape(InvocationExpressionSyntax invocation, out bool isWaitAll)
    {
        isWaitAll = false;
        if (invocation.ArgumentList.Arguments.Count != 1
            || invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        var name = access.Name.Identifier.ValueText;
        if (name == WaitAllMethodName)
        {
            isWaitAll = true;
            return true;
        }

        return name == WhenAllMethodName;
    }

    /// <summary>Reports PSH1301 for a WhenAll/WaitAll invocation wrapping a single task.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="taskType">The non-generic task type.</param>
    /// <param name="taskOfTType">The generic task type, when it exists.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol taskType, INamedTypeSymbol? taskOfTType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsSingleArgumentCombinatorShape(invocation, out var isWaitAll)
            || !BindsToTaskCombinator(context.SemanticModel, invocation, taskType, context.CancellationToken))
        {
            return;
        }

        var argument = invocation.ArgumentList.Arguments[0].Expression;
        var argumentType = context.SemanticModel.GetTypeInfo(argument, context.CancellationToken).Type;
        if (!TryClassifyArgumentTask(argumentType, taskType, taskOfTType, out var isGenericTask))
        {
            return;
        }

        // WhenAll of a single Task<T> produces a Task<T[]>; replacing it with the bare task is
        // only truthful when that array result is never observed.
        if (!isWaitAll && isGenericTask && !IsDirectlyAwaitedStatement(invocation))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.AwaitSingleTaskDirectly,
            invocation.SyntaxTree,
            invocation.Span,
            isWaitAll ? WaitAllMethodName : WhenAllMethodName));
    }

    /// <summary>Returns whether an invocation binds to a static method on the non-generic task type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="taskType">The non-generic task type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the invocation is a static task combinator call.</returns>
    private static bool BindsToTaskCombinator(SemanticModel model, InvocationExpressionSyntax invocation, INamedTypeSymbol taskType, CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { IsStatic: true } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, taskType);

    /// <summary>Returns whether the single argument is a non-generic or generic task of the expected types.</summary>
    /// <param name="argumentType">The argument's static type.</param>
    /// <param name="taskType">The non-generic task type.</param>
    /// <param name="taskOfTType">The generic task type, when it exists.</param>
    /// <param name="isGenericTask">Whether the argument is a <c>Task&lt;T&gt;</c> rather than a bare task.</param>
    /// <returns><see langword="true"/> when the argument is one of the recognized task shapes.</returns>
    private static bool TryClassifyArgumentTask(ITypeSymbol? argumentType, INamedTypeSymbol taskType, INamedTypeSymbol? taskOfTType, out bool isGenericTask)
    {
        isGenericTask = false;
        if (SymbolEqualityComparer.Default.Equals(argumentType, taskType))
        {
            return true;
        }

        if (taskOfTType is null
            || argumentType is not INamedTypeSymbol namedArgumentType
            || !SymbolEqualityComparer.Default.Equals(namedArgumentType.OriginalDefinition, taskOfTType))
        {
            return false;
        }

        isGenericTask = true;
        return true;
    }

    /// <summary>Returns whether an invocation is awaited as a standalone expression statement, discarding the result.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> for the <c>await Task.WhenAll(t);</c> statement shape.</returns>
    private static bool IsDirectlyAwaitedStatement(InvocationExpressionSyntax invocation)
        => invocation.Parent is AwaitExpressionSyntax { Parent: ExpressionStatementSyntax };
}
