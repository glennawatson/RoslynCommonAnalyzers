// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a routable component whose route template constrains a segment to one type while the component
/// parameter that receives it is another (SST2703). A typed segment such as <c>{id:int}</c> tells the router to
/// accept the segment only as that type and to hand the parsed value to the same-named parameter; when the
/// constraint and the parameter type disagree the binding fails or is coerced at runtime, not at build time.
/// </summary>
/// <remarks>
/// The constraints mapped to a CLR type are <c>int</c>, <c>long</c>, <c>guid</c>, <c>bool</c>, <c>datetime</c>,
/// <c>decimal</c>, <c>double</c>, and <c>float</c>; other constraints (<c>alpha</c>, <c>regex</c>, <c>length</c>,
/// and the like) constrain the text without fixing a numeric or temporal type and are not checked. Only typed
/// <c>{name:constraint}</c> segments are examined; an untyped <c>{name}</c> segment is ignored. A parameter whose
/// type is the <c>Nullable&lt;&gt;</c> form of the constrained type matches, since <c>{id:int?}</c> and an
/// <c>int?</c> parameter agree.
/// <para>
/// The whole rule is gated at compilation start on both the <c>Microsoft.AspNetCore.Components.RouteAttribute</c>
/// and <c>Microsoft.AspNetCore.Components.ParameterAttribute</c> markers resolving; a project that references
/// neither registers nothing and pays nothing. The clean path scans each type's attributes for a route marker and
/// parses a template only for a type that carries one, so a non-routable type costs a single attribute scan.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2703RouteConstraintTypeMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the component route attribute.</summary>
    private const string RouteAttributeMetadataName = "Microsoft.AspNetCore.Components.RouteAttribute";

    /// <summary>The metadata name of the component parameter attribute.</summary>
    private const string ParameterAttributeMetadataName = "Microsoft.AspNetCore.Components.ParameterAttribute";

    /// <summary>The character span of a doubled brace escape (<c>{{</c> or <c>}}</c>).</summary>
    private const int EscapedBraceLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.RouteConstraintTypeMismatch);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var model = RouteBindingModel.Resolve(start.Compilation);
            if (model is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => AnalyzeType(symbolContext, model), SymbolKind.NamedType);
        });
    }

    /// <summary>Parses each route template on a type and reports every typed segment whose parameter type disagrees.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="model">The resolved markers and constraint-to-type map.</param>
    private static void AnalyzeType(SymbolAnalysisContext context, RouteBindingModel model)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class)
        {
            return;
        }

        var attributes = type.GetAttributes();
        HashSet<ISymbol>? reported = null;
        for (var i = 0; i < attributes.Length; i++)
        {
            if (!model.IsRoute(attributes[i].AttributeClass) || !TryGetTemplate(attributes[i], out var template))
            {
                continue;
            }

            reported ??= new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            InspectTemplate(context, model, type, template, reported);
        }
    }

    /// <summary>Reads the route template string a route attribute carries.</summary>
    /// <param name="attribute">The route attribute.</param>
    /// <param name="template">The template string, when present.</param>
    /// <returns><see langword="true"/> when a non-empty template argument is present.</returns>
    private static bool TryGetTemplate(AttributeData attribute, [NotNullWhen(true)] out string? template)
    {
        var arguments = attribute.ConstructorArguments;
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Kind == TypedConstantKind.Primitive && arguments[i].Value is string { Length: > 0 } value)
            {
                template = value;
                return true;
            }
        }

        template = null;
        return false;
    }

    /// <summary>Walks a template's typed segments and reports each one whose parameter type disagrees with the constraint.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="model">The resolved markers and constraint-to-type map.</param>
    /// <param name="type">The routable component type.</param>
    /// <param name="template">The route template.</param>
    /// <param name="reported">The set of already-reported parameters, guarding against a duplicate across templates.</param>
    private static void InspectTemplate(SymbolAnalysisContext context, RouteBindingModel model, INamedTypeSymbol type, string template, HashSet<ISymbol> reported)
    {
        var index = 0;
        while (index < template.Length)
        {
            var current = template[index];
            if (current == '{')
            {
                // A doubled brace is a literal '{', not the start of a segment.
                if (index + 1 < template.Length && template[index + 1] == '{')
                {
                    index += EscapedBraceLength;
                    continue;
                }

                var close = template.IndexOf('}', index + 1);
                if (close < 0)
                {
                    return;
                }

                InspectSegment(context, model, type, template.Substring(index + 1, close - index - 1), reported);
                index = close + 1;
                continue;
            }

            // A doubled closing brace is a literal '}'.
            index += current == '}' && index + 1 < template.Length && template[index + 1] == '}' ? EscapedBraceLength : 1;
        }
    }

    /// <summary>Reports a single typed segment when its constraint and the matching parameter type disagree.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="model">The resolved markers and constraint-to-type map.</param>
    /// <param name="type">The routable component type.</param>
    /// <param name="segment">The template segment content, without its enclosing braces.</param>
    /// <param name="reported">The set of already-reported parameters.</param>
    private static void InspectSegment(SymbolAnalysisContext context, RouteBindingModel model, INamedTypeSymbol type, string segment, HashSet<ISymbol> reported)
    {
        var colon = segment.IndexOf(':');
        if (colon < 0)
        {
            return;
        }

        var name = NormalizeName(segment, colon);
        if (name.Length == 0)
        {
            return;
        }

        var constraint = NormalizeConstraint(segment, colon + 1);
        if (constraint.Length == 0 || !model.TryGetConstraintType(constraint, out var constraintType))
        {
            return;
        }

        if (FindParameter(model, type, name) is not { } parameter)
        {
            return;
        }

        var parameterType = UnwrapNullable(parameter.Type);
        if (parameterType.TypeKind == TypeKind.Error || SymbolEqualityComparer.Default.Equals(parameterType, constraintType))
        {
            return;
        }

        if (!reported.Add(parameter))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            FrameworksRules.RouteConstraintTypeMismatch,
            parameter.Locations[0],
            name,
            constraint,
            parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    /// <summary>Reads a segment's parameter name, dropping a catch-all marker and any optional or default suffix.</summary>
    /// <param name="segment">The segment content.</param>
    /// <param name="colon">The index of the first colon.</param>
    /// <returns>The parameter name.</returns>
    private static string NormalizeName(string segment, int colon)
    {
        var start = 0;
        while (start < colon && segment[start] == '*')
        {
            start++;
        }

        var end = colon;
        for (var i = start; i < colon; i++)
        {
            if (segment[i] is '?' or '=')
            {
                end = i;
                break;
            }
        }

        return segment.Substring(start, end - start);
    }

    /// <summary>Reads the first constraint token, dropping any additional constraints, arguments, or optional marker.</summary>
    /// <param name="segment">The segment content.</param>
    /// <param name="start">The index just after the first colon.</param>
    /// <returns>The lowercased constraint token.</returns>
    private static string NormalizeConstraint(string segment, int start)
    {
        var end = segment.Length;
        for (var i = start; i < segment.Length; i++)
        {
            if (segment[i] is ':' or '(' or '=')
            {
                end = i;
                break;
            }
        }

        var token = segment.Substring(start, end - start).TrimEnd('?');
        return token.ToLowerInvariant();
    }

    /// <summary>Finds a same-named component parameter on the type or one of its base types.</summary>
    /// <param name="model">The resolved markers and constraint-to-type map.</param>
    /// <param name="type">The routable component type.</param>
    /// <param name="name">The route parameter name.</param>
    /// <returns>The matching parameter property, or <see langword="null"/> when none carries the parameter marker.</returns>
    private static IPropertySymbol? FindParameter(RouteBindingModel model, INamedTypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var members = current.GetMembers();
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is IPropertySymbol property
                    && string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                    && model.HasParameter(property))
                {
                    return property;
                }
            }
        }

        return null;
    }

    /// <summary>Unwraps a <c>Nullable&lt;T&gt;</c> value type to its underlying type.</summary>
    /// <param name="type">The type to unwrap.</param>
    /// <returns>The underlying type, or the type unchanged.</returns>
    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
        => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named
            ? named.TypeArguments[0]
            : type;

    /// <summary>
    /// The route and parameter marker attributes and the constraint-to-CLR-type map, resolved once per compilation
    /// so every membership and equality test compares real symbols rather than names.
    /// </summary>
    private sealed class RouteBindingModel
    {
        /// <summary>The route marker attribute.</summary>
        private readonly INamedTypeSymbol _routeAttribute;

        /// <summary>The parameter marker attribute.</summary>
        private readonly INamedTypeSymbol _parameterAttribute;

        /// <summary>The constraint keyword to CLR type map.</summary>
        private readonly Dictionary<string, ITypeSymbol> _constraintTypes;

        /// <summary>Initializes a new instance of the <see cref="RouteBindingModel"/> class.</summary>
        /// <param name="routeAttribute">The route marker attribute.</param>
        /// <param name="parameterAttribute">The parameter marker attribute.</param>
        /// <param name="constraintTypes">The constraint keyword to CLR type map.</param>
        private RouteBindingModel(INamedTypeSymbol routeAttribute, INamedTypeSymbol parameterAttribute, Dictionary<string, ITypeSymbol> constraintTypes)
        {
            _routeAttribute = routeAttribute;
            _parameterAttribute = parameterAttribute;
            _constraintTypes = constraintTypes;
        }

        /// <summary>Resolves the markers and constraint map, or <see langword="null"/> when a marker is absent.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The resolved model, or <see langword="null"/> to disable the rule.</returns>
        public static RouteBindingModel? Resolve(Compilation compilation)
        {
            var routeAttribute = compilation.GetTypeByMetadataName(RouteAttributeMetadataName);
            var parameterAttribute = compilation.GetTypeByMetadataName(ParameterAttributeMetadataName);
            if (routeAttribute is null || parameterAttribute is null)
            {
                return null;
            }

            var constraintTypes = new Dictionary<string, ITypeSymbol>(8, StringComparer.Ordinal);
            AddSpecial(constraintTypes, compilation, "int", SpecialType.System_Int32);
            AddSpecial(constraintTypes, compilation, "long", SpecialType.System_Int64);
            AddSpecial(constraintTypes, compilation, "bool", SpecialType.System_Boolean);
            AddSpecial(constraintTypes, compilation, "datetime", SpecialType.System_DateTime);
            AddSpecial(constraintTypes, compilation, "decimal", SpecialType.System_Decimal);
            AddSpecial(constraintTypes, compilation, "double", SpecialType.System_Double);
            AddSpecial(constraintTypes, compilation, "float", SpecialType.System_Single);
            AddResolved(constraintTypes, compilation, "guid", "System.Guid");

            return new RouteBindingModel(routeAttribute, parameterAttribute, constraintTypes);
        }

        /// <summary>Returns whether an attribute class is the route marker.</summary>
        /// <param name="attributeClass">The bound attribute class.</param>
        /// <returns><see langword="true"/> for the route attribute.</returns>
        public bool IsRoute(INamedTypeSymbol? attributeClass)
            => SymbolEqualityComparer.Default.Equals(attributeClass, _routeAttribute);

        /// <summary>Returns whether a property carries the parameter marker attribute.</summary>
        /// <param name="property">The property to inspect.</param>
        /// <returns><see langword="true"/> when the marker is present.</returns>
        public bool HasParameter(IPropertySymbol property)
        {
            var attributes = property.GetAttributes();
            for (var i = 0; i < attributes.Length; i++)
            {
                if (SymbolEqualityComparer.Default.Equals(attributes[i].AttributeClass, _parameterAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Looks up the CLR type a route constraint keyword maps to.</summary>
        /// <param name="constraint">The lowercased constraint keyword.</param>
        /// <param name="constraintType">The mapped CLR type, when the keyword is a typed constraint we resolve.</param>
        /// <returns><see langword="true"/> when the keyword maps to a resolved CLR type.</returns>
        public bool TryGetConstraintType(string constraint, [NotNullWhen(true)] out ITypeSymbol? constraintType)
            => _constraintTypes.TryGetValue(constraint, out constraintType);

        /// <summary>Adds a constraint keyword bound to a special type.</summary>
        /// <param name="map">The constraint map.</param>
        /// <param name="compilation">The compilation to resolve against.</param>
        /// <param name="constraint">The constraint keyword.</param>
        /// <param name="specialType">The special type it maps to.</param>
        private static void AddSpecial(Dictionary<string, ITypeSymbol> map, Compilation compilation, string constraint, SpecialType specialType)
            => map[constraint] = compilation.GetSpecialType(specialType);

        /// <summary>Adds a constraint keyword bound to a metadata-named type when it resolves.</summary>
        /// <param name="map">The constraint map.</param>
        /// <param name="compilation">The compilation to resolve against.</param>
        /// <param name="constraint">The constraint keyword.</param>
        /// <param name="metadataName">The metadata name it maps to.</param>
        private static void AddResolved(Dictionary<string, ITypeSymbol> map, Compilation compilation, string constraint, string metadataName)
        {
            var resolved = compilation.GetTypeByMetadataName(metadataName);
            if (resolved is null)
            {
                return;
            }

            map[constraint] = resolved;
        }
    }
}
