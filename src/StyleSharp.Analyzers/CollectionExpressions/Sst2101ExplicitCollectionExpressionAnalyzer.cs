// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Suggests a C# 12 collection expression for explicit collection initializers (SST2101).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2101ExplicitCollectionExpressionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CollectionExpressionRules.UseExplicitCollectionExpression);

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
            var targets = new CollectionTargets(start.Compilation);
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
            _ => null
        };
        return initializer is not null;
    }

    /// <summary>Reports an accepted explicit collection creation.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="targets">The accepted target definitions resolved on demand.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, CollectionTargets targets)
    {
        if (context.Node is not ExpressionSyntax expression
            || !CollectionExpressionHelper.IsLanguageSupported(expression)
            || !TryGetInitializer(expression, out var initializer)
            || initializer!.Expressions.Count == 0
            || HasComplexElement(initializer)
            || !HasAcceptedTarget(context, expression, targets)
            || ChangesOverloadResolution(context, expression, initializer!))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(CollectionExpressionRules.UseExplicitCollectionExpression, expression.GetLocation()));
    }

    /// <summary>Checks the target context before resolving named collection definitions.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="expression">The collection creation.</param>
    /// <param name="targets">The accepted target definitions resolved on demand.</param>
    /// <returns>Whether the explicit target is a one-dimensional array or an accepted named collection.</returns>
    private static bool HasAcceptedTarget(in SyntaxNodeAnalysisContext context, ExpressionSyntax expression, CollectionTargets targets)
    {
        if (!CollectionExpressionHelper.TryGetConvertedTypeWithExplicitTarget(context, expression, out var converted))
        {
            return false;
        }

        if (converted is IArrayTypeSymbol { Rank: 1 })
        {
            return true;
        }

        if (converted is not INamedTypeSymbol named)
        {
            return false;
        }

        var resolvedTargets = targets.Get();
        for (var i = 0; i < resolvedTargets.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(resolvedTargets[i], named.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether writing the creation as a collection expression would call something else.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="expression">The collection creation.</param>
    /// <param name="initializer">The creation's initializer.</param>
    /// <returns><see langword="true"/> when the call would bind to a different member, or stop binding.</returns>
    /// <remarks>
    /// An argument's written type is what picks the overload. A collection expression has no type of its
    /// own and takes one from the parameter, so dropping the type can select a different member — one that
    /// still compiles and quietly does something else. The rewritten call is bound before the suggestion
    /// is offered, and anything that does not land back on the same member is left alone.
    /// </remarks>
    private static bool ChangesOverloadResolution(in SyntaxNodeAnalysisContext context, ExpressionSyntax expression, InitializerExpressionSyntax initializer)
    {
        if (expression.Parent is not ArgumentSyntax argument
            || argument.Parent is not BaseArgumentListSyntax list
            || list.Parent is not ExpressionSyntax call)
        {
            return false;
        }

        var original = context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol;
        if (original is null)
        {
            return true;
        }

        var elements = new List<CollectionElementSyntax>(initializer.Expressions.Count);
        for (var i = 0; i < initializer.Expressions.Count; i++)
        {
            elements.Add(SyntaxFactory.ExpressionElement(initializer.Expressions[i].WithoutTrivia()));
        }

        var rewritten = call.ReplaceNode(expression, SyntaxFactory.CollectionExpression(SyntaxFactory.SeparatedList(elements)));
        var speculative = context.SemanticModel.GetSpeculativeSymbolInfo(call.SpanStart, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol;
        return !SymbolEqualityComparer.Default.Equals(original, speculative);
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

    /// <summary>Resolves named collection targets only when a candidate needs them.</summary>
    /// <param name="compilation">The compilation whose collection target definitions are cached.</param>
    private sealed class CollectionTargets(Compilation compilation)
    {
        /// <summary>The resolved target definitions, including an empty result when no target resolves.</summary>
        private INamedTypeSymbol[]? _resolved;

        /// <summary>Gets the target definitions, allowing equivalent concurrent first resolutions.</summary>
        /// <returns>The accepted named collection definitions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol[] Get() => _resolved ??= CollectionExpressionHelper.ResolveTargets(compilation);
    }
}
