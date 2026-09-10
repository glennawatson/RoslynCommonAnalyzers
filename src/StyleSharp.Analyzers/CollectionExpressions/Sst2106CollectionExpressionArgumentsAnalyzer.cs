// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a collection creation whose only constructor arguments are a capacity or a comparer,
/// where a C# 15 collection expression with a <c>with(...)</c> element says the same thing (SST2106).
/// </summary>
/// <remarks>
/// Gated on C# 15 because <c>with(...)</c> does not parse below it, and on the created type being one
/// of the three collections the element is defined to forward to. An argument that supplies contents
/// rather than configuration is deliberately not reported: that rewrite is a spread element, which is
/// a different shape and belongs to the rules covering seeding a collection from a source.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2106CollectionExpressionArgumentsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue =
        ImmutableArrays.Of(CollectionExpressionRules.UseCollectionExpressionArguments);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var targets = CollectionExpressionArgumentTargets.Resolve(start.Compilation);
            if (targets is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, targets),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Reports one collection creation whose arguments are pure configuration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="targets">The resolved collection and comparer symbols.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, CollectionExpressionArgumentTargets targets)
    {
        if (!LanguageVersions.SupportsCSharp15(context.Node))
        {
            return;
        }

        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is not { Arguments.Count: > 0 } arguments || !IsPositionalOnly(arguments))
        {
            return;
        }

        if (!HasExplicitTarget(creation))
        {
            return;
        }

        var created = ConfiguredCollection(context, creation, targets);
        if (created is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionExpressionRules.UseCollectionExpressionArguments,
            creation.GetLocation(),
            created.Name));
    }

    /// <summary>Gets whether every argument is positional, so it maps onto a <c>with(...)</c> element.</summary>
    /// <param name="arguments">The constructor argument list.</param>
    /// <returns><see langword="true"/> when no argument is named or passed by reference.</returns>
    private static bool IsPositionalOnly(ArgumentListSyntax arguments)
    {
        foreach (var argument in arguments.Arguments)
        {
            if (argument.NameColon is not null || !argument.RefKindKeyword.IsKind(SyntaxKind.None))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the created collection when every constructor argument is pure configuration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="creation">The object creation.</param>
    /// <param name="targets">The resolved collection and comparer symbols.</param>
    /// <returns>The constructed collection type, or <see langword="null"/> when the shape does not qualify.</returns>
    private static INamedTypeSymbol? ConfiguredCollection(
        SyntaxNodeAnalysisContext context,
        BaseObjectCreationExpressionSyntax creation,
        CollectionExpressionArgumentTargets targets)
    {
        if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol constructor)
        {
            return null;
        }

        var created = constructor.ContainingType;
        if (created is null || !targets.IsSupportedCollection(created))
        {
            return null;
        }

        foreach (var parameter in constructor.Parameters)
        {
            if (!targets.IsConfigurationParameter(parameter.Type))
            {
                return null;
            }
        }

        return created;
    }

    /// <summary>Gets whether the creation sits somewhere a collection expression can take its type from.</summary>
    /// <param name="creation">The object creation.</param>
    /// <returns><see langword="true"/> when an explicit target type is present.</returns>
    /// <remarks>
    /// A collection expression is target-typed, so it needs a target that is not itself inferred.
    /// <c>new(...)</c> already proves one exists; an explicit <c>new List&lt;T&gt;(...)</c> only
    /// carries one when it initializes a declaration whose type is written out rather than <c>var</c>.
    /// </remarks>
    private static bool HasExplicitTarget(BaseObjectCreationExpressionSyntax creation)
    {
        if (creation is ImplicitObjectCreationExpressionSyntax)
        {
            return true;
        }

        if (creation.Parent is not EqualsValueClauseSyntax equals)
        {
            return false;
        }

        return equals.Parent switch
        {
            VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } => !declaration.Type.IsVar,
            PropertyDeclarationSyntax => true,
            _ => false,
        };
    }
}
