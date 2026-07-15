// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an implicit reference conversion from a reference-type array to an array of its element type's
/// base type (SST2434): <c>string[]</c> handed over as <c>object[]</c>, at an assignment, initialiser,
/// argument, or return. Every write through the widened reference becomes a runtime-checked store that can
/// throw <see cref="ArrayTypeMismatchException"/>.
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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.ArrayCovariance);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var readOnlySpanResolves = start.Compilation.GetTypeByMetadataName(ReadOnlySpanMetadataName) is not null;
            start.RegisterOperationAction(operationContext => Analyze(operationContext, readOnlySpanResolves), OperationKind.Conversion);
        });
    }

    /// <summary>Analyzes one conversion for array covariance.</summary>
    /// <param name="context">The operation analysis context.</param>
    /// <param name="readOnlySpanResolves">Whether the compilation can name <c>ReadOnlySpan&lt;T&gt;</c>.</param>
    private static void Analyze(OperationAnalysisContext context, bool readOnlySpanResolves)
    {
        var conversion = (IConversionOperation)context.Operation;
        if (!conversion.Conversion.IsReference
            || conversion.Operand.Type is not IArrayTypeSymbol source
            || conversion.Type is not IArrayTypeSymbol target)
        {
            return;
        }

        var sourceElement = source.ElementType;
        if (SymbolEqualityComparer.Default.Equals(sourceElement, target.ElementType) || !sourceElement.IsReferenceType)
        {
            return;
        }

        // A covariant array passed to a params parameter is the language's own doing, not the caller's; leave it.
        if (conversion.Parent is IArgumentOperation { Parameter.IsParams: true })
        {
            return;
        }

        var sourceDisplay = source.ToDisplayString();
        var elementDisplay = sourceElement.ToDisplayString();
        var advice = "use IReadOnlyList<" + elementDisplay + "> for read-only access, or keep the array typed '" + sourceDisplay + "'";
        if (readOnlySpanResolves)
        {
            advice += ", or ReadOnlySpan<" + elementDisplay + ">";
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.ArrayCovariance,
            conversion.Syntax.GetLocation(),
            sourceDisplay,
            target.ToDisplayString(),
            advice));
    }
}
