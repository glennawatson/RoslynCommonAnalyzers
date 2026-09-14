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

    /// <summary>The public-required test markers followed by the TUnit marker.</summary>
    private static readonly string[] TestMarkerMetadataNames = TestAttributeNames.CreateMarkerMetadataNames();

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
    private static bool IsUniversallyRunnable(IMethodSymbol method, LazyCompilationValue<INamedTypeSymbol?[]> returnTypes) =>
        method.DeclaredAccessibility == Accessibility.Public
            && (!method.IsGenericMethod || !method.Parameters.IsEmpty)
            && (method.ReturnsVoid || method.ReturnType.TypeKind == TypeKind.Error || IsAwaitedReturn(method.ReturnType, returnTypes));

    /// <summary>Binds the method's attributes to determine whether it is a real test and whether its framework requires a public method.</summary>
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
                var attribute = attributes[j];
                if (!CouldBeTestAttribute(attribute, declaredAttributes, markers))
                {
                    continue;
                }

                if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType: { } attributeClass })
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

    /// <summary>Uses the method's attribute data to exclude known unrelated attributes before binding their syntax.</summary>
    /// <param name="attribute">The attribute syntax being classified.</param>
    /// <param name="declaredAttributes">The method's resolved attributes, including any from another partial declaration.</param>
    /// <param name="markers">The test markers resolved one slot at a time per compilation.</param>
    /// <returns>Whether the attribute is a possible test marker or still needs binding to determine its type.</returns>
    private static bool CouldBeTestAttribute(
        AttributeSyntax attribute,
        ImmutableArray<AttributeData> declaredAttributes,
        LazyMetadataTypeSlots markers)
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
                || IsPublicRequiredMarker(markers, attributeClass)
                || IsTUnitMarker(markers, attributeClass);
        }

        // Return attributes and invalid targets may be absent from the method's attribute data.
        return true;
    }

    /// <summary>Describes the first shape violation on a confirmed test method, or <see langword="null"/> when it is runnable.</summary>
    /// <param name="method">The test method symbol.</param>
    /// <param name="requiresPublic">Whether the matched framework discovers only public test methods.</param>
    /// <param name="returnTypes">The awaited return types resolved independently of test markers.</param>
    /// <returns>A clause describing the violation, or <see langword="null"/> when the shape is runnable.</returns>
    private static string? DescribeViolation(IMethodSymbol method, bool requiresPublic, LazyCompilationValue<INamedTypeSymbol?[]> returnTypes)
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
            ? $"returns '{returnType.ToDisplayString()}', which is not void, Task, or ValueTask"
            : null;
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
