// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a method that carries a test attribute but whose signature the runner cannot execute (SST2509): it is
/// non-public, a generic method with no parameters, or returns a type other than <c>void</c>, <c>Task</c>,
/// <c>ValueTask</c>, <c>Task&lt;T&gt;</c>, or <c>ValueTask&lt;T&gt;</c> without NUnit expected-result data.
/// </summary>
/// <remarks>
/// <para>
/// The rule reuses the plain test-method attribute markers — xUnit <c>[Fact]</c>/<c>[Theory]</c>, NUnit
/// <c>[Test]</c>/<c>[TestCase]</c>/<c>[TestCaseSource]</c>/<c>[Theory]</c>, MSTest <c>[TestMethod]</c>/<c>[DataTestMethod]</c>,
/// and TUnit <c>[Test]</c>. The public requirement is applied only for xUnit, NUnit, and MSTest, which do not discover
/// non-public methods; TUnit is excluded from the public check because it is not a universal requirement there. The
/// generic requirement applies to every framework. NUnit cases can return values when their attributes declare expected
/// results or a test-case source can supply them. Static methods are allowed.
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
    /// <summary>The literal parameter-data attribute following the framework marker slots.</summary>
    private const int NUnitValuesIndex = TestAttributeNames.TUnitMarkerIndex + 1;

    /// <summary>The method declaration registration shared by every compilation.</summary>
    private static readonly SyntaxKind[] MethodKinds = [SyntaxKind.MethodDeclaration];

    /// <summary>The public-required test markers followed by the TUnit marker.</summary>
    private static readonly string[] TestMarkerMetadataNames = [.. TestAttributeNames.CreateMarkerMetadataNames(), "NUnit.Framework.ValuesAttribute"];

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
            var markers = new LazyMetadataTypeSlots(compilation, TestMarkerMetadataNames);
            var returnTypes = new LazyCompilationValue<INamedTypeSymbol?[]>(compilation, ResolveReturnTypes, runOnce: true);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMethod(nodeContext, markers, returnTypes), MethodKinds);
        });
    }

    /// <summary>Analyzes one method declaration for a test-method shape the runner cannot execute.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="markers">The test markers resolved one slot at a time per compilation.</param>
    /// <param name="returnTypes">The awaited return types resolved independently of test markers.</param>
    private static void AnalyzeMethod(in SyntaxNodeAnalysisContext context, LazyMetadataTypeSlots markers, LazyCompilationValue<INamedTypeSymbol?[]> returnTypes)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!SyntaxNames.AnyAttributeNamed(method.AttributeLists, TestAttributeNames.IsTestAttributeName) || IsSyntacticallyRunnableShape(method))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not { } methodSymbol
            || IsUniversallyRunnable(methodSymbol, returnTypes))
        {
            return;
        }

        if (ClassifyTestAttributes(context, method.AttributeLists, methodSymbol, markers) is not { } requiresPublic)
        {
            return;
        }

        var reason = DescribeViolation(methodSymbol, requiresPublic, returnTypes, markers);
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
    private static bool IsUniversallyRunnable(IMethodSymbol method, LazyCompilationValue<INamedTypeSymbol?[]> returnTypes) =>
        method.DeclaredAccessibility == Accessibility.Public
            && (!method.IsGenericMethod || !method.Parameters.IsEmpty)
            && (method.ReturnsVoid || method.ReturnType.TypeKind == TypeKind.Error || IsAwaitedReturn(method.ReturnType, returnTypes));

    /// <summary>Classifies the method's bound attributes and the framework's accessibility requirement.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="attributeLists">The method's attribute lists.</param>
    /// <param name="methodSymbol">The declared method whose attribute data can exclude unrelated attributes.</param>
    /// <param name="markers">The test markers resolved one slot at a time per compilation.</param>
    /// <returns>Whether the matched framework discovers only public methods, or <see langword="null"/> when no real test attribute is present.</returns>
    private static bool? ClassifyTestAttributes(
        in SyntaxNodeAnalysisContext context,
        SyntaxList<AttributeListSyntax> attributeLists,
        IMethodSymbol methodSymbol,
        LazyMetadataTypeSlots markers)
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
                if (GetAttributeClass(context, attributes[j], declaredAttributes) is not { } attributeClass)
                {
                    continue;
                }

                if (IsPublicRequiredMarker(markers, attributeClass))
                {
                    isTest = true;
                    requiresPublic = true;
                }
                else if (IsTUnitMarker(markers, attributeClass))
                {
                    isTest = true;
                }
            }
        }

        return isTest ? requiresPublic : null;
    }

    /// <summary>Reuses bound constructors, binding syntax only when it has no matching method attribute data.</summary>
    /// <param name="context">The syntax context used for return attributes and other unmatched syntax.</param>
    /// <param name="attribute">The attribute syntax being classified.</param>
    /// <param name="declaredAttributes">The method's resolved attributes, including any from another partial declaration.</param>
    /// <returns>The resolved constructor's class, or null when constructor binding failed.</returns>
    private static INamedTypeSymbol? GetAttributeClass(
        in SyntaxNodeAnalysisContext context,
        AttributeSyntax attribute,
        ImmutableArray<AttributeData> declaredAttributes)
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

            return declaredAttribute.AttributeConstructor?.ContainingType;
        }

        // Return attributes and invalid targets may be absent from the method's attribute data.
        return context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is IMethodSymbol constructor
            ? constructor.ContainingType
            : null;
    }

    /// <summary>Describes the first shape violation on a confirmed test method, or <see langword="null"/> when it is runnable.</summary>
    /// <param name="method">The test method symbol.</param>
    /// <param name="requiresPublic">Whether the matched framework discovers only public test methods.</param>
    /// <param name="returnTypes">The awaited return types resolved independently of test markers.</param>
    /// <param name="markers">The test markers resolved one slot at a time per compilation.</param>
    /// <returns>A clause describing the violation, or <see langword="null"/> when the shape is runnable.</returns>
    private static string? DescribeViolation(
        IMethodSymbol method,
        bool requiresPublic,
        LazyCompilationValue<INamedTypeSymbol?[]> returnTypes,
        LazyMetadataTypeSlots markers)
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
        return !method.ReturnsVoid && returnType.TypeKind != TypeKind.Error && !IsAwaitedReturn(returnType, returnTypes)
            && !AllowsNUnitValueReturn(method, markers)
            ? $"returns '{returnType.ToDisplayString()}', which is not void, Task, or ValueTask"
            : null;
    }

    /// <summary>Accepts NUnit result-bearing cases while preserving requirements from every other test marker.</summary>
    /// <param name="method">The confirmed test method with an otherwise unsupported return type.</param>
    /// <param name="markers">The lazily resolved framework marker slots.</param>
    /// <returns>Whether NUnit can consume the returned value and no known case or other framework rejects it.</returns>
    private static bool AllowsNUnitValueReturn(IMethodSymbol method, LazyMetadataTypeSlots markers)
    {
        var allowsValueReturn = false;
        var isTheory = false;
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            var contract = GetAttributeReturnContract(attribute, attributeClass, !method.Parameters.IsEmpty, markers);
            if (contract == false)
            {
                return false;
            }

            if (contract == true)
            {
                allowsValueReturn = true;
            }
            else if (markers.IsOrDerivesFromAny(attributeClass, TestAttributeNames.NUnitTheoryMarkerIndex, TestAttributeNames.NUnitTheoryMarkerIndex + 1))
            {
                isTheory = true;
            }
        }

        return allowsValueReturn && !HasKnownGeneratedCases(method.Parameters, markers, isTheory);
    }

    /// <summary>Classifies whether a single test attribute permits or rejects an ordinary return value.</summary>
    /// <param name="attribute">The bound test attribute.</param>
    /// <param name="attributeClass">The attribute's resolved class.</param>
    /// <param name="hasParameters">Whether the test declares method parameters.</param>
    /// <param name="markers">The lazily resolved framework marker slots.</param>
    /// <returns>True for result-bearing cases, false for a conflicting contract, or null when the attribute supplies neither.</returns>
    private static bool? GetAttributeReturnContract(AttributeData attribute, INamedTypeSymbol attributeClass, bool hasParameters, LazyMetadataTypeSlots markers)
    {
        if (markers.IsOrDerivesFromAny(attributeClass, 0, TestAttributeNames.NUnitMarkerStart)
            || markers.IsOrDerivesFromAny(attributeClass, TestAttributeNames.NUnitMarkerEnd, TestAttributeNames.TUnitMarkerIndex + 1))
        {
            return false;
        }

        if (markers.IsOrDerivesFromAny(attributeClass, TestAttributeNames.NUnitTestCaseMarkerIndex, TestAttributeNames.NUnitTestCaseMarkerIndex + 1))
        {
            return HasExpectedResult(attribute);
        }

        if (markers.IsOrDerivesFromAny(attributeClass, TestAttributeNames.NUnitTestCaseSourceMarkerIndex, TestAttributeNames.NUnitTestCaseSourceMarkerIndex + 1))
        {
            return true;
        }

        return markers.IsOrDerivesFromAny(attributeClass, TestAttributeNames.NUnitMarkerStart, TestAttributeNames.NUnitMarkerStart + 1)
            && HasExpectedResult(attribute)
            ? !hasParameters
            : null;
    }

    /// <summary>Detects parameter data guaranteed to generate additional cases without expected results.</summary>
    /// <param name="parameters">The test's parameters.</param>
    /// <param name="markers">The lazily resolved framework marker slots.</param>
    /// <param name="isTheory">Whether NUnit supplies automatic boolean and enum theory data.</param>
    /// <returns>Whether every parameter has known nonempty data.</returns>
    private static bool HasKnownGeneratedCases(ImmutableArray<IParameterSymbol> parameters, LazyMetadataTypeSlots markers, bool isTheory)
    {
        if (parameters.IsEmpty)
        {
            return false;
        }

        foreach (var parameter in parameters)
        {
            if (isTheory && HasAutomaticTypeValues(parameter.Type))
            {
                continue;
            }

            if (!HasKnownValues(parameter, markers))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Recognizes literal NUnit values while leaving runtime and custom data providers unevaluated.</summary>
    /// <param name="parameter">The parameter whose data attributes are inspected.</param>
    /// <param name="markers">The lazily resolved framework marker slots.</param>
    /// <returns>Whether NUnit's own values attribute supplies at least one value.</returns>
    private static bool HasKnownValues(IParameterSymbol parameter, LazyMetadataTypeSlots markers)
    {
        foreach (var attribute in parameter.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass || !markers.IsAny(attributeClass, NUnitValuesIndex, NUnitValuesIndex + 1))
            {
                continue;
            }

            return HasValuesArguments(attribute, parameter.Type);
        }

        return false;
    }

    /// <summary>Determines whether NUnit's values constructor supplies literal or automatic parameter data.</summary>
    /// <param name="attribute">The bound NUnit values attribute.</param>
    /// <param name="parameterType">The parameter type used for automatic data.</param>
    /// <returns>Whether the attribute supplies at least one known value.</returns>
    private static bool HasValuesArguments(AttributeData attribute, ITypeSymbol parameterType)
    {
        if (attribute.AttributeConstructor is not { } constructor)
        {
            return false;
        }

        var arguments = attribute.ConstructorArguments;
        if (arguments.IsEmpty)
        {
            return HasAutomaticTypeValues(parameterType);
        }

        var first = arguments[0];
        if (first.Kind == TypedConstantKind.Error)
        {
            return false;
        }

        if (arguments.Length != 1 || !constructor.Parameters[0].IsParams)
        {
            return true;
        }

        return first.IsNull || !first.Values.IsEmpty || HasAutomaticTypeValues(parameterType);
    }

    /// <summary>Recognizes boolean values and declared enum constants, including their nullable forms.</summary>
    /// <param name="type">The parameter type used by NUnit's automatic data provider.</param>
    /// <returns>Whether the type supplies automatic values without inspecting runtime data sources.</returns>
    private static bool HasAutomaticTypeValues(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        if (type.SpecialType == SpecialType.System_Boolean)
        {
            return true;
        }

        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration)
        {
            return false;
        }

        foreach (var member in enumeration.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Detects an explicitly assigned expected result, including null, false, and zero.</summary>
    /// <param name="attribute">A bound NUnit test or test-case attribute.</param>
    /// <returns>Whether the attribute names the expected-result property.</returns>
    private static bool HasExpectedResult(AttributeData attribute)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == "ExpectedResult")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an attribute derives from a marker whose framework requires public methods.</summary>
    /// <param name="markers">The test markers resolved one slot at a time per compilation.</param>
    /// <param name="attributeClass">The attribute's type.</param>
    /// <returns>Whether the attribute marks an xUnit, NUnit, or MSTest test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPublicRequiredMarker(LazyMetadataTypeSlots markers, INamedTypeSymbol attributeClass) =>
        markers.IsOrDerivesFromAny(attributeClass, 0, TestAttributeNames.TUnitMarkerIndex);

    /// <summary>Returns whether an attribute derives from the TUnit test marker.</summary>
    /// <param name="markers">The test markers resolved one slot at a time per compilation.</param>
    /// <param name="attributeClass">The attribute's type.</param>
    /// <returns>Whether the attribute marks a TUnit test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTUnitMarker(LazyMetadataTypeSlots markers, INamedTypeSymbol attributeClass) =>
        markers.IsOrDerivesFromAny(attributeClass, TestAttributeNames.TUnitMarkerIndex, TestAttributeNames.TUnitMarkerIndex + 1);

    /// <summary>Returns whether the method's shape is syntactically already runnable, so binding can be skipped.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns>
    /// <see langword="true"/> when the method is written with a <c>public</c> modifier and a <c>void</c> return,
    /// and is either non-generic or declares parameters — shapes every framework accepts without further checking.
    /// </returns>
    private static bool IsSyntacticallyRunnableShape(MethodDeclarationSyntax method) =>
        (method.TypeParameterList is null || method.ParameterList.Parameters.Count != 0)
            && method.Modifiers.Any(SyntaxKind.PublicKeyword)
            && method.ReturnType is PredefinedTypeSyntax predefined
            && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

    /// <summary>Determines whether a return type is one the test runner awaits, resolving the definitions only after a matching type name.</summary>
    /// <param name="type">The method return type.</param>
    /// <param name="returnTypes">The awaited return definitions for the compilation.</param>
    /// <returns>Whether the return is Task, ValueTask, or a generic form of either.</returns>
    private static bool IsAwaitedReturn(ITypeSymbol type, LazyCompilationValue<INamedTypeSymbol?[]> returnTypes)
    {
        if (type.Name is not ("Task" or "ValueTask"))
        {
            return false;
        }

        var types = returnTypes.Get();
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

    /// <summary>Resolves all awaited return definitions together.</summary>
    /// <param name="compilation">The compilation whose return types are resolved.</param>
    /// <returns>The return-type definitions, with a null slot for each type the compilation lacks.</returns>
    private static INamedTypeSymbol?[] ResolveReturnTypes(Compilation compilation) =>
    [
        compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
        compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
        compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask"),
        compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1"),
    ];
}
