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

    /// <summary>Returns whether the construction is built out of state a static field could not hold.</summary>
    /// <param name="creation">The object creation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the options are shaped by a parameter, a local, or instance state.</returns>
    /// <remarks>
    /// The suggestion is to hoist the options into a <c>static readonly</c> field, and that is only possible
    /// when the options do not depend on anything the caller brought with them. Options built around a
    /// parameter — <c>new() { TypeInfoResolver = resolver }</c> — cannot be shared: one static instance would
    /// hand every caller the first caller's resolver. The options are per-call because they are per-caller,
    /// and the rule has nothing to offer.
    /// <para>
    /// The names on the left of an object initializer are the options' own properties, not captured state, so
    /// they are stepped over — otherwise every initializer would look like a capture.
    /// </para>
    /// </remarks>
    private static bool DependsOnStateAStaticFieldCannotHold(
        BaseObjectCreationExpressionSyntax creation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var node in creation.DescendantNodes())
        {
            if (node is ThisExpressionSyntax or BaseExpressionSyntax)
            {
                return true;
            }

            if (node is not IdentifierNameSyntax identifier || IsPropertyBeingSet(identifier) || IsMemberName(identifier))
            {
                continue;
            }

            switch (model.GetSymbolInfo(identifier, cancellationToken).Symbol)
            {
                case ILocalSymbol:
                case IParameterSymbol:
                case IFieldSymbol { IsStatic: false }:
                case IPropertySymbol { IsStatic: false }:
                    return true;

                default:
                    continue;
            }
        }

        return false;
    }

    /// <summary>Returns whether an identifier names a property the initializer is setting.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns><see langword="true"/> for the <c>Foo</c> in <c>new() { Foo = value }</c>.</returns>
    private static bool IsPropertyBeingSet(IdentifierNameSyntax identifier)
        => identifier.Parent is AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax } assignment
            && assignment.Left == identifier;

    /// <summary>Returns whether an identifier is the member half of an access rather than the receiver.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns><see langword="true"/> for the <c>CamelCase</c> in <c>JsonNamingPolicy.CamelCase</c>.</returns>
    private static bool IsMemberName(IdentifierNameSyntax identifier)
        => identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier;

    /// <summary>Reports PSH1416 for a serializer options instance built on every call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="optionsType">The <c>JsonSerializerOptions</c> type in the current compilation.</param>
    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol optionsType)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (!IsConstructedPerCall(creation)
            || context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not { } created
            || !SymbolEqualityComparer.Default.Equals(created, optionsType)
            || DependsOnStateAStaticFieldCannotHold(creation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.CacheSerializerOptions,
            creation.GetLocation(),
            OptionsTypeName));
    }
}
