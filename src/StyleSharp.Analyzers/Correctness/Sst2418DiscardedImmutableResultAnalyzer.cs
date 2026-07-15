// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a whole-statement call on an immutable value whose result is discarded (SST2418):
/// <c>date.AddDays(1);</c> computes a new value and drops it, leaving the receiver unchanged.
/// </summary>
/// <remarks>
/// The immutable receiver is recognised from its type — a readonly struct, a span or memory slice, a
/// well-known immutable value type, or a stateless numeric helper class — rather than a fixed list of
/// methods, so a user-defined readonly record struct is covered for free. Shapes an unused-object or
/// unused-string diagnostic already reports (string receivers, LINQ, <c>[Pure]</c>, <c>TryParse</c>, object
/// creation) are left alone. The clean path is two syntax-kind tests; nothing binds until a statement is a
/// bare member call.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2418DiscardedImmutableResultAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.DiscardedImmutableResult);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Resolves the span-like types once, then analyzes each expression statement.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var spanTypes = new[]
        {
            context.Compilation.GetTypeByMetadataName("System.Span`1"),
            context.Compilation.GetTypeByMetadataName("System.ReadOnlySpan`1"),
            context.Compilation.GetTypeByMetadataName("System.Memory`1"),
            context.Compilation.GetTypeByMetadataName("System.ReadOnlyMemory`1"),
        };
        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, spanTypes), SyntaxKind.ExpressionStatement);
    }

    /// <summary>Reports one discarded immutable-value result.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="spanTypes">The resolved span and memory type definitions.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol?[] spanTypes)
    {
        if (((ExpressionStatementSyntax)context.Node).Expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax } invocation)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { ReturnsVoid: false } method
            || method.ContainingType is not { } container
            || IsAlreadyReported(method, container)
            || !IsDiscardedImmutableResult(method, container, spanTypes))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.DiscardedImmutableResult, invocation.GetLocation(), container.Name, method.Name));
    }

    /// <summary>Returns whether an unused-object or unused-string diagnostic already covers this call.</summary>
    /// <param name="method">The resolved method.</param>
    /// <param name="container">The method's containing type.</param>
    /// <returns><see langword="true"/> when the shape is reported elsewhere.</returns>
    private static bool IsAlreadyReported(IMethodSymbol method, INamedTypeSymbol container)
        => container.SpecialType == SpecialType.System_String
            || method.Name == "TryParse"
            || (container.Name == "Enumerable" && container.ContainingNamespace?.ToDisplayString() == "System.Linq")
            || HasPureAttribute(method);

    /// <summary>Returns whether a method carries a purity attribute.</summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> when the method is marked <c>[Pure]</c>.</returns>
    private static bool HasPureAttribute(IMethodSymbol method)
    {
        var attributes = method.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (attributes[i].AttributeClass?.Name == "PureAttribute")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the call's only effect is a discarded immutable value.</summary>
    /// <param name="method">The resolved method.</param>
    /// <param name="container">The method's containing type.</param>
    /// <param name="spanTypes">The resolved span and memory type definitions.</param>
    /// <returns><see langword="true"/> when the discarded result makes the call pointless.</returns>
    private static bool IsDiscardedImmutableResult(IMethodSymbol method, INamedTypeSymbol container, INamedTypeSymbol?[] spanTypes)
        => IsStatelessHelper(method, container)
            || IsSpanLike(method.ReturnType, spanTypes)
            || IsFallbackImmutableValueType(container)
            || IsReadonlyStructSelfReturn(method, container);

    /// <summary>Returns whether the method is a static member of a stateless numeric helper class.</summary>
    /// <param name="method">The method.</param>
    /// <param name="container">The containing type.</param>
    /// <returns><see langword="true"/> for <c>Math</c>, <c>MathF</c>, <c>Convert</c>, <c>HashCode</c>, <c>BitConverter</c>.</returns>
    private static bool IsStatelessHelper(IMethodSymbol method, INamedTypeSymbol container)
        => method.IsStatic
            && container.ContainingNamespace?.Name == "System"
            && container.Name is "Math" or "MathF" or "Convert" or "HashCode" or "BitConverter";

    /// <summary>Returns whether a return type is a span or memory slice.</summary>
    /// <param name="returnType">The method's return type.</param>
    /// <param name="spanTypes">The resolved span and memory type definitions.</param>
    /// <returns><see langword="true"/> when the returned slice is thrown away.</returns>
    private static bool IsSpanLike(ITypeSymbol returnType, INamedTypeSymbol?[] spanTypes)
    {
        var definition = returnType.OriginalDefinition;
        for (var i = 0; i < spanTypes.Length; i++)
        {
            if (spanTypes[i] is { } spanType && SymbolEqualityComparer.Default.Equals(definition, spanType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is a BCL value type that is immutable but not always marked readonly.</summary>
    /// <param name="container">The containing type.</param>
    /// <returns><see langword="true"/> for <c>DateTime</c>, <c>TimeSpan</c>, <c>Guid</c>, or <c>decimal</c>.</returns>
    private static bool IsFallbackImmutableValueType(INamedTypeSymbol container)
        => container.SpecialType == SpecialType.System_Decimal
            || (container.ContainingNamespace?.Name == "System" && container.Name is "DateTime" or "TimeSpan" or "Guid");

    /// <summary>Returns whether a readonly struct method returns its own type and mutates no argument.</summary>
    /// <param name="method">The method.</param>
    /// <param name="container">The containing type.</param>
    /// <returns><see langword="true"/> for a fluent value-returning method on a readonly struct.</returns>
    private static bool IsReadonlyStructSelfReturn(IMethodSymbol method, INamedTypeSymbol container)
        => container is { IsReadOnly: true, IsValueType: true }
            && SymbolEqualityComparer.Default.Equals(method.ReturnType, container)
            && !HasByReferenceParameter(method);

    /// <summary>Returns whether any parameter is passed by <c>ref</c> or <c>out</c>.</summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> when the call could write through an argument.</returns>
    private static bool HasByReferenceParameter(IMethodSymbol method)
    {
        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].RefKind is RefKind.Ref or RefKind.Out)
            {
                return true;
            }
        }

        return false;
    }
}
