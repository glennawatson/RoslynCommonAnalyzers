// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>ArrayPool&lt;T&gt;.Return</c> calls that hand back an array of reference-containing
/// elements without <c>clearArray: true</c> (PSH1010). The pool holds returned arrays
/// indefinitely, so uncleared reference elements keep whole object graphs reachable until the
/// array is rented and overwritten again. Calls are gated on the <c>.Return(...)</c> shape
/// before binding; unmanaged element types and opaque non-constant <c>clearArray</c> arguments
/// are never reported, and unconstrained type parameters are skipped.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1010ClearPooledReferenceArraysAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string ReturnMethodName = "Return";

    /// <summary>The name of the flag parameter the fix supplies.</summary>
    internal const string ClearArrayParameterName = "clearArray";

    /// <summary>The argument count of the Return overload that carries the clear flag.</summary>
    private const int FlagArgumentCount = 2;

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(AllocationRules.ClearPooledReferenceArrays);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var poolTypes = new PoolTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, poolTypes), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1010 for a pool return of reference-containing elements without clearing.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="poolTypes">The lazily resolved array pool type definition.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, PoolTypes poolTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: ReturnMethodName }
            || invocation.ArgumentList.Arguments.Count is not (1 or FlagArgumentCount))
        {
            return;
        }

        if (poolTypes.Get() is not { } poolType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.ContainingType is not { } containingType
            || !SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, poolType)
            || !ElementKeepsReferencesAlive(containingType.TypeArguments[0]))
        {
            return;
        }

        if (!ClearIsProvablyMissing(context, invocation, method))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.ClearPooledReferenceArrays,
            invocation.SyntaxTree,
            invocation.Span,
            containingType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    /// <summary>Returns whether the pool's element type can keep other objects reachable.</summary>
    /// <param name="elementType">The pool's element type.</param>
    /// <returns><see langword="true"/> for reference types and reference-containing structs; type parameters only with a class constraint.</returns>
    private static bool ElementKeepsReferencesAlive(ITypeSymbol elementType) =>
        elementType is ITypeParameterSymbol typeParameter
            ? typeParameter.HasReferenceTypeConstraint
            : !elementType.IsUnmanagedType;

    /// <summary>Returns whether the clear flag is provably absent or false.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The return invocation.</param>
    /// <param name="method">The bound return method.</param>
    /// <returns><see langword="true"/> when no flag is passed or a constant false is; opaque values are trusted.</returns>
    private static bool ClearIsProvablyMissing(in SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        var arguments = invocation.ArgumentList.Arguments;
        ArgumentSyntax? clearArgument = null;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (argument.NameColon is { } nameColon)
            {
                if (nameColon.Name.Identifier.ValueText == ClearArrayParameterName)
                {
                    clearArgument = argument;
                    break;
                }

                continue;
            }

            if (i != 1 || method.Parameters.Length != FlagArgumentCount)
            {
                continue;
            }

            clearArgument = argument;
            break;
        }

        if (clearArgument is null)
        {
            return true;
        }

        var constant = context.SemanticModel.GetConstantValue(clearArgument.Expression, context.CancellationToken);
        return constant is { HasValue: true, Value: false };
    }

    /// <summary>Resolves the array pool type only after a return call passes the syntax gate.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    private sealed class PoolTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the array pool type.</summary>
        private const string ArrayPoolMetadataName = "System.Buffers.ArrayPool`1";

        /// <summary>The cached type lookup, including a missing type.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the array pool type, resolving it on first use.</summary>
        /// <returns>The array pool definition, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(ArrayPoolMetadataName)])[0];
    }
}
