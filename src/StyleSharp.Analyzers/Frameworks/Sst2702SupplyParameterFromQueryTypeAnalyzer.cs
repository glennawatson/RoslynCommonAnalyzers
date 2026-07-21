// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a property annotated <c>[SupplyParameterFromQuery]</c> whose type the framework cannot bind from a
/// query string (SST2702). The value is filled by parsing query text into the property's type, and only a fixed
/// set of types is parseable; a property of any other type compiles but throws when the component is navigated to.
/// </summary>
/// <remarks>
/// The supported set is bool, the numeric primitives (byte, sbyte, short, ushort, int, uint, long, ulong, float,
/// double, decimal), Guid, string, DateTime, DateTimeOffset, DateOnly, and TimeOnly, plus the <c>Nullable&lt;&gt;</c>
/// and single-dimension array forms of those. Each supported type is resolved against the compilation once, so a
/// target framework without DateOnly, TimeOnly, or DateTimeOffset simply never matches those — the rule never
/// suggests a type the compilation lacks.
/// <para>
/// The whole rule is gated at compilation start on the
/// <c>Microsoft.AspNetCore.Components.SupplyParameterFromQueryAttribute</c> marker resolving; a project that does
/// not reference it registers nothing and pays nothing. The property need not be public — the framework binds it
/// by reflection regardless of accessibility — so accessibility is not part of the check.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2702SupplyParameterFromQueryTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the query-parameter supply attribute.</summary>
    private const string SupplyParameterFromQueryAttributeMetadataName = "Microsoft.AspNetCore.Components.SupplyParameterFromQueryAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.SupplyParameterFromQueryUnsupportedType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var model = QueryBindingModel.Resolve(start.Compilation);
            if (model is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => AnalyzeProperty(symbolContext, model), SymbolKind.Property);
        });
    }

    /// <summary>Reports a supplied query property whose type is outside the bindable set.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="model">The resolved marker and supported-type set.</param>
    private static void AnalyzeProperty(SymbolAnalysisContext context, QueryBindingModel model)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (!model.HasMarker(property))
        {
            return;
        }

        if (model.IsBindable(property.Type))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            FrameworksRules.SupplyParameterFromQueryUnsupportedType,
            property.Locations[0],
            property.Name,
            property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    /// <summary>
    /// The query-binding marker and the set of element types the framework can parse from a query string,
    /// resolved once per compilation so every membership test compares real symbols rather than names.
    /// </summary>
    private sealed class QueryBindingModel
    {
        /// <summary>The supply-from-query marker attribute.</summary>
        private readonly INamedTypeSymbol _marker;

        /// <summary>The element types the framework can bind, including the ones that may be absent.</summary>
        private readonly HashSet<ITypeSymbol> _supportedElementTypes;

        /// <summary>Initializes a new instance of the <see cref="QueryBindingModel"/> class.</summary>
        /// <param name="marker">The supply-from-query marker attribute.</param>
        /// <param name="supportedElementTypes">The element types the framework can bind.</param>
        private QueryBindingModel(INamedTypeSymbol marker, HashSet<ITypeSymbol> supportedElementTypes)
        {
            _marker = marker;
            _supportedElementTypes = supportedElementTypes;
        }

        /// <summary>Resolves the marker and supported-type set, or <see langword="null"/> when the marker is absent.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The resolved model, or <see langword="null"/> to disable the rule.</returns>
        public static QueryBindingModel? Resolve(Compilation compilation)
        {
            var marker = compilation.GetTypeByMetadataName(SupplyParameterFromQueryAttributeMetadataName);
            if (marker is null)
            {
                return null;
            }

            var supported = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            AddSpecial(supported, compilation, SpecialType.System_Boolean);
            AddSpecial(supported, compilation, SpecialType.System_Byte);
            AddSpecial(supported, compilation, SpecialType.System_SByte);
            AddSpecial(supported, compilation, SpecialType.System_Int16);
            AddSpecial(supported, compilation, SpecialType.System_UInt16);
            AddSpecial(supported, compilation, SpecialType.System_Int32);
            AddSpecial(supported, compilation, SpecialType.System_UInt32);
            AddSpecial(supported, compilation, SpecialType.System_Int64);
            AddSpecial(supported, compilation, SpecialType.System_UInt64);
            AddSpecial(supported, compilation, SpecialType.System_Single);
            AddSpecial(supported, compilation, SpecialType.System_Double);
            AddSpecial(supported, compilation, SpecialType.System_Decimal);
            AddSpecial(supported, compilation, SpecialType.System_String);
            AddSpecial(supported, compilation, SpecialType.System_DateTime);
            AddResolved(supported, compilation, "System.Guid");
            AddResolved(supported, compilation, "System.DateTimeOffset");
            AddResolved(supported, compilation, "System.DateOnly");
            AddResolved(supported, compilation, "System.TimeOnly");

            return new QueryBindingModel(marker, supported);
        }

        /// <summary>Returns whether a property carries the supply-from-query marker attribute.</summary>
        /// <param name="property">The property to inspect.</param>
        /// <returns><see langword="true"/> when the marker is present.</returns>
        public bool HasMarker(IPropertySymbol property)
        {
            var attributes = property.GetAttributes();
            for (var i = 0; i < attributes.Length; i++)
            {
                if (SymbolEqualityComparer.Default.Equals(attributes[i].AttributeClass, _marker))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether a property type is one the framework can bind from a query string.</summary>
        /// <param name="type">The property type.</param>
        /// <returns><see langword="true"/> for a supported type or its nullable/array form.</returns>
        public bool IsBindable(ITypeSymbol type)
        {
            // Unwrap a single-dimension array to its element; a multi-dimensional or jagged array is not bindable.
            if (type is IArrayTypeSymbol { Rank: 1 } singleDimensionArray)
            {
                type = singleDimensionArray.ElementType;
            }
            else if (type is IArrayTypeSymbol)
            {
                return false;
            }

            // Unwrap a Nullable<T> value-type wrapper so the element is measured against the supported set.
            if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named)
            {
                type = named.TypeArguments[0];
            }

            // A broken type says nothing about intent; leave it to the compiler rather than adding noise.
            return type.TypeKind == TypeKind.Error || _supportedElementTypes.Contains(type);
        }

        /// <summary>Adds a special type to the supported set.</summary>
        /// <param name="set">The supported-type set.</param>
        /// <param name="compilation">The compilation to resolve against.</param>
        /// <param name="specialType">The special type to add.</param>
        private static void AddSpecial(HashSet<ITypeSymbol> set, Compilation compilation, SpecialType specialType)
            => set.Add(compilation.GetSpecialType(specialType));

        /// <summary>Adds a metadata-named type to the supported set when it resolves.</summary>
        /// <param name="set">The supported-type set.</param>
        /// <param name="compilation">The compilation to resolve against.</param>
        /// <param name="metadataName">The metadata name to resolve.</param>
        private static void AddResolved(HashSet<ITypeSymbol> set, Compilation compilation, string metadataName)
        {
            var resolved = compilation.GetTypeByMetadataName(metadataName);
            if (resolved is null)
            {
                return;
            }

            set.Add(resolved);
        }
    }
}
