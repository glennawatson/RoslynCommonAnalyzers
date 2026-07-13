// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a <c>JsonSerializerOptions</c> constructed inside a method body, so a fresh instance is
/// built on every call (PSH1416). The serializer caches its per-type metadata <em>against the
/// options instance</em> it was handed, so a new instance throws that cache away and re-derives
/// the whole contract — reflection, converter lookup and all — for a type it has already seen.
/// This is one of the most expensive accidental costs in a serialization path.
/// <para>
/// Only per-call construction is reported. A field or property <em>initializer</em> runs once and
/// is left alone, as is anything built in a constructor, so the cached
/// <c>static readonly JsonSerializerOptions</c> the rule is steering toward never reports itself.
/// An expression-bodied property, which does run on every read, is reported. The rule is resolved
/// once per compilation by probing for <c>System.Text.Json.JsonSerializerOptions</c>, so it costs
/// nothing where the serializer is not referenced.
/// </para>
/// <para>
/// There is no code fix. Hoisting the construction has to invent a field name, choose where to put
/// it, and — the part no analyzer can settle at the creation site — be sure the options are never
/// mutated afterwards, because <c>options.Converters.Add(...)</c> against a now-shared instance
/// would change behavior rather than just move an allocation.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1416CacheSerializerOptionsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The type name used in the diagnostic message.</summary>
    internal const string OptionsTypeName = "JsonSerializerOptions";

    /// <summary>The metadata name of the serializer options type.</summary>
    private const string OptionsMetadataName = "System.Text.Json.JsonSerializerOptions";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.CacheSerializerOptions);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(OptionsMetadataName) is not { } optionsType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeCreation(nodeContext, optionsType),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Returns whether a construction runs on every call, rather than once at initialization.</summary>
    /// <param name="creation">The object creation.</param>
    /// <returns><see langword="true"/> when the creation sits in a body that runs per call.</returns>
    internal static bool IsConstructedPerCall(SyntaxNode creation)
    {
        for (var current = creation.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax:
                    return true;
                case PropertyDeclarationSyntax property:
                    return property.Initializer?.Span.Contains(creation.Span) != true;
                case FieldDeclarationSyntax or ConstructorDeclarationSyntax or BaseTypeDeclarationSyntax:
                    return false;
                default:
                    continue;
            }
        }

        return false;
    }

    /// <summary>Reports PSH1416 for a serializer options instance built on every call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="optionsType">The <c>JsonSerializerOptions</c> type in the current compilation.</param>
    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol optionsType)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (!IsConstructedPerCall(creation)
            || context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not { } created
            || !SymbolEqualityComparer.Default.Equals(created, optionsType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.CacheSerializerOptions,
            creation.GetLocation(),
            OptionsTypeName));
    }
}
