// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an implicit reference conversion from a reference-type array to an array of its element type's
/// base type (SST2434): <c>string[]</c> handed over as <c>object[]</c>, at an assignment, initialiser,
/// argument, return, or as the left operand of <c>??</c>. Every write through the widened reference becomes a
/// runtime-checked store that can throw <see cref="ArrayTypeMismatchException"/>.
/// </summary>
/// <remarks>
/// The guard chain rejects a conversion the moment any link fails, and each link is cheap: the conversion must
/// be a reference conversion, both its operand and result must be array types, their element types must
/// differ, and the source element must be a reference type. Only that exact shape is array covariance. The
/// message mentions <c>ReadOnlySpan&lt;T&gt;</c> as a read-only alternative only when the compilation has one,
/// which is resolved once at compilation start.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2434ArrayCovarianceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the read-only span type.</summary>
    private const string ReadOnlySpanMetadataName = "System.ReadOnlySpan`1";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.ArrayCovariance);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var readOnlySpanResolves = start.Compilation.GetTypeByMetadataName(ReadOnlySpanMetadataName) is not null;
            start.RegisterOperationAction(operationContext => AnalyzeConversion(operationContext, readOnlySpanResolves), OperationKind.Conversion);
            start.RegisterOperationAction(operationContext => AnalyzeCoalesce(operationContext, readOnlySpanResolves), OperationKind.Coalesce);
        });
    }

    /// <summary>Analyzes one conversion for array covariance.</summary>
    /// <param name="context">The operation analysis context.</param>
    /// <param name="readOnlySpanResolves">Whether the compilation can name <c>ReadOnlySpan&lt;T&gt;</c>.</param>
    private static void AnalyzeConversion(in OperationAnalysisContext context, bool readOnlySpanResolves)
    {
        var conversion = (IConversionOperation)context.Operation;

        // A covariant array passed to a params parameter is the language's own doing, not the caller's; leave it.
        if (!conversion.Conversion.IsReference || conversion.Parent is IArgumentOperation { Parameter.IsParams: true })
        {
            return;
        }

        Report(context, conversion.Operand.Type, conversion.Type, conversion.Syntax, readOnlySpanResolves);
    }

    /// <summary>Analyzes the widened operand of a null-coalescing expression for array covariance.</summary>
    /// <param name="context">The operation analysis context.</param>
    /// <param name="readOnlySpanResolves">Whether the compilation can name <c>ReadOnlySpan&lt;T&gt;</c>.</param>
    /// <remarks>
    /// The conversion applied to the left operand of <c>??</c> is carried by the coalesce operation itself
    /// rather than by a nested conversion operation, so it is invisible to the conversion callback and has to
    /// be read from the operation directly.
    /// </remarks>
    private static void AnalyzeCoalesce(in OperationAnalysisContext context, bool readOnlySpanResolves)
    {
        var coalesce = (ICoalesceOperation)context.Operation;
        if (!coalesce.ValueConversion.IsReference)
        {
            return;
        }

        Report(context, coalesce.Value.Type, coalesce.Type, coalesce.Value.Syntax, readOnlySpanResolves);
    }

    /// <summary>Reports SST2434 when a reference conversion widens an array to an array of a base element type.</summary>
    /// <param name="context">The operation analysis context.</param>
    /// <param name="sourceType">The converted operand's type.</param>
    /// <param name="targetType">The conversion's result type.</param>
    /// <param name="syntax">The syntax to report on.</param>
    /// <param name="readOnlySpanResolves">Whether the compilation can name <c>ReadOnlySpan&lt;T&gt;</c>.</param>
    private static void Report(
        in OperationAnalysisContext context,
        ITypeSymbol? sourceType,
        ITypeSymbol? targetType,
        SyntaxNode syntax,
        bool readOnlySpanResolves)
    {
        if (sourceType is not IArrayTypeSymbol source || targetType is not IArrayTypeSymbol target)
        {
            return;
        }

        var sourceElement = source.ElementType;
        if (SymbolEqualityComparer.Default.Equals(sourceElement, target.ElementType) || !sourceElement.IsReferenceType)
        {
            return;
        }

        var sourceDisplay = source.ToDisplayString();
        var elementDisplay = sourceElement.ToDisplayString();
        var advice = $"use IReadOnlyList<{elementDisplay}> for read-only access, or keep the array typed '{sourceDisplay}'";
        if (readOnlySpanResolves)
        {
            advice += $", or ReadOnlySpan<{elementDisplay}>";
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.ArrayCovariance,
            syntax.GetLocation(),
            sourceDisplay,
            target.ToDisplayString(),
            advice));
    }
}
