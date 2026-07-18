// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the three ways a Managed Extensibility Framework (MEF) part contradicts its own composition
/// metadata, in a single walk. The attributes that wire a part into the container — its export contract and
/// its creation policy — are promises the compiler cannot check, so a part that breaks one of them compiles
/// and only fails, or silently misbehaves, once it is composed.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST2472 — a type is exported for a contract (<c>[Export(typeof(IFoo))]</c>) that it neither
/// implements nor inherits, so the container cannot supply it for that contract.</description></item>
/// <item><description>SST2473 — a <c>new</c> expression directly constructs a type that is itself a shared export
/// part, bypassing the container and its single-instance guarantee.</description></item>
/// <item><description>SST2474 — a part-creation-policy attribute is applied to a type that has no export, so the
/// attribute governs nothing.</description></item>
/// </list>
/// <para>
/// Both MEF flavors are supported: <c>System.ComponentModel.Composition</c> (its <c>ExportAttribute</c> and
/// <c>PartCreationPolicyAttribute</c>) and <c>System.Composition</c> (its <c>ExportAttribute</c> and
/// <c>SharedAttribute</c>). The whole analyzer is gated at compilation start on at least one of those marker
/// attributes resolving; a project that references no MEF assembly registers nothing and pays nothing.
/// </para>
/// <para>
/// The clean path stays cheap. The attribute rules reject every non-MEF attribute on its written simple name
/// alone, before any binding, and only an attribute whose name matches is bound to confirm it really is the
/// marker type. The object-creation rule registers only when both an export and a shared marker resolve, so a
/// MEF project that never constructs a shared part directly still pays only a symbol lookup per <c>new</c>.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MefContractAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the MEF1 export attribute.</summary>
    private const string Mef1ExportMetadataName = "System.ComponentModel.Composition.ExportAttribute";

    /// <summary>The metadata name of the MEF1 part-creation-policy attribute.</summary>
    private const string Mef1PartCreationPolicyMetadataName = "System.ComponentModel.Composition.PartCreationPolicyAttribute";

    /// <summary>The metadata name of the MEF1 creation-policy enum.</summary>
    private const string Mef1CreationPolicyMetadataName = "System.ComponentModel.Composition.CreationPolicy";

    /// <summary>The metadata name of the MEF2 export attribute.</summary>
    private const string Mef2ExportMetadataName = "System.Composition.ExportAttribute";

    /// <summary>The metadata name of the MEF2 shared attribute.</summary>
    private const string Mef2SharedMetadataName = "System.Composition.SharedAttribute";

    /// <summary>The name of the creation-policy enum's shared member.</summary>
    private const string SharedPolicyFieldName = "Shared";

    /// <summary>The category an attribute's written name places it in.</summary>
    private enum MefAttributeKind
    {
        /// <summary>Not a MEF attribute this analyzer cares about.</summary>
        None,

        /// <summary>An export attribute (<c>[Export]</c>).</summary>
        Export,

        /// <summary>A creation-policy attribute (<c>[PartCreationPolicy]</c> or <c>[Shared]</c>).</summary>
        CreationPolicy,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        CorrectnessRules.ExportedContractNotImplemented,
        CorrectnessRules.SharedExportPartConstructedDirectly,
        CorrectnessRules.CreationPolicyWithoutExport);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var symbols = MefContractSymbols.Resolve(start.Compilation);
            if (symbols is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAttribute(nodeContext, symbols), SyntaxKind.Attribute);

            if (symbols.HasAnyExport && symbols.HasAnySharedMarker)
            {
                start.RegisterSyntaxNodeAction(
                    nodeContext => AnalyzeObjectCreation(nodeContext, symbols),
                    SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.ImplicitObjectCreationExpression);
            }
        });
    }

    /// <summary>Routes a MEF attribute to the export or creation-policy rule after a syntactic name match.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="symbols">The resolved MEF marker symbols.</param>
    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, MefContractSymbols symbols)
    {
        var attribute = (AttributeSyntax)context.Node;
        var kind = ClassifyAttributeName(GetSimpleName(attribute.Name));
        if (kind == MefAttributeKind.None)
        {
            return;
        }

        if (attribute.Parent is not AttributeListSyntax { Parent: TypeDeclarationSyntax typeDeclaration })
        {
            return;
        }

        if (kind == MefAttributeKind.Export)
        {
            AnalyzeExport(context, symbols, attribute, typeDeclaration);
        }
        else
        {
            AnalyzeCreationPolicy(context, symbols, attribute, typeDeclaration);
        }
    }

    /// <summary>Reports SST2472 when an exported contract type is not assignable from the exporting type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="symbols">The resolved MEF marker symbols.</param>
    /// <param name="attribute">The candidate export attribute.</param>
    /// <param name="typeDeclaration">The type the attribute is written on.</param>
    private static void AnalyzeExport(SyntaxNodeAnalysisContext context, MefContractSymbols symbols, AttributeSyntax attribute, TypeDeclarationSyntax typeDeclaration)
    {
        if (!symbols.HasAnyExport)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(typeDeclaration, context.CancellationToken) is not { } exportingType)
        {
            return;
        }

        if (FindAttributeData(exportingType, attribute) is not { AttributeClass: { } attributeClass } data
            || !symbols.IsExport(attributeClass))
        {
            return;
        }

        if (!TryGetContractType(data, out var contract) || IsAssignableToContract(exportingType, contract))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.ExportedContractNotImplemented,
            attribute.GetLocation(),
            contract.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    /// <summary>Reports SST2474 when a creation-policy attribute is applied to a type that is not exported.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="symbols">The resolved MEF marker symbols.</param>
    /// <param name="attribute">The candidate creation-policy attribute.</param>
    /// <param name="typeDeclaration">The type the attribute is written on.</param>
    private static void AnalyzeCreationPolicy(SyntaxNodeAnalysisContext context, MefContractSymbols symbols, AttributeSyntax attribute, TypeDeclarationSyntax typeDeclaration)
    {
        if (!symbols.HasAnyExport)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(typeDeclaration, context.CancellationToken) is not { } declaredType)
        {
            return;
        }

        if (FindAttributeData(declaredType, attribute) is not { AttributeClass: { } attributeClass }
            || !symbols.IsCreationPolicy(attributeClass))
        {
            return;
        }

        if (symbols.HasExportAttribute(declaredType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.CreationPolicyWithoutExport, attribute.GetLocation()));
    }

    /// <summary>Reports SST2473 when a <c>new</c> expression constructs a shared export part directly.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="symbols">The resolved MEF marker symbols.</param>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, MefContractSymbols symbols)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor, ContainingType: { } constructedType })
        {
            return;
        }

        if (!symbols.HasExportAttribute(constructedType) || !symbols.IsSharedPart(constructedType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.SharedExportPartConstructedDirectly,
            creation.GetLocation(),
            constructedType.Name));
    }

    /// <summary>Finds the attribute data on a symbol that a given attribute syntax produced.</summary>
    /// <param name="symbol">The symbol carrying the attribute.</param>
    /// <param name="attribute">The attribute syntax to match.</param>
    /// <returns>The matching attribute data, or <see langword="null"/> when none matches.</returns>
    private static AttributeData? FindAttributeData(ISymbol symbol, AttributeSyntax attribute)
    {
        var attributes = symbol.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            var reference = attributes[i].ApplicationSyntaxReference;
            if (reference is not null && reference.Span == attribute.Span && reference.SyntaxTree == attribute.SyntaxTree)
            {
                return attributes[i];
            }
        }

        return null;
    }

    /// <summary>Reads the contract type an export attribute names, if any.</summary>
    /// <param name="data">The bound export attribute.</param>
    /// <param name="contract">The contract type, when one was supplied.</param>
    /// <returns><see langword="true"/> when a <c>typeof(...)</c> contract argument is present.</returns>
    private static bool TryGetContractType(AttributeData data, [NotNullWhen(true)] out ITypeSymbol? contract)
    {
        var arguments = data.ConstructorArguments;
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Kind == TypedConstantKind.Type
                && arguments[i].Value is ITypeSymbol { TypeKind: not TypeKind.Error } type)
            {
                contract = type;
                return true;
            }
        }

        contract = null;
        return false;
    }

    /// <summary>Returns whether a type implements, inherits, or is the named contract.</summary>
    /// <param name="type">The exporting type.</param>
    /// <param name="contract">The declared contract type.</param>
    /// <returns><see langword="true"/> when the contract is assignable from the type.</returns>
    private static bool IsAssignableToContract(INamedTypeSymbol type, ITypeSymbol contract)
    {
        if (Matches(type, contract))
        {
            return true;
        }

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (Matches(baseType, contract))
            {
                return true;
            }
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (Matches(interfaces[i], contract))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether one type is the contract, comparing closed and open generic forms.</summary>
    /// <param name="candidate">The candidate type from the exporting type's hierarchy.</param>
    /// <param name="contract">The declared contract type.</param>
    /// <returns><see langword="true"/> when they are the same type.</returns>
    private static bool Matches(ITypeSymbol candidate, ITypeSymbol contract)
        => SymbolEqualityComparer.Default.Equals(candidate, contract)
            || SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, contract.OriginalDefinition);

    /// <summary>Classifies an attribute's written simple name as an export, a creation policy, or neither.</summary>
    /// <param name="simpleName">The attribute's rightmost identifier.</param>
    /// <returns>The attribute kind.</returns>
    private static MefAttributeKind ClassifyAttributeName(string simpleName) => simpleName switch
    {
        "Export" or "ExportAttribute" => MefAttributeKind.Export,
        "PartCreationPolicy" or "PartCreationPolicyAttribute" or "Shared" or "SharedAttribute" => MefAttributeKind.CreationPolicy,
        _ => MefAttributeKind.None,
    };

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased attribute name.</summary>
    /// <param name="name">The attribute name as written.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>
    /// The MEF marker attribute symbols, resolved once per compilation. Holds whichever of the two flavors'
    /// attributes are present so every membership test binds against a real type rather than a name.
    /// </summary>
    private sealed class MefContractSymbols
    {
        /// <summary>The MEF1 export attribute, when resolved.</summary>
        private readonly INamedTypeSymbol? _mef1Export;

        /// <summary>The MEF1 part-creation-policy attribute, when resolved.</summary>
        private readonly INamedTypeSymbol? _mef1PartCreationPolicy;

        /// <summary>The MEF2 export attribute, when resolved.</summary>
        private readonly INamedTypeSymbol? _mef2Export;

        /// <summary>The MEF2 shared attribute, when resolved.</summary>
        private readonly INamedTypeSymbol? _mef2Shared;

        /// <summary>The constant value of the MEF1 <c>CreationPolicy.Shared</c> member, when resolved.</summary>
        private readonly object? _sharedPolicyValue;

        /// <summary>Initializes a new instance of the <see cref="MefContractSymbols"/> class.</summary>
        /// <param name="mef1Export">The MEF1 export attribute, when resolved.</param>
        /// <param name="mef1PartCreationPolicy">The MEF1 part-creation-policy attribute, when resolved.</param>
        /// <param name="mef2Export">The MEF2 export attribute, when resolved.</param>
        /// <param name="mef2Shared">The MEF2 shared attribute, when resolved.</param>
        /// <param name="sharedPolicyValue">The constant value of the MEF1 <c>Shared</c> creation policy, when resolved.</param>
        private MefContractSymbols(
            INamedTypeSymbol? mef1Export,
            INamedTypeSymbol? mef1PartCreationPolicy,
            INamedTypeSymbol? mef2Export,
            INamedTypeSymbol? mef2Shared,
            object? sharedPolicyValue)
        {
            _mef1Export = mef1Export;
            _mef1PartCreationPolicy = mef1PartCreationPolicy;
            _mef2Export = mef2Export;
            _mef2Shared = mef2Shared;
            _sharedPolicyValue = sharedPolicyValue;
        }

        /// <summary>Gets a value indicating whether either flavor's export attribute resolved.</summary>
        public bool HasAnyExport => _mef1Export is not null || _mef2Export is not null;

        /// <summary>Gets a value indicating whether either flavor's shared or creation-policy attribute resolved.</summary>
        public bool HasAnySharedMarker => _mef1PartCreationPolicy is not null || _mef2Shared is not null;

        /// <summary>Resolves the MEF marker symbols, or <see langword="null"/> when no MEF attribute is present.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The resolved symbols, or <see langword="null"/> to disable the analyzer.</returns>
        public static MefContractSymbols? Resolve(Compilation compilation)
        {
            var mef1Export = compilation.GetTypeByMetadataName(Mef1ExportMetadataName);
            var mef1PartCreationPolicy = compilation.GetTypeByMetadataName(Mef1PartCreationPolicyMetadataName);
            var mef2Export = compilation.GetTypeByMetadataName(Mef2ExportMetadataName);
            var mef2Shared = compilation.GetTypeByMetadataName(Mef2SharedMetadataName);

            if (mef1Export is null && mef1PartCreationPolicy is null && mef2Export is null && mef2Shared is null)
            {
                return null;
            }

            var sharedPolicyValue = ReadSharedPolicyValue(compilation.GetTypeByMetadataName(Mef1CreationPolicyMetadataName));
            return new MefContractSymbols(mef1Export, mef1PartCreationPolicy, mef2Export, mef2Shared, sharedPolicyValue);
        }

        /// <summary>Returns whether an attribute class is one of the resolved export attributes.</summary>
        /// <param name="attributeClass">The bound attribute class.</param>
        /// <returns><see langword="true"/> for a MEF export attribute.</returns>
        public bool IsExport(INamedTypeSymbol attributeClass)
            => SymbolEqualityComparer.Default.Equals(attributeClass, _mef1Export)
                || SymbolEqualityComparer.Default.Equals(attributeClass, _mef2Export);

        /// <summary>Returns whether an attribute class is one of the resolved creation-policy attributes.</summary>
        /// <param name="attributeClass">The bound attribute class.</param>
        /// <returns><see langword="true"/> for a MEF creation-policy or shared attribute.</returns>
        public bool IsCreationPolicy(INamedTypeSymbol attributeClass)
            => SymbolEqualityComparer.Default.Equals(attributeClass, _mef1PartCreationPolicy)
                || SymbolEqualityComparer.Default.Equals(attributeClass, _mef2Shared);

        /// <summary>Returns whether a type carries a MEF export attribute.</summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns><see langword="true"/> when the type is exported.</returns>
        public bool HasExportAttribute(INamedTypeSymbol type)
        {
            var attributes = type.GetAttributes();
            for (var i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].AttributeClass is { } attributeClass && IsExport(attributeClass))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether a type is a shared (single-instance) export part.</summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns><see langword="true"/> for a <c>[Shared]</c> part or a <c>Shared</c> creation policy.</returns>
        public bool IsSharedPart(INamedTypeSymbol type)
        {
            var attributes = type.GetAttributes();
            for (var i = 0; i < attributes.Length; i++)
            {
                var attributeClass = attributes[i].AttributeClass;
                if (attributeClass is null)
                {
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(attributeClass, _mef2Shared))
                {
                    return true;
                }

                if (SymbolEqualityComparer.Default.Equals(attributeClass, _mef1PartCreationPolicy) && IsSharedPolicy(attributes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Reads the constant value of the <c>Shared</c> creation-policy enum member.</summary>
        /// <param name="creationPolicy">The resolved creation-policy enum, if any.</param>
        /// <returns>The <c>Shared</c> member's value, or <see langword="null"/>.</returns>
        private static object? ReadSharedPolicyValue(INamedTypeSymbol? creationPolicy)
        {
            if (creationPolicy is null)
            {
                return null;
            }

            var members = creationPolicy.GetMembers(SharedPolicyFieldName);
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is IFieldSymbol { HasConstantValue: true } field)
                {
                    return field.ConstantValue;
                }
            }

            return null;
        }

        /// <summary>Returns whether a creation-policy attribute names the <c>Shared</c> policy.</summary>
        /// <param name="data">The bound <c>PartCreationPolicy</c> attribute.</param>
        /// <returns><see langword="true"/> when its argument is <c>CreationPolicy.Shared</c>.</returns>
        private bool IsSharedPolicy(AttributeData data)
        {
            if (_sharedPolicyValue is null || data.ConstructorArguments.Length == 0)
            {
                return false;
            }

            return Equals(data.ConstructorArguments[0].Value, _sharedPolicyValue);
        }
    }
}
