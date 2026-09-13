// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>System.ComponentModel.DefaultValueAttribute</c> applied to a method (or record
/// primary-constructor) parameter (SST2460). On a parameter the attribute is inert: it does not make the
/// parameter optional and the compiler never consults it when a call site omits the argument, so the caller
/// must still pass the value. The author almost always meant a real optional parameter
/// (<c>= value</c>) or, for interop, <c>System.Runtime.InteropServices.DefaultParameterValueAttribute</c>.
/// </summary>
/// <remarks>
/// The clean path is a syntax check: a parameter with no attribute lists returns before the semantic model is
/// touched, and only an attribute whose simple name is <c>DefaultValue</c>/<c>DefaultValueAttribute</c> and
/// that lands on the parameter itself (no target, or <c>[param:]</c>) is bound. <c>DefaultValueAttribute</c>
/// is resolved on first demand per compilation after the name and target checks pass. An attribute retargeted to the record's
/// generated property or field with <c>[property:]</c>/<c>[field:]</c> reaches a home that really does read it
/// and is left alone.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2460DefaultValueOnParameterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the designer default-value attribute.</summary>
    internal const string DefaultValueMetadataName = "System.ComponentModel.DefaultValueAttribute";

    /// <summary>The metadata name of the interop parameter-default attribute.</summary>
    internal const string DefaultParameterValueMetadataName = "System.Runtime.InteropServices.DefaultParameterValueAttribute";

    /// <summary>The attribute target specifier that keeps an attribute on the parameter itself.</summary>
    private const string ParameterTarget = "param";

    /// <summary>The attribute's unqualified spelling without the redundant suffix.</summary>
    private const string DefaultValueName = "DefaultValue";

    /// <summary>The attribute's unqualified spelling with the explicit suffix.</summary>
    private const string DefaultValueSuffixedName = "DefaultValueAttribute";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.DefaultValueOnParameter);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the parameter walk with deferred designer-attribute resolution.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var defaultValueAttribute = new DefaultValueType(context.Compilation);
        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, defaultValueAttribute), SyntaxKind.Parameter);
    }

    /// <summary>Reports the designer attribute on a parameter that no call site reads.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="defaultValueAttribute">The designer attribute resolved on first demand.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, DefaultValueType defaultValueAttribute)
    {
        var parameter = (ParameterSyntax)context.Node;
        var attributeLists = parameter.AttributeLists;
        if (attributeLists.Count == 0)
        {
            return;
        }

        foreach (var attributeList in attributeLists)
        {
            // A record positional parameter can retarget its attributes to the generated property or field
            // with [property:]/[field:]; those homes really do read DefaultValue, so only an attribute that
            // lands on the parameter itself (no target, or [param:]) is a candidate.
            if (attributeList.Target is { } target && target.Identifier.ValueText is not ParameterTarget)
            {
                continue;
            }

            foreach (var attribute in attributeList.Attributes)
            {
                if (!IsDefaultValueName(attribute.Name))
                {
                    continue;
                }

                if (!IsDesignerAttribute(context, parameter, attribute, defaultValueAttribute))
                {
                    continue;
                }

                context.ReportDiagnostic(DiagnosticHelper.Create(
                    CorrectnessRules.DefaultValueOnParameter,
                    attribute.GetLocation(),
                    parameter.Identifier.ValueText));
            }
        }
    }

    /// <summary>Confirms the designer attribute after excluding unrelated declaration attributes.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameter">The parameter carrying the attribute.</param>
    /// <param name="attribute">The candidate attribute syntax.</param>
    /// <param name="defaultValueAttribute">The cached designer attribute type.</param>
    /// <returns>Whether the attribute constructor belongs to the designer attribute.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDesignerAttribute(
        in SyntaxNodeAnalysisContext context,
        ParameterSyntax parameter,
        AttributeSyntax attribute,
        DefaultValueType defaultValueAttribute) =>
        defaultValueAttribute.Get() is { } attributeType
            && !IsUnrelatedAttribute(context, parameter, attribute, attributeType)
            && context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is IMethodSymbol constructor
            && SymbolEqualityComparer.Default.Equals(constructor.ContainingType, attributeType);

    /// <summary>Excludes an unrelated attribute using declaration data before creating an attribute semantic model.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameter">The parameter carrying the attribute.</param>
    /// <param name="attribute">The candidate attribute syntax.</param>
    /// <param name="attributeType">The resolved designer attribute type.</param>
    /// <returns>Whether the declaration has already resolved the candidate to a different attribute type.</returns>
    private static bool IsUnrelatedAttribute(
        in SyntaxNodeAnalysisContext context,
        ParameterSyntax parameter,
        AttributeSyntax attribute,
        INamedTypeSymbol attributeType)
    {
        if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is not { } symbol)
        {
            return false;
        }

        var attributes = symbol.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            var candidate = attributes[i];
            if (candidate.ApplicationSyntaxReference is { } reference
                && reference.SyntaxTree == attribute.SyntaxTree
                && reference.Span == attribute.Span)
            {
                return candidate.AttributeClass is { } candidateType
                    && candidateType.TypeKind != TypeKind.Error
                    && !SymbolEqualityComparer.Default.Equals(candidateType, attributeType);
            }
        }

        return false;
    }

    /// <summary>Returns whether an attribute name is spelled <c>DefaultValue</c> or <c>DefaultValueAttribute</c>.</summary>
    /// <param name="name">The attribute name syntax.</param>
    /// <returns><see langword="true"/> when the simple name matches either spelling.</returns>
    private static bool IsDefaultValueName(NameSyntax name) =>
        GetSimpleName(name) is DefaultValueName or DefaultValueSuffixedName;

    /// <summary>Reduces an attribute name to its rightmost identifier text.</summary>
    /// <param name="name">The attribute name syntax.</param>
    /// <returns>The simple identifier text, or <see langword="null"/> for an unexpected shape.</returns>
    private static string? GetSimpleName(NameSyntax name) => name switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Resolves the designer attribute only when a parameter attribute is a candidate.</summary>
    /// <param name="compilation">The compilation whose designer attribute is resolved.</param>
    private sealed class DefaultValueType(Compilation compilation)
    {
        /// <summary>The cached attribute type, including a null slot when it is unavailable.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the designer attribute on first demand, caching its absence too.</summary>
        /// <returns>The designer attribute type, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(DefaultValueMetadataName)])[0];
    }
}
