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
            var symbols = new Lazy<FrameworkSymbols?>(() => FrameworkSymbols.Resolve(compilation));
            var returnTypes = new ReturnTypeCache(compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMethod(nodeContext, symbols, returnTypes), MethodKinds);
        });
    }

    /// <summary>Analyzes one method declaration for a test-method shape the runner cannot execute.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="frameworkSymbols">The framework types resolved on first demand per compilation.</param>
    /// <param name="returnTypes">The awaited return types resolved independently of test markers.</param>
    private static void AnalyzeMethod(in SyntaxNodeAnalysisContext context, Lazy<FrameworkSymbols?> frameworkSymbols, ReturnTypeCache returnTypes)
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

        var symbols = frameworkSymbols.Value;
        if (symbols is null)
        {
            return;
        }

        var (isTest, requiresPublic) = ClassifyTestAttributes(context, method.AttributeLists, methodSymbol, symbols);
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

    /// <summary>The test-framework symbols resolved for a candidate method to classify attributes.</summary>
    private sealed class FrameworkSymbols
    {
        /// <summary>The metadata name of the TUnit test marker, which does not universally require a public method.</summary>
        private const string TUnitTestMarkerMetadataName = "TUnit.Core.TestAttribute";

        /// <summary>The metadata names of the attributes that mark a method as a test the framework discovers only when public.</summary>
        private static readonly string[] PublicRequiredMarkerMetadataNames =
        [
            "Xunit.FactAttribute",
            "Xunit.TheoryAttribute",
            "NUnit.Framework.TestAttribute",
            "NUnit.Framework.TestCaseAttribute",
            "NUnit.Framework.TestCaseSourceAttribute",
            "NUnit.Framework.TheoryAttribute",
            "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
            "Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute",
        ];

        /// <summary>The resolved markers whose framework discovers only public test methods; unresolved slots stay <see langword="null"/>.</summary>
        private readonly INamedTypeSymbol?[] _publicRequiredMarkers;

        /// <summary>The resolved TUnit test marker, or <see langword="null"/> when TUnit is not referenced.</summary>
        private readonly INamedTypeSymbol? _tunitMarker;

        /// <summary>Initializes a new instance of the <see cref="FrameworkSymbols"/> class.</summary>
        /// <param name="publicRequiredMarkers">The resolved public-required test markers.</param>
        /// <param name="tunitMarker">The resolved TUnit test marker, or <see langword="null"/>.</param>
        private FrameworkSymbols(
            INamedTypeSymbol?[] publicRequiredMarkers,
            INamedTypeSymbol? tunitMarker)
        {
            _publicRequiredMarkers = publicRequiredMarkers;
            _tunitMarker = tunitMarker;
        }

        /// <summary>Resolves the test-framework symbols, or <see langword="null"/> when no test framework is referenced.</summary>
        /// <param name="compilation">The analyzed compilation.</param>
        /// <returns>The resolved symbols, or <see langword="null"/> when no test-attribute marker resolves.</returns>
        public static FrameworkSymbols? Resolve(Compilation compilation)
        {
            var publicRequiredMarkers = new INamedTypeSymbol?[PublicRequiredMarkerMetadataNames.Length];
            var anyPublicRequired = false;
            for (var i = 0; i < PublicRequiredMarkerMetadataNames.Length; i++)
            {
                var marker = compilation.GetTypeByMetadataName(PublicRequiredMarkerMetadataNames[i]);
                publicRequiredMarkers[i] = marker;
                anyPublicRequired = anyPublicRequired || marker is not null;
            }

            var tunitMarker = compilation.GetTypeByMetadataName(TUnitTestMarkerMetadataName);
            return !anyPublicRequired && tunitMarker is null
                ? null
                : new FrameworkSymbols(publicRequiredMarkers, tunitMarker);
        }

        /// <summary>Returns whether an attribute type is or derives from a marker whose framework requires a public test method.</summary>
        /// <param name="attributeClass">The attribute's type.</param>
        /// <returns><see langword="true"/> when the type marks a method as an xUnit, NUnit, or MSTest test.</returns>
        public bool IsPublicRequiredMarker(INamedTypeSymbol attributeClass)
        {
            for (var type = attributeClass; type is not null; type = type.BaseType)
            {
                var definition = type.OriginalDefinition;
                for (var m = 0; m < _publicRequiredMarkers.Length; m++)
                {
                    if (SymbolEqualityComparer.Default.Equals(definition, _publicRequiredMarkers[m]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Returns whether an attribute type is or derives from the TUnit test marker.</summary>
        /// <param name="attributeClass">The attribute's type.</param>
        /// <returns><see langword="true"/> when the type marks a method as a TUnit test.</returns>
        public bool IsTUnitMarker(INamedTypeSymbol attributeClass)
        {
            for (var type = attributeClass; type is not null; type = type.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _tunitMarker))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
