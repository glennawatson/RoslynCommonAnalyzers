// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports properties that trivially wrap a private single-use backing field (SST1420).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TrivialAutoPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.PreferAutoProperty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Returns whether all property accessors directly read or assign the backing field.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="property">The property.</param>
    /// <param name="field">The backing-field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when all accessors are trivial.</returns>
    internal static bool HasOnlyTrivialAccessors(
        SemanticModel model,
        PropertyDeclarationSyntax property,
        IFieldSymbol field,
        CancellationToken cancellationToken)
    {
        if (property.AccessorList is not { Accessors.Count: > 0 } accessors)
        {
            return false;
        }

        for (var i = 0; i < accessors.Accessors.Count; i++)
        {
            var accessor = accessors.Accessors[i];
            if (accessor.Keyword.IsKind(SyntaxKind.GetKeyword))
            {
                if (!IsTrivialGet(model, accessor, field, cancellationToken))
                {
                    return false;
                }
            }
            else if (!IsTrivialSet(model, accessor, field, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reports a property when every accessor is a direct field read or write.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (!FieldReferenceAnalysis.TryFindSingleUseBackingField(
                context.SemanticModel,
                property,
                context.CancellationToken,
                out _,
                out _,
                out var field)
            || !HasOnlyTrivialAccessors(context.SemanticModel, property, field!, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.PreferAutoProperty, property.Identifier.GetLocation()));
    }

    /// <summary>Returns whether a getter directly returns the backing field.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="accessor">The getter.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for a trivial getter.</returns>
    private static bool IsTrivialGet(
        SemanticModel model,
        AccessorDeclarationSyntax accessor,
        IFieldSymbol field,
        CancellationToken cancellationToken)
    {
        var expression = accessor.ExpressionBody?.Expression;
        if (expression is null && accessor.Body?.Statements is [ReturnStatementSyntax returnStatement])
        {
            expression = returnStatement.Expression;
        }

        return expression is not null
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(expression, cancellationToken).Symbol, field);
    }

    /// <summary>Returns whether a write accessor directly assigns <c>value</c> to the backing field.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="accessor">The write accessor.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for a trivial write accessor.</returns>
    private static bool IsTrivialSet(
        SemanticModel model,
        AccessorDeclarationSyntax accessor,
        IFieldSymbol field,
        CancellationToken cancellationToken)
    {
        var expression = accessor.ExpressionBody?.Expression;
        if (expression is null && accessor.Body?.Statements is [ExpressionStatementSyntax statement])
        {
            expression = statement.Expression;
        }

        return expression is AssignmentExpressionSyntax { Left: var left, Right: IdentifierNameSyntax { Identifier.Text: "value" } }
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(left, cancellationToken).Symbol, field);
    }
}
