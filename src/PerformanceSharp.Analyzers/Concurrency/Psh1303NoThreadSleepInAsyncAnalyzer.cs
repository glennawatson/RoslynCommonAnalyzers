// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>Thread.Sleep</c> calls inside <c>async</c> methods, local functions, and lambdas
/// (PSH1303). The nearest enclosing function decides the context, so a synchronous local
/// function inside an async method stays clean. The call is gated on the <c>Thread.Sleep</c>
/// syntax shape before any binding, and only reported when <c>Task.Delay</c> exists to move to.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1303NoThreadSleepInAsyncAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string SleepMethodName = "Sleep";

    /// <summary>The receiver type name the syntax gate requires.</summary>
    internal const string ThreadTypeName = "Thread";

    /// <summary>The metadata name of the thread type.</summary>
    private const string ThreadMetadataName = "System.Threading.Thread";

    /// <summary>The metadata name of the task type that provides Delay.</summary>
    private const string TaskMetadataName = "System.Threading.Tasks.Task";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.NoThreadSleepInAsync);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var threadType = start.Compilation.GetTypeByMetadataName(ThreadMetadataName);
            if (threadType is null || start.Compilation.GetTypeByMetadataName(TaskMetadataName) is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, threadType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation has the <c>Thread.Sleep(...)</c> syntax shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the member name is Sleep and the receiver's rightmost identifier is Thread.</returns>
    internal static bool IsThreadSleepShape(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText != SleepMethodName)
        {
            return false;
        }

        var receiver = access.Expression;
        while (receiver is MemberAccessExpressionSyntax nested)
        {
            receiver = nested.Name;
        }

        return receiver is IdentifierNameSyntax identifier
            && identifier.Identifier.ValueText == ThreadTypeName;
    }

    /// <summary>Returns whether the nearest enclosing function of a node is <c>async</c>.</summary>
    /// <param name="node">The node whose enclosing function is sought.</param>
    /// <returns><see langword="true"/> when the nearest enclosing method, local function, or lambda is async.</returns>
    internal static bool IsInAsyncFunction(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AnonymousFunctionExpressionSyntax anonymousFunction:
                    return anonymousFunction.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
                case LocalFunctionStatementSyntax localFunction:
                    return localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case MethodDeclarationSyntax method:
                    return method.Modifiers.Any(SyntaxKind.AsyncKeyword);
                case BaseTypeDeclarationSyntax or CompilationUnitSyntax:
                    return false;
                default:
                    continue;
            }
        }

        return false;
    }

    /// <summary>Reports PSH1303 for a <c>Thread.Sleep</c> call whose nearest enclosing function is async.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="threadType">The thread type.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol threadType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsThreadSleepShape(invocation)
            || !IsInAsyncFunction(invocation)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, threadType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.NoThreadSleepInAsync,
            invocation.SyntaxTree,
            invocation.Span));
    }
}
