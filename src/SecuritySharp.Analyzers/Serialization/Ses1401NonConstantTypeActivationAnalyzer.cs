// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a type resolved from non-constant data that is then instantiated or used as a deserialization
/// target (SES1401). The rule reports the inline <c>Type.GetType(x)</c> expression when <c>x</c> is not a
/// compile-time constant and that expression is passed directly as the <see cref="System.Type"/> argument
/// of either <c>Activator.CreateInstance(...)</c> or any method named <c>Deserialize</c>. Resolving a type
/// from data an attacker can influence lets them choose the type that is constructed, so a gadget whose
/// construction or deserialization touches the file system, network, or a process turns a crafted type name
/// into code execution. Scope is intentionally the inline shape only -- a type first stored in a local is
/// left alone because confirming it would require data-flow tracking, a non-goal here. The rule is resolved
/// once per compilation by probing <c>System.Activator</c> and <c>System.Type</c>; on a target framework
/// without them nothing is registered, so a project that cannot hit this shape pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1401NonConstantTypeActivationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The <c>Activator.CreateInstance</c> method whose type argument is inspected.</summary>
    private const string CreateInstanceMethodName = "CreateInstance";

    /// <summary>The deserialization method name whose type argument is inspected.</summary>
    private const string DeserializeMethodName = "Deserialize";

    /// <summary>The static <c>Type.GetType</c> factory that resolves a type from a name.</summary>
    private const string GetTypeMethodName = "GetType";

    /// <summary>The metadata name of the type that hosts <c>CreateInstance</c>.</summary>
    private const string ActivatorMetadataName = "System.Activator";

    /// <summary>The metadata name of the type that hosts the static <c>GetType</c> factory.</summary>
    private const string TypeMetadataName = "System.Type";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.NonConstantTypeActivation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // The keyed types anchor the match rather than a suggested API, and both are present on every
            // target framework, so they are resolved once and passed through: when a symbol is absent the
            // comparison below simply never matches and the rule stays silent, avoiding a dead early-return.
            var activatorType = start.Compilation.GetTypeByMetadataName(ActivatorMetadataName);
            var typeType = start.Compilation.GetTypeByMetadataName(TypeMetadataName);

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, activatorType, typeType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1401 when a call's <see cref="System.Type"/> argument is an inline <c>Type.GetType(nonConstant)</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="activatorType">The resolved <c>System.Activator</c> type, or <see langword="null"/> when absent.</param>
    /// <param name="typeType">The resolved <c>System.Type</c> type, or <see langword="null"/> when absent.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol? activatorType, INamedTypeSymbol? typeType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.CreateInstance(...)' or '.Deserialize(...)' call that carries at
        // least one argument shaped like 'x.GetType(y)'. Everything else is rejected without binding.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: CreateInstanceMethodName or DeserializeMethodName }
            || invocation.ArgumentList.Arguments.Count == 0
            || !HasGetTypeShapedArgument(invocation.ArgumentList))
        {
            return;
        }

        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation outerCall
            || !IsGuardedTarget(outerCall.TargetMethod, activatorType)
            || FindNonConstantTypeSource(context.SemanticModel, outerCall, typeType, context.CancellationToken) is not { } getType)
        {
            return;
        }

        var member = outerCall.TargetMethod;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.NonConstantTypeActivation,
            getType.SyntaxTree,
            getType.Span,
            member.ContainingType.Name + "." + member.Name));
    }

    /// <summary>Returns the first <see cref="System.Type"/> argument backed by an inline <c>Type.GetType(nonConstant)</c> call.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="outerCall">The bound instantiation or deserialization call.</param>
    /// <param name="typeType">The resolved <c>System.Type</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The offending <c>Type.GetType</c> invocation, or <see langword="null"/> when none applies.</returns>
    private static InvocationExpressionSyntax? FindNonConstantTypeSource(SemanticModel model, IInvocationOperation outerCall, INamedTypeSymbol? typeType, CancellationToken cancellationToken)
    {
        var arguments = outerCall.Arguments;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Parameter is not { } parameter || !SymbolEqualityComparer.Default.Equals(parameter.Type, typeType))
            {
                continue;
            }

            if (GetTypeInvocation(argument.Value, typeType) is { } getType && IsNonConstantTypeName(model, getType, cancellationToken))
            {
                return getType;
            }
        }

        return null;
    }

    /// <summary>Returns whether any argument is syntactically an <c>x.GetType(...)</c> call with at least one argument.</summary>
    /// <param name="argumentList">The invocation's argument list.</param>
    /// <returns><see langword="true"/> when a <c>GetType</c>-shaped argument is present.</returns>
    private static bool HasGetTypeShapedArgument(ArgumentListSyntax argumentList)
    {
        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].Expression is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: GetTypeMethodName },
                    ArgumentList.Arguments.Count: > 0
                })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a bound call is <c>Activator.CreateInstance</c> or a <c>Deserialize</c> method.</summary>
    /// <param name="method">The bound outer method.</param>
    /// <param name="activatorType">The resolved <c>System.Activator</c> type, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the call is a guarded instantiation or deserialization target.</returns>
    private static bool IsGuardedTarget(IMethodSymbol method, INamedTypeSymbol? activatorType)
        => (method.Name == CreateInstanceMethodName && SymbolEqualityComparer.Default.Equals(method.ContainingType, activatorType))
            || method.Name == DeserializeMethodName;

    /// <summary>Returns the <c>Type.GetType(...)</c> invocation syntax backing an argument value, or <see langword="null"/>.</summary>
    /// <param name="value">The argument value operation.</param>
    /// <param name="typeType">The resolved <c>System.Type</c> type, or <see langword="null"/> when absent.</param>
    /// <returns>The <c>Type.GetType</c> invocation expression, or <see langword="null"/> when the value is not one.</returns>
    private static InvocationExpressionSyntax? GetTypeInvocation(IOperation value, INamedTypeSymbol? typeType)
    {
        // The type argument binds directly to a System.Type parameter, so the value is the Type.GetType
        // invocation itself with no intervening conversion to unwrap.
        if (value is not IInvocationOperation invocation)
        {
            return null;
        }

        var target = invocation.TargetMethod;
        if (!target.IsStatic
            || target.Name != GetTypeMethodName
            || !SymbolEqualityComparer.Default.Equals(target.ContainingType, typeType))
        {
            return null;
        }

        // A bound 'Type.GetType' call is always an invocation expression carrying its string name argument.
        return (InvocationExpressionSyntax)invocation.Syntax;
    }

    /// <summary>Returns whether the first argument of a <c>Type.GetType</c> call is not a compile-time constant.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="getType">The <c>Type.GetType</c> invocation expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the type name is non-constant.</returns>
    private static bool IsNonConstantTypeName(SemanticModel model, InvocationExpressionSyntax getType, CancellationToken cancellationToken)
    {
        var nameExpression = getType.ArgumentList.Arguments[0].Expression;
        return !model.GetConstantValue(nameExpression, cancellationToken).HasValue;
    }
}
