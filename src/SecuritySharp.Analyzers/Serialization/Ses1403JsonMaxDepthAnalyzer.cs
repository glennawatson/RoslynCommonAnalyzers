// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a <c>System.Text.Json</c> <c>MaxDepth</c> raised past a safe ceiling (SES1403). The rule
/// reports a <c>MaxDepth</c> assignment -- a plain <c>options.MaxDepth = N</c> statement or a
/// <c>{ MaxDepth = N }</c> object-initializer member -- whose target is the <c>MaxDepth</c> property of
/// <c>JsonSerializerOptions</c>, <c>JsonReaderOptions</c>, or <c>JsonDocumentOptions</c> and whose value
/// is a compile-time constant strictly greater than the configured ceiling (default 64, overridable per
/// project via <c>securitysharp.SES1403.maxdepth</c> or <c>securitysharp.maxdepth</c>). The default of 64
/// exists to bound recursion; a constant far above it re-opens a deep-nesting denial-of-service surface.
/// A value of 0 selects the framework default, and any value at or below the ceiling, or one whose size
/// cannot be judged from the source, is left alone -- so only a deliberately raised constant is reported.
/// This is a purely local shape: the value is the direct right-hand side, so no flow analysis is needed.
/// The rule is gated on <c>JsonSerializerOptions</c> resolving in the compilation, so a target framework
/// without <c>System.Text.Json</c> pays nothing and never receives a diagnostic it cannot act on.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1403JsonMaxDepthAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The default depth ceiling when no <c>.editorconfig</c> value is set (the framework default).</summary>
    internal const int DefaultMaxDepthCeiling = 64;

    /// <summary>The name of the depth-limit property inspected on the JSON option types.</summary>
    private const string MaxDepthPropertyName = "MaxDepth";

    /// <summary>The rule-specific ceiling key.</summary>
    private const string MaxDepthRuleKey = "securitysharp.SES1403.maxdepth";

    /// <summary>The project-wide ceiling key.</summary>
    private const string MaxDepthGeneralKey = "securitysharp.maxdepth";

    /// <summary>The smallest ceiling that means anything: a ceiling below 1 would flag every positive depth.</summary>
    private const int SmallestCeiling = 1;

    /// <summary>The metadata names of the JSON option types whose <c>MaxDepth</c> is guarded.</summary>
    private static readonly string[] JsonOptionMetadataNames =
    [
        "System.Text.Json.JsonSerializerOptions",
        "System.Text.Json.JsonReaderOptions",
        "System.Text.Json.JsonDocumentOptions"
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.JsonMaxDepth);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var jsonTypes = GetJsonOptionTypes(start.Compilation);
            if (jsonTypes is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, jsonTypes), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1403 for a <c>MaxDepth</c> assignment whose constant value exceeds the ceiling.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="jsonTypes">The gated JSON option types resolved for the compilation.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol?[] jsonTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: the left side names 'MaxDepth', as either 'x.MaxDepth' or a bare
        // 'MaxDepth' object-initializer member.
        if (!IsMaxDepthTarget(assignment.Left))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol { Name: MaxDepthPropertyName } property
            || GetGatedJsonType(property.ContainingType, jsonTypes) is null)
        {
            return;
        }

        // A non-constant depth (a config value or a runtime computation) cannot be judged here, so stay
        // silent. A value at or below the ceiling -- including 0, which selects the framework default --
        // is safe and left alone.
        var constant = context.SemanticModel.GetConstantValue(assignment.Right, context.CancellationToken);
        if (!constant.HasValue || constant.Value is not int depth)
        {
            return;
        }

        var ceiling = ReadMaxDepthCeiling(context.Options.AnalyzerConfigOptionsProvider.GetOptions(assignment.Right.SyntaxTree));
        if (depth <= ceiling)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.JsonMaxDepth,
            assignment.Right.SyntaxTree,
            assignment.Right.Span,
            depth.ToString(CultureInfo.InvariantCulture),
            ceiling.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Returns whether an assignment target syntactically names the <c>MaxDepth</c> property.</summary>
    /// <param name="left">The assignment's left-hand side.</param>
    /// <returns><see langword="true"/> for <c>x.MaxDepth</c> or a bare <c>MaxDepth</c> initializer member.</returns>
    private static bool IsMaxDepthTarget(ExpressionSyntax left)
        => left switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: MaxDepthPropertyName } => true,
            IdentifierNameSyntax { Identifier.ValueText: MaxDepthPropertyName } => true,
            _ => false,
        };

    /// <summary>Returns the gated JSON option type when the property's container is one of them.</summary>
    /// <param name="containingType">The bound <c>MaxDepth</c> property's containing type.</param>
    /// <param name="jsonTypes">The gated JSON option types resolved for the compilation.</param>
    /// <returns>The gated type, or <see langword="null"/> when the container is not gated.</returns>
    private static INamedTypeSymbol? GetGatedJsonType(INamedTypeSymbol containingType, INamedTypeSymbol?[] jsonTypes)
    {
        for (var i = 0; i < jsonTypes.Length; i++)
        {
            if (jsonTypes[i] is { } jsonType && SymbolEqualityComparer.Default.Equals(jsonType, containingType))
            {
                return jsonType;
            }
        }

        return null;
    }

    /// <summary>Reads the depth ceiling, preferring the rule-specific key over the project-wide key.</summary>
    /// <param name="options">The analyzer config options for the value's tree.</param>
    /// <returns>The configured ceiling, or <see cref="DefaultMaxDepthCeiling"/> when neither key parses to a sensible value.</returns>
    private static int ReadMaxDepthCeiling(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(MaxDepthRuleKey, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= SmallestCeiling)
        {
            return parsed;
        }

        return options.TryGetValue(MaxDepthGeneralKey, out value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
            && parsed >= SmallestCeiling
            ? parsed
            : DefaultMaxDepthCeiling;
    }

    /// <summary>Resolves the JSON option types present in the compilation.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>An array whose slots hold each resolved type, or <see langword="null"/> when none resolve.</returns>
    private static INamedTypeSymbol?[]? GetJsonOptionTypes(Compilation compilation)
    {
        INamedTypeSymbol?[]? types = null;
        for (var i = 0; i < JsonOptionMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(JsonOptionMetadataNames[i]) is { } type)
            {
                types ??= new INamedTypeSymbol?[JsonOptionMetadataNames.Length];
                types[i] = type;
            }
        }

        return types;
    }
}
