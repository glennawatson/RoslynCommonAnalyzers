// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a method marked with a serialization callback attribute whose signature does not match the shape
/// the serializer invokes (SST2430), so the callback silently never runs.
/// </summary>
/// <remarks>
/// The four attributes (<c>OnSerializing</c>, <c>OnSerialized</c>, <c>OnDeserializing</c>,
/// <c>OnDeserialized</c>) and <c>StreamingContext</c> are resolved on the first candidate and cached for the
/// compilation, including missing types. Methods without candidate attribute syntax do not resolve them.
/// The shape the serializer requires is a non-generic instance method returning <c>void</c> with a
/// single <c>StreamingContext</c> parameter — anything else is skipped at runtime.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2430SerializationCallbackSignatureAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The number of parameters a serialization callback must declare.</summary>
    private const int CallbackParameterCount = 1;

    /// <summary>The metadata name of the streaming-context parameter every callback must take.</summary>
    private const string StreamingContextMetadataName = "System.Runtime.Serialization.StreamingContext";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.SerializationCallbackSignature);

    /// <summary>The metadata names of the four serialization callback attributes.</summary>
    private static readonly string[] CallbackAttributeMetadataNames =
    [
        "System.Runtime.Serialization.OnSerializingAttribute",
        "System.Runtime.Serialization.OnSerializedAttribute",
        "System.Runtime.Serialization.OnDeserializingAttribute",
        "System.Runtime.Serialization.OnDeserializedAttribute",
    ];

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

    /// <summary>Registers method analysis with callback types that resolve only when needed.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var types = new LazyCompilationValue<CallbackFacts?>(context.Compilation, ResolveCallbackFacts);
        context.RegisterSymbolAction(symbolContext => AnalyzeMethod(symbolContext, types), SymbolKind.Method);
    }

    /// <summary>Reports a serialization callback whose signature stops it from ever running.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="types">The callback types resolved only after a candidate attribute is found.</param>
    private static void AnalyzeMethod(in SymbolAnalysisContext context, LazyCompilationValue<CallbackFacts?> types)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!MayHaveCallbackAttribute(method, context.CancellationToken))
        {
            return;
        }

        var attributes = method.GetAttributes();
        if (attributes.IsEmpty
            || types.Get() is not { } facts
            || !CarriesCallbackAttribute(attributes, facts.Attributes))
        {
            return;
        }

        if (HasCallbackShape(method, facts.StreamingContext)
            || method.Locations.IsEmpty
            || !method.Locations[0].IsInSource)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(CorrectnessRules.SerializationCallbackSignature, method.Locations[0], method.Name));
    }

    /// <summary>Rejects declarations without candidate attributes while preserving partial and synthesized methods.</summary>
    /// <param name="method">The method whose declaration syntax is inspected.</param>
    /// <param name="cancellationToken">A token that cancels syntax retrieval.</param>
    /// <returns>Whether the method may carry a callback attribute.</returns>
    private static bool MayHaveCallbackAttribute(IMethodSymbol method, CancellationToken cancellationToken)
    {
        // Attributes can be supplied by the other partial declaration or transferred to a synthesized method.
        if (method.PartialDefinitionPart is not null || method.PartialImplementationPart is not null)
        {
            return true;
        }

        var references = method.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            if (references[i].GetSyntax(cancellationToken) is not BaseMethodDeclarationSyntax declaration
                || HasCandidateAttributeName(declaration.AttributeLists))
            {
                return true;
            }
        }

        return references.IsEmpty;
    }

    /// <summary>Matches callback names while allowing bare names that may be using aliases.</summary>
    /// <param name="lists">The declaration's attribute lists.</param>
    /// <returns>Whether an attribute could bind to a serialization callback.</returns>
    private static bool HasCandidateAttributeName(SyntaxList<AttributeListSyntax> lists)
    {
        for (var i = 0; i < lists.Count; i++)
        {
            var attributes = lists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var name = attributes[j].Name switch
                {
                    QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                    AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
                    _ => null,
                };
                if (IsCallbackName(name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Accepts callback names and unknown names that require semantic confirmation.</summary>
    /// <param name="name">The qualified attribute's simple name, or null for a possible alias.</param>
    /// <returns>Whether the name could identify a serialization callback.</returns>
    private static bool IsCallbackName(string? name) =>
        name is null or "OnSerializing" or "OnSerializingAttribute"
            or "OnSerialized" or "OnSerializedAttribute"
            or "OnDeserializing" or "OnDeserializingAttribute"
            or "OnDeserialized" or "OnDeserializedAttribute";

    /// <summary>Returns whether a method carries one of the serialization callback attributes.</summary>
    /// <param name="attributes">The method's attributes.</param>
    /// <param name="callbackAttributes">The resolved callback attribute types.</param>
    /// <returns><see langword="true"/> when at least one attribute is a serialization callback.</returns>
    private static bool CarriesCallbackAttribute(ImmutableArray<AttributeData> attributes, INamedTypeSymbol[] callbackAttributes)
    {
        for (var i = 0; i < attributes.Length; i++)
        {
            if (attributes[i].AttributeClass is { } attributeClass && TypeRelations.IsOneOf(attributeClass, callbackAttributes))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a method has the shape the serializer invokes.</summary>
    /// <param name="method">The candidate callback method.</param>
    /// <param name="streamingContext">The resolved streaming-context type.</param>
    /// <returns><see langword="true"/> for a non-generic instance <c>void</c> method taking one <c>StreamingContext</c>.</returns>
    private static bool HasCallbackShape(IMethodSymbol method, INamedTypeSymbol streamingContext) =>
        !method.IsStatic
            && !method.IsGenericMethod
            && method.ReturnsVoid
            && method.Parameters.Length == CallbackParameterCount
            && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, streamingContext);

    /// <summary>Resolves the required parameter type and available callback attributes.</summary>
    /// <param name="compilation">The compilation supplying the types.</param>
    /// <returns>The callback facts, or null when callbacks cannot be checked.</returns>
    private static CallbackFacts? ResolveCallbackFacts(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(StreamingContextMetadataName) is not { } streamingContext)
        {
            return null;
        }

        var attributes = MetadataTypeLookup.ResolveAll(compilation, CallbackAttributeMetadataNames);
        return attributes.Length == 0 ? null : new CallbackFacts(streamingContext, attributes);
    }

    /// <summary>The resolved serialization callback facts for one compilation.</summary>
    /// <param name="StreamingContext">The streaming-context type a callback must take.</param>
    /// <param name="Attributes">The serialization callback attributes present in the compilation.</param>
    private readonly record struct CallbackFacts(INamedTypeSymbol StreamingContext, INamedTypeSymbol[] Attributes);
}
