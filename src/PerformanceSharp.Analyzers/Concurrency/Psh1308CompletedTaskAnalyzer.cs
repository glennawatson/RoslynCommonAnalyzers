// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>Task.FromResult(...)</c> calls whose result is only ever observed as the
/// non-generic <c>Task</c> (PSH1308), where the carried value can never be read and
/// <c>Task.CompletedTask</c> returns the shared cached instance instead. The conversion
/// target decides: only invocations implicitly converted to the non-generic task type are
/// reported. Gated on <c>Task.CompletedTask</c> existing in the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1308CompletedTaskAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string FromResultMethodName = "FromResult";

    /// <summary>The receiver type name the syntax gate requires.</summary>
    internal const string TaskTypeName = "Task";

    /// <summary>The name of the cached completed-task property the fix moves to.</summary>
    internal const string CompletedTaskPropertyName = "CompletedTask";

    /// <summary>The metadata name of the non-generic task type.</summary>
    private const string TaskMetadataName = "System.Threading.Tasks.Task";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.UseCompletedTask);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(TaskMetadataName) is not { } taskType
                || taskType.GetMembers(CompletedTaskPropertyName).IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, taskType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation has the <c>Task.FromResult(x)</c> syntax shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the member name is FromResult and the receiver's rightmost identifier is Task.</returns>
    internal static bool IsTaskFromResultShape(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 1
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText != FromResultMethodName)
        {
            return false;
        }

        var receiver = access.Expression;
        while (receiver is MemberAccessExpressionSyntax nested)
        {
            receiver = nested.Name;
        }

        return receiver is IdentifierNameSyntax identifier
            && identifier.Identifier.ValueText == TaskTypeName;
    }

    /// <summary>Reports PSH1308 for a FromResult call that is consumed as the non-generic task.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="taskType">The non-generic task type.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol taskType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsTaskFromResultShape(invocation))
        {
            return;
        }

        var typeInfo = context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken);
        if (!SymbolEqualityComparer.Default.Equals(typeInfo.ConvertedType, taskType)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, taskType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.UseCompletedTask,
            invocation.SyntaxTree,
            invocation.Span));
    }
}
