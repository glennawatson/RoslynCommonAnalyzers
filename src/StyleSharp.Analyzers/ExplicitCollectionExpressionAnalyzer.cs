// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Suggests a C# 12 collection expression for explicit collection initializers (SST2101).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExplicitCollectionExpressionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CollectionExpressionRules.UseExplicitCollectionExpression);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(start =>
        {
            var targets = CollectionExpressionHelper.ResolveTargets(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, targets),
                SyntaxKind.ArrayCreationExpression,
                SyntaxKind.ImplicitArrayCreationExpression,
                SyntaxKind.ObjectCreationExpression);
        });
    }

    /// <summary>Gets the initializer carried by a supported explicit collection creation.</summary>
    /// <param name="expression">The collection creation.</param>
    /// <param name="initializer">The initializer.</param>
    /// <returns><see langword="true"/> when an initializer is present.</returns>
    internal static bool TryGetInitializer(ExpressionSyntax expression, out InitializerExpressionSyntax? initializer)
    {
        initializer = expression switch
        {
            ArrayCreationExpressionSyntax array => array.Initializer,
            ImplicitArrayCreationExpressionSyntax array => array.Initializer,
            ObjectCreationExpressionSyntax creation => creation.Initializer,
            _ => null,
        };
        return initializer is not null;
    }

    /// <summary>Reports an accepted explicit collection creation.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="targets">The accepted target definitions.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol[] targets)
    {
        if (context.Node is not ExpressionSyntax expression
            || !CollectionExpressionHelper.IsLanguageSupported(expression)
            || !TryGetInitializer(expression, out var initializer)
            || initializer!.Expressions.Count == 0
            || HasComplexElement(initializer)
            || !CollectionExpressionHelper.HasAcceptedTarget(context, expression, targets))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(CollectionExpressionRules.UseExplicitCollectionExpression, expression.GetLocation()));
    }

    /// <summary>Returns whether an initializer contains a multi-argument element.</summary>
    /// <param name="initializer">The initializer.</param>
    /// <returns><see langword="true"/> for dictionary-style or complex elements.</returns>
    private static bool HasComplexElement(InitializerExpressionSyntax initializer)
    {
        for (var i = 0; i < initializer.Expressions.Count; i++)
        {
            if (initializer.Expressions[i] is InitializerExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }
}
