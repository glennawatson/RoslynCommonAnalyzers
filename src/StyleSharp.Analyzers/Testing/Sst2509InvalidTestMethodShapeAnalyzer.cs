// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a method that carries a test attribute but whose signature the runner cannot execute (SST2509): it is
/// non-public, a generic method with no parameters, or returns a type other than <c>void</c>, <c>Task</c>,
/// <c>ValueTask</c>, <c>Task&lt;T&gt;</c>, or <c>ValueTask&lt;T&gt;</c>. Such a method is discovered as a test and then
/// silently skipped, so it appears to pass while never running.
/// </summary>
/// <remarks>
/// <para>
/// The rule reuses the plain test-method attribute markers — xUnit <c>[Fact]</c>/<c>[Theory]</c>, NUnit
/// <c>[Test]</c>/<c>[TestCase]</c>/<c>[TestCaseSource]</c>/<c>[Theory]</c>, MSTest <c>[TestMethod]</c>/<c>[DataTestMethod]</c>,
/// and TUnit <c>[Test]</c>. The public requirement is applied only for xUnit, NUnit, and MSTest, which do not discover
/// non-public methods; TUnit is excluded from the public check because it is not a universal requirement there. The
/// generic and return-type requirements are universal and apply to every framework. A static method is never reported —
/// some frameworks run static test methods.
/// </para>
/// <para>
/// The clean path is a syntactic prepass: a method must carry an attribute written with a known test-attribute
/// name and must not already have a runnable shape (public, non-generic, returning <c>void</c>) before any framework
/// symbols resolve. Only a method that looks like a test with a suspect shape is bound — to confirm a real test
/// attribute is present and to classify the exact violation from the method symbol.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2509InvalidTestMethodShapeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The method declaration registration shared by every compilation.</summary>
    private static readonly SyntaxKind[] MethodKinds = [SyntaxKind.MethodDeclaration];

    /// <summary>The simple names, with and without the suffix, that a test-marking attribute is written as.</summary>
    private static readonly HashSet<string> TestAttributeSimpleNames = new(StringComparer.Ordinal)
    {
        "Fact", "FactAttribute",
        "Theory", "TheoryAttribute",
        "Test", "TestAttribute",
        "TestCase", "TestCaseAttribute",
        "TestCaseSource", "TestCaseSourceAttribute",
        "TestMethod", "TestMethodAttribute",
        "DataTestMethod", "DataTestMethodAttribute",
    };

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(TestingRules.InvalidTestMethodShape);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var compilation = start.Compilation;
            var symbols = new FrameworkSymbols(compilation);
            var returnTypes = new ReturnTypeCache(compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMethod(nodeContext, symbols, returnTypes), MethodKinds);
        });
    }

    /// <summary>Analyzes one method declaration for a test-method shape the runner cannot execute.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="frameworkSymbols">The framework types resolved on first demand per compilation.</param>
    /// <param name="returnTypes">The awaited return types resolved independently of test markers.</param>
    private static void AnalyzeMethod(in SyntaxNodeAnalysisContext context, FrameworkSymbols frameworkSymbols, ReturnTypeCache returnTypes)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!HasTestAttributeName(method.AttributeLists) || IsSyntacticallyRunnableShape(method))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not { } methodSymbol
            || IsUniversallyRunnable(methodSymbol, returnTypes))
        {
            return;
        }

        var (isTest, requiresPublic) = ClassifyTestAttributes(context, method.AttributeLists, methodSymbol, frameworkSymbols);
        if (!isTest)
        {
            return;
        }

        var reason = DescribeViolation(methodSymbol, requiresPublic, returnTypes);
        if (reason is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            TestingRules.InvalidTestMethodShape,
            method.Identifier.GetLocation(),
            method.Identifier.ValueText,
            reason));
    }

    /// <summary>Excludes bound method shapes that cannot violate any framework's requirements.</summary>
    /// <param name="method">The candidate method.</param>
    /// <param name="returnTypes">The return definitions cached independently of framework markers.</param>
    /// <returns>Whether no test framework can report this method's shape.</returns>
    private static bool IsUniversallyRunnable(IMethodSymbol method, ReturnTypeCache returnTypes) =>
        method.DeclaredAccessibility == Accessibility.Public
            && (!method.IsGenericMethod || !method.Parameters.IsEmpty)
            && (method.ReturnsVoid || method.ReturnType.TypeKind == TypeKind.Error || returnTypes.Contains(method.ReturnType));

    /// <summary>Binds the method's attributes to determine whether it is a real test and whether its framework requires a public method.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="attributeLists">The method's attribute lists.</param>
    /// <param name="methodSymbol">The declared method whose attribute data can exclude unrelated attributes.</param>
    /// <param name="symbols">The resolved test-framework symbols.</param>
    /// <returns>Whether a real test attribute is present and whether the matched framework discovers only public methods.</returns>
    private static (bool IsTest, bool RequiresPublic) ClassifyTestAttributes(
        in SyntaxNodeAnalysisContext context,
        SyntaxList<AttributeListSyntax> attributeLists,
        IMethodSymbol methodSymbol,
        FrameworkSymbols symbols)
    {
        var isTest = false;
        var requiresPublic = false;
        var declaredAttributes = methodSymbol.GetAttributes();
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var attribute = attributes[j];
                if (!CouldBeTestAttribute(attribute, declaredAttributes, symbols))
                {
                    continue;
                }

                if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType: { } attributeClass })
                {
                    continue;
                }

                if (symbols.IsPublicRequiredMarker(attributeClass))
                {
                    isTest = true;
                    requiresPublic = true;
                }
                else if (symbols.IsTUnitMarker(attributeClass))
                {
                    isTest = true;
                }
            }
        }

        return (isTest, requiresPublic);
    }

    /// <summary>Uses the method's attribute data to exclude known unrelated attributes before binding their syntax.</summary>
    /// <param name="attribute">The attribute syntax being classified.</param>
    /// <param name="declaredAttributes">The method's resolved attributes, including any from another partial declaration.</param>
    /// <param name="symbols">The resolved test-framework symbols.</param>
    /// <returns>Whether the attribute is a possible test marker or still needs binding to determine its type.</returns>
    private static bool CouldBeTestAttribute(
        AttributeSyntax attribute,
        ImmutableArray<AttributeData> declaredAttributes,
        FrameworkSymbols symbols)
    {
        for (var i = 0; i < declaredAttributes.Length; i++)
        {
            var declaredAttribute = declaredAttributes[i];
            if (declaredAttribute.ApplicationSyntaxReference is not { } reference
                || reference.SyntaxTree != attribute.SyntaxTree
                || reference.Span != attribute.Span)
            {
                continue;
            }

            return declaredAttribute.AttributeClass is not { } attributeClass
                || symbols.IsPublicRequiredMarker(attributeClass)
                || symbols.IsTUnitMarker(attributeClass);
        }

        // Return attributes and invalid targets may be absent from the method's attribute data.
        return true;
    }

    /// <summary>Describes the first shape violation on a confirmed test method, or <see langword="null"/> when it is runnable.</summary>
    /// <param name="method">The test method symbol.</param>
    /// <param name="requiresPublic">Whether the matched framework discovers only public test methods.</param>
    /// <param name="returnTypes">The awaited return types resolved independently of test markers.</param>
    /// <returns>A clause describing the violation, or <see langword="null"/> when the shape is runnable.</returns>
    private static string? DescribeViolation(IMethodSymbol method, bool requiresPublic, ReturnTypeCache returnTypes)
    {
        if (requiresPublic && method.DeclaredAccessibility != Accessibility.Public)
        {
            return "is not public";
        }

        if (method.IsGenericMethod && method.Parameters.IsEmpty)
        {
            return "is a generic method with no parameters, so its type argument cannot be inferred";
        }

        var returnType = method.ReturnType;
        return !method.ReturnsVoid && returnType.TypeKind != TypeKind.Error && !returnTypes.Contains(returnType)
            ? $"returns '{returnType.ToDisplayString()}', which is not void, Task, or ValueTask"
            : null;
    }

    /// <summary>Returns whether the method's shape is syntactically already runnable, so binding can be skipped.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns>
    /// <see langword="true"/> when the method is written with a <c>public</c> modifier, no type parameters, and a
    /// <c>void</c> return — the shape every framework runs, which needs no further checking.
    /// </returns>
    private static bool IsSyntacticallyRunnableShape(MethodDeclarationSyntax method) =>
        method.TypeParameterList is null
            && method.Modifiers.Any(SyntaxKind.PublicKeyword)
            && method.ReturnType is PredefinedTypeSyntax predefined
            && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

    /// <summary>Returns whether any attribute on the method is written with a known test-attribute name.</summary>
    /// <param name="attributeLists">The method's attribute lists.</param>
    /// <returns><see langword="true"/> when a test-attribute name is present.</returns>
    private static bool HasTestAttributeName(SyntaxList<AttributeListSyntax> attributeLists)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (TestAttributeSimpleNames.Contains(GetSimpleName(attributes[j].Name)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased attribute name.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>Caches the awaitable return definitions only after a matching type name is encountered.</summary>
    /// <param name="compilation">The compilation whose return types are cached.</param>
    private sealed class ReturnTypeCache(Compilation compilation)
    {
        /// <summary>Serializes the first return-type resolution.</summary>
        private readonly object _gate = new();

        /// <summary>The immutable published return-type definitions.</summary>
        private INamedTypeSymbol?[]? _types;

        /// <summary>Determines whether a return type is one the test runner awaits.</summary>
        /// <param name="type">The method return type.</param>
        /// <returns>Whether the return is Task, ValueTask, or a generic form of either.</returns>
        public bool Contains(ITypeSymbol type)
        {
            if (type.Name is not ("Task" or "ValueTask"))
            {
                return false;
            }

            var types = GetTypes();
            var definition = type.OriginalDefinition;
            for (var i = 0; i < types.Length; i++)
            {
                if (SymbolEqualityComparer.Default.Equals(definition, types[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Reads the published definitions without entering the cold resolution gate.</summary>
        /// <returns>The immutable return-type definitions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private INamedTypeSymbol?[] GetTypes() => Volatile.Read(ref _types) ?? Resolve();

        /// <summary>Resolves and publishes all awaited return definitions together.</summary>
        /// <returns>The immutable return-type definitions.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private INamedTypeSymbol?[] Resolve()
        {
            lock (_gate)
            {
                var types = _types;
                if (types is null)
                {
                    types =
                    [
                        compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
                        compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
                        compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask"),
                        compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1"),
                    ];
                    Volatile.Write(ref _types, types);
                }

                return types;
            }
        }
    }

    /// <summary>Resolves only the test markers encountered in a candidate attribute's inheritance chain.</summary>
    /// <param name="compilation">The compilation whose marker identities are cached.</param>
    private sealed class FrameworkSymbols(Compilation compilation)
    {
        /// <summary>The slot reserved for the marker that does not require public methods.</summary>
        private const int TUnitMarkerIndex = 8;

        /// <summary>The public-required markers followed by the TUnit marker.</summary>
        private static readonly string[] MarkerMetadataNames =
        [
            "Xunit.FactAttribute",
            "Xunit.TheoryAttribute",
            "NUnit.Framework.TestAttribute",
            "NUnit.Framework.TestCaseAttribute",
            "NUnit.Framework.TestCaseSourceAttribute",
            "NUnit.Framework.TheoryAttribute",
            "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
            "Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute",
            "TUnit.Core.TestAttribute",
        ];

        /// <summary>Serializes the first lookup of each marker, including missing results.</summary>
        private readonly object _gate = new();

        /// <summary>The marker slots, allocated only after a candidate needs a metadata lookup.</summary>
        private INamedTypeSymbol?[]? _markers;

        /// <summary>Published bits distinguish unresolved slots from resolved missing types.</summary>
        private int _resolvedMarkers;

        /// <summary>Returns whether an attribute derives from a marker whose framework requires public methods.</summary>
        /// <param name="attributeClass">The attribute's type.</param>
        /// <returns>Whether the attribute marks an xUnit, NUnit, or MSTest test.</returns>
        public bool IsPublicRequiredMarker(INamedTypeSymbol attributeClass)
        {
            for (var type = attributeClass; type is not null; type = type.BaseType)
            {
                var definition = type.OriginalDefinition;
                for (var i = 0; i < TUnitMarkerIndex; i++)
                {
                    if (CouldMatchMarker(definition, MarkerMetadataNames[i])
                        && SymbolEqualityComparer.Default.Equals(definition, GetMarker(i)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Returns whether an attribute derives from the TUnit test marker.</summary>
        /// <param name="attributeClass">The attribute's type.</param>
        /// <returns>Whether the attribute marks a TUnit test.</returns>
        public bool IsTUnitMarker(INamedTypeSymbol attributeClass)
        {
            for (var type = attributeClass; type is not null; type = type.BaseType)
            {
                var definition = type.OriginalDefinition;
                if (CouldMatchMarker(definition, MarkerMetadataNames[TUnitMarkerIndex])
                    && SymbolEqualityComparer.Default.Equals(definition, GetMarker(TUnitMarkerIndex)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Excludes unrelated types without constructing a qualified name or probing metadata.</summary>
        /// <param name="type">The candidate attribute definition.</param>
        /// <param name="metadataName">The top-level marker's metadata name.</param>
        /// <returns>Whether the type's name and namespace can identify this marker.</returns>
        private static bool CouldMatchMarker(INamedTypeSymbol type, string metadataName)
        {
            var separator = metadataName.LastIndexOf('.');
            var name = type.Name;
            if (type.ContainingType is not null || type.Arity != 0
                || name.Length != metadataName.Length - separator - 1
                || string.Compare(metadataName, separator + 1, name, 0, name.Length, StringComparison.Ordinal) != 0)
            {
                return false;
            }

            var ns = type.ContainingNamespace;
            while (separator > 0 && !ns.IsGlobalNamespace)
            {
                var end = separator;
                separator = metadataName.LastIndexOf('.', end - 1);
                var part = ns.Name;
                if (part.Length != end - separator - 1
                    || string.Compare(metadataName, separator + 1, part, 0, part.Length, StringComparison.Ordinal) != 0)
                {
                    return false;
                }

                ns = ns.ContainingNamespace;
            }

            return separator < 0 && ns.IsGlobalNamespace;
        }

        /// <summary>Reads a published marker without entering the cold lookup gate.</summary>
        /// <param name="index">The marker slot.</param>
        /// <returns>The resolved marker, including a cached missing result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private INamedTypeSymbol? GetMarker(int index) =>
            (Volatile.Read(ref _resolvedMarkers) & (1 << index)) != 0 ? _markers![index] : Resolve(index);

        /// <summary>Resolves one needed marker once and publishes its completed slot.</summary>
        /// <param name="index">The marker slot.</param>
        /// <returns>The marker, or null when it is absent or ambiguous.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private INamedTypeSymbol? Resolve(int index)
        {
            lock (_gate)
            {
                var markers = _markers ??= new INamedTypeSymbol?[MarkerMetadataNames.Length];
                var bit = 1 << index;
                if ((_resolvedMarkers & bit) == 0)
                {
                    markers[index] = compilation.GetTypeByMetadataName(MarkerMetadataNames[index]);
                    Volatile.Write(ref _resolvedMarkers, _resolvedMarkers | bit);
                }

                return markers[index];
            }
        }
    }
}
