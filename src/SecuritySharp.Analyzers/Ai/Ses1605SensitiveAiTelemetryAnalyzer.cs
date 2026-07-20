// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags AI instrumentation told to capture raw prompts and responses as telemetry (SES1605). The rule
/// reports an <c>EnableSensitiveData = true</c> assignment -- a plain <c>otel.EnableSensitiveData = true</c>
/// statement or a <c>{ EnableSensitiveData = true }</c> object-initializer member (including the
/// configure-delegate shape <c>o =&gt; o.EnableSensitiveData = true</c>) -- whose target is the
/// <c>EnableSensitiveData</c> property of a <c>Microsoft.Extensions.AI</c> OpenTelemetry instrumentation
/// client (the chat, embedding, realtime, speech-to-text, text-to-speech, image, and hosted-file variants)
/// and whose right-hand side is the compile-time constant <c>true</c>. Turning the switch on writes every
/// prompt and completion verbatim to the telemetry backend, where it routinely carries secrets and PII. The
/// value is the direct right-hand side, so the check is purely local with no flow analysis. The rule is
/// gated on at least one of those instrumentation types resolving in the compilation, so a project without
/// <c>Microsoft.Extensions.AI</c> registers nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1605SensitiveAiTelemetryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the switch property that toggles raw prompt/response capture in telemetry.</summary>
    private const string EnableSensitiveDataPropertyName = "EnableSensitiveData";

    /// <summary>The metadata names of the AI instrumentation types whose <c>EnableSensitiveData</c> is guarded.</summary>
    private static readonly string[] InstrumentationMetadataNames =
    [
        "Microsoft.Extensions.AI.OpenTelemetryChatClient",
        "Microsoft.Extensions.AI.OpenTelemetryEmbeddingGenerator`2",
        "Microsoft.Extensions.AI.OpenTelemetryRealtimeClient",
        "Microsoft.Extensions.AI.OpenTelemetrySpeechToTextClient",
        "Microsoft.Extensions.AI.OpenTelemetryTextToSpeechClient",
        "Microsoft.Extensions.AI.OpenTelemetryImageGenerator",
        "Microsoft.Extensions.AI.OpenTelemetryHostedFileClient"
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.SensitiveAiTelemetry);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var instrumentationTypes = GetInstrumentationTypes(start.Compilation);
            if (instrumentationTypes is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, instrumentationTypes), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1605 for an <c>EnableSensitiveData = true</c> assignment on a gated instrumentation type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="instrumentationTypes">The gated instrumentation types resolved for the compilation.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol?[] instrumentationTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: the left side names 'EnableSensitiveData', as either 'x.EnableSensitiveData'
        // or a bare 'EnableSensitiveData' object-initializer member.
        if (!IsEnableSensitiveDataTarget(assignment.Left))
        {
            return;
        }

        // Only a compile-time 'true' enables raw capture; a false or a runtime-computed value is left alone.
        var constant = context.SemanticModel.GetConstantValue(assignment.Right, context.CancellationToken);
        if (constant.Value is not true)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol { Name: EnableSensitiveDataPropertyName } property
            || GetGatedInstrumentationType(property.ContainingType, instrumentationTypes) is not { } instrumentationType)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.SensitiveAiTelemetry,
            assignment.SyntaxTree,
            assignment.Span,
            instrumentationType.Name));
    }

    /// <summary>Returns whether an assignment target syntactically names the <c>EnableSensitiveData</c> property.</summary>
    /// <param name="left">The assignment's left-hand side.</param>
    /// <returns><see langword="true"/> for <c>x.EnableSensitiveData</c> or a bare <c>EnableSensitiveData</c> initializer member.</returns>
    private static bool IsEnableSensitiveDataTarget(ExpressionSyntax left)
        => left switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: EnableSensitiveDataPropertyName } => true,
            IdentifierNameSyntax { Identifier.ValueText: EnableSensitiveDataPropertyName } => true,
            _ => false,
        };

    /// <summary>Returns the gated instrumentation type when the property's container is one of them.</summary>
    /// <param name="containingType">The bound <c>EnableSensitiveData</c> property's containing type.</param>
    /// <param name="instrumentationTypes">The gated instrumentation types resolved for the compilation.</param>
    /// <returns>The gated type, or <see langword="null"/> when the container is not gated.</returns>
    private static INamedTypeSymbol? GetGatedInstrumentationType(INamedTypeSymbol containingType, INamedTypeSymbol?[] instrumentationTypes)
    {
        // The embedding generator is generic, so compare against the unbound definition of the container.
        var definition = containingType.OriginalDefinition;
        for (var i = 0; i < instrumentationTypes.Length; i++)
        {
            if (instrumentationTypes[i] is { } instrumentationType && SymbolEqualityComparer.Default.Equals(instrumentationType, definition))
            {
                return instrumentationType;
            }
        }

        return null;
    }

    /// <summary>Resolves the AI instrumentation types present in the compilation.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>An array whose slots hold each resolved type, or <see langword="null"/> when none resolve.</returns>
    private static INamedTypeSymbol?[]? GetInstrumentationTypes(Compilation compilation)
    {
        INamedTypeSymbol?[]? types = null;
        for (var i = 0; i < InstrumentationMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(InstrumentationMetadataNames[i]) is { } type)
            {
                types ??= new INamedTypeSymbol?[InstrumentationMetadataNames.Length];
                types[i] = type;
            }
        }

        return types;
    }
}
