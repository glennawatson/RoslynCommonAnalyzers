// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a <see cref="System.Collections.BitArray"/> built from an array created at the call site,
/// where a span constructor reads the same values without the throwaway array (PSH1024).
/// </summary>
/// <remarks>
/// Gated on a span constructor existing in the analyzed compilation, so a project on a framework
/// without one is never told to call an overload it does not have. An array the caller already holds
/// is not reported: it is not a temporary, and passing it allocates nothing extra.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1024BitArraySpanConstructorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the bit array.</summary>
    private const string BitArrayMetadataName = "System.Collections.BitArray";

    /// <summary>The metadata name of the read-only span.</summary>
    private const string ReadOnlySpanMetadataName = "System.ReadOnlySpan`1";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue =
        ImmutableArrays.Of(AllocationRules.PreferBitArraySpanConstructor);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var bitArray = start.Compilation.GetTypeByMetadataName(BitArrayMetadataName);
            var readOnlySpan = start.Compilation.GetTypeByMetadataName(ReadOnlySpanMetadataName);
            if (bitArray is null || readOnlySpan is null || !HasSpanConstructor(bitArray, readOnlySpan))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, bitArray),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Reports one bit array built from a throwaway array.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="bitArray">The resolved bit-array type.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, INamedTypeSymbol bitArray)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is not { Arguments.Count: 1 } arguments
            || !IsTemporaryArray(arguments.Arguments[0].Expression))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, bitArray)
            || constructor.Parameters.Length != 1
            || constructor.Parameters[0].Type is not IArrayTypeSymbol arrayType)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.PreferBitArraySpanConstructor,
            arguments.Arguments[0].Expression.GetLocation(),
            $"ReadOnlySpan<{arrayType.ElementType.Name}>"));
    }

    /// <summary>Gets whether the argument allocates an array that exists only for this call.</summary>
    /// <param name="expression">The single constructor argument.</param>
    /// <returns><see langword="true"/> for an array created inline.</returns>
    private static bool IsTemporaryArray(ExpressionSyntax expression) =>
        expression is ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax;

    /// <summary>Gets whether the bit array offers a constructor taking a read-only span.</summary>
    /// <param name="bitArray">The resolved bit-array type.</param>
    /// <param name="readOnlySpan">The resolved read-only span definition.</param>
    /// <returns><see langword="true"/> when a span constructor is available.</returns>
    private static bool HasSpanConstructor(INamedTypeSymbol bitArray, INamedTypeSymbol readOnlySpan)
    {
        foreach (var constructor in bitArray.InstanceConstructors)
        {
            if (constructor.Parameters.Length == 1
                && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type.OriginalDefinition, readOnlySpan))
            {
                return true;
            }
        }

        return false;
    }
}
