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
/// <c>OnDeserialized</c>) and <c>StreamingContext</c> are resolved once at compilation start; a compilation
/// that has none of them registers nothing, so a target framework without the serialization attributes pays
/// nothing. The shape the serializer requires is a non-generic instance method returning <c>void</c> with a
/// single <c>StreamingContext</c> parameter — anything else is skipped at runtime.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2430SerializationCallbackSignatureAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the streaming-context parameter every callback must take.</summary>
    private const string StreamingContextMetadataName = "System.Runtime.Serialization.StreamingContext";

    /// <summary>The number of parameters a serialization callback must declare.</summary>
    private const int CallbackParameterCount = 1;

    /// <summary>The metadata names of the four serialization callback attributes.</summary>
    private static readonly string[] CallbackAttributeMetadataNames =
    [
        "System.Runtime.Serialization.OnSerializingAttribute",
        "System.Runtime.Serialization.OnSerializedAttribute",
        "System.Runtime.Serialization.OnDeserializingAttribute",
        "System.Runtime.Serialization.OnDeserializedAttribute",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.SerializationCallbackSignature);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the rule only when the compilation actually has serialization callbacks to check.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(StreamingContextMetadataName) is not { } streamingContext)
        {
            return;
        }

        var attributes = ResolveCallbackAttributes(context.Compilation);
        if (attributes.Length == 0)
        {
            return;
        }

        var facts = new CallbackFacts(streamingContext, attributes);
        context.RegisterSymbolAction(symbolContext => AnalyzeMethod(symbolContext, facts), SymbolKind.Method);
    }

    /// <summary>Resolves the serialization callback attributes present in the compilation.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns>The resolved attribute types; empty when none are present.</returns>
    private static ImmutableArray<INamedTypeSymbol> ResolveCallbackAttributes(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(CallbackAttributeMetadataNames.Length);
        for (var i = 0; i < CallbackAttributeMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(CallbackAttributeMetadataNames[i]) is { } attribute)
            {
                builder.Add(attribute);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Reports a serialization callback whose signature stops it from ever running.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="facts">The resolved callback attributes and streaming-context type.</param>
    private static void AnalyzeMethod(SymbolAnalysisContext context, CallbackFacts facts)
    {
        var method = (IMethodSymbol)context.Symbol;
        var attributes = method.GetAttributes();
        if (attributes.Length == 0 || !CarriesCallbackAttribute(attributes, facts.Attributes))
        {
            return;
        }

        if (HasCallbackShape(method, facts.StreamingContext)
            || method.Locations.Length == 0
            || !method.Locations[0].IsInSource)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(CorrectnessRules.SerializationCallbackSignature, method.Locations[0], method.Name));
    }

    /// <summary>Returns whether a method carries one of the serialization callback attributes.</summary>
    /// <param name="attributes">The method's attributes.</param>
    /// <param name="callbackAttributes">The resolved callback attribute types.</param>
    /// <returns><see langword="true"/> when at least one attribute is a serialization callback.</returns>
    private static bool CarriesCallbackAttribute(ImmutableArray<AttributeData> attributes, ImmutableArray<INamedTypeSymbol> callbackAttributes)
    {
        for (var i = 0; i < attributes.Length; i++)
        {
            var attributeClass = attributes[i].AttributeClass;
            if (attributeClass is null)
            {
                continue;
            }

            for (var j = 0; j < callbackAttributes.Length; j++)
            {
                if (SymbolEqualityComparer.Default.Equals(attributeClass, callbackAttributes[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a method has the shape the serializer invokes.</summary>
    /// <param name="method">The candidate callback method.</param>
    /// <param name="streamingContext">The resolved streaming-context type.</param>
    /// <returns><see langword="true"/> for a non-generic instance <c>void</c> method taking one <c>StreamingContext</c>.</returns>
    private static bool HasCallbackShape(IMethodSymbol method, INamedTypeSymbol streamingContext)
        => !method.IsStatic
            && !method.IsGenericMethod
            && method.ReturnsVoid
            && method.Parameters.Length == CallbackParameterCount
            && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, streamingContext);

    /// <summary>The resolved serialization callback facts for one compilation.</summary>
    /// <param name="StreamingContext">The streaming-context type a callback must take.</param>
    /// <param name="Attributes">The serialization callback attributes present in the compilation.</param>
    private readonly record struct CallbackFacts(INamedTypeSymbol StreamingContext, ImmutableArray<INamedTypeSymbol> Attributes);
}
