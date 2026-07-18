// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
/// is resolved once per compilation and the whole rule is gated on it. An attribute retargeted to the record's
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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.DefaultValueOnParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the parameter walk only when the designer attribute resolves.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(DefaultValueMetadataName) is not { } defaultValueAttribute)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, defaultValueAttribute), SyntaxKind.Parameter);
    }

    /// <summary>Reports the designer attribute on a parameter that no call site reads.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="defaultValueAttribute">The resolved designer attribute symbol.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol defaultValueAttribute)
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

                if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
                    || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, defaultValueAttribute))
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

    /// <summary>Returns whether an attribute name is spelled <c>DefaultValue</c> or <c>DefaultValueAttribute</c>.</summary>
    /// <param name="name">The attribute name syntax.</param>
    /// <returns><see langword="true"/> when the simple name matches either spelling.</returns>
    private static bool IsDefaultValueName(NameSyntax name)
        => GetSimpleName(name) is DefaultValueName or DefaultValueSuffixedName;

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
}
