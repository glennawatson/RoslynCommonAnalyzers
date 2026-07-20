// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a type instantiated by name from non-constant data through the string overloads of the
/// activator (SES1404). The rule reports the <c>typeName</c> argument of
/// <c>Activator.CreateInstance(string assemblyName, string typeName, ...)</c> and
/// <c>Activator.CreateInstanceFrom(string assemblyFile, string typeName, ...)</c> when that argument is
/// not a compile-time constant. These overloads load and construct whatever type the string names, so a
/// name an attacker can influence lets them choose the type that is built -- and a hostile type's
/// constructor alone, before any member is invoked, can reach the file system, the network, or a process.
/// This is the by-name complement to the inline <c>Activator.CreateInstance(Type.GetType(x))</c> shape
/// covered elsewhere: here the overload is picked out by its first two parameters both being
/// <see cref="string"/> (the assembly reference plus the type name), and only the <c>typeName</c>
/// (second string) argument is inspected. Scope is intentionally the direct shape only -- a name first
/// stored in a local is left alone because confirming it would require data-flow tracking, a non-goal
/// here. The rule is resolved once per compilation by probing <c>System.Activator</c>; on a target
/// framework without it nothing is registered, so a project that cannot hit this shape pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1404NonConstantActivatorTypeNameAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The <c>Activator.CreateInstance</c> method whose by-name overloads are inspected.</summary>
    private const string CreateInstanceMethodName = "CreateInstance";

    /// <summary>The <c>Activator.CreateInstanceFrom</c> method whose by-name overloads are inspected.</summary>
    private const string CreateInstanceFromMethodName = "CreateInstanceFrom";

    /// <summary>The metadata name of the type that hosts the by-name activation overloads.</summary>
    private const string ActivatorMetadataName = "System.Activator";

    /// <summary>The position of the <c>typeName</c> parameter on every guarded by-name overload.</summary>
    private const int TypeNameParameterIndex = 1;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.NonConstantActivatorTypeName);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var activatorType = start.Compilation.GetTypeByMetadataName(ActivatorMetadataName);
            if (activatorType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, activatorType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1404 when a by-name activator overload's <c>typeName</c> argument is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="activatorType">The resolved <c>System.Activator</c> type.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol activatorType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.CreateInstance(...)' / '.CreateInstanceFrom(...)' call carrying at
        // least the (assemblyName/assemblyFile, typeName) pair. Everything else is rejected without binding.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: CreateInstanceMethodName or CreateInstanceFromMethodName }
            || invocation.ArgumentList.Arguments.Count < 2)
        {
            return;
        }

        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation call
            || !IsStringTypeNameOverload(call.TargetMethod, activatorType)
            || FindNonConstantTypeName(call) is not { } typeName)
        {
            return;
        }

        var member = call.TargetMethod;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.NonConstantActivatorTypeName,
            typeName.SyntaxTree,
            typeName.Span,
            member.ContainingType.Name + "." + member.Name));
    }

    /// <summary>Returns whether a bound method is an Activator by-name overload (first two parameters both <see cref="string"/>).</summary>
    /// <param name="method">The bound outer method.</param>
    /// <param name="activatorType">The resolved <c>System.Activator</c> type.</param>
    /// <returns><see langword="true"/> when the call is a guarded <c>(string assemblyName, string typeName, ...)</c> overload.</returns>
    private static bool IsStringTypeNameOverload(IMethodSymbol method, INamedTypeSymbol activatorType)
    {
        if ((method.Name != CreateInstanceMethodName && method.Name != CreateInstanceFromMethodName)
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, activatorType))
        {
            return false;
        }

        // The by-name overloads lead with (string assemblyName/assemblyFile, string typeName); the
        // Type-taking and generic overloads never do, so this cleanly separates them from SES1401's shape.
        var parameters = method.Parameters;
        return parameters.Length > TypeNameParameterIndex
            && parameters[0].Type.SpecialType == SpecialType.System_String
            && parameters[TypeNameParameterIndex].Type.SpecialType == SpecialType.System_String;
    }

    /// <summary>Returns the <c>typeName</c> argument expression when it is not a compile-time constant.</summary>
    /// <param name="call">The bound by-name activation call.</param>
    /// <returns>The offending <c>typeName</c> expression syntax, or <see langword="null"/> when it is constant.</returns>
    private static SyntaxNode? FindNonConstantTypeName(IInvocationOperation call)
    {
        var typeNameParameter = call.TargetMethod.Parameters[TypeNameParameterIndex];

        // Arguments carry their bound parameter, so a named or reordered 'typeName:' argument still maps to
        // the second string parameter regardless of source order.
        var arguments = call.Arguments;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (!SymbolEqualityComparer.Default.Equals(argument.Parameter, typeNameParameter))
            {
                continue;
            }

            return argument.Value.ConstantValue.HasValue ? null : argument.Value.Syntax;
        }

        return null;
    }
}
