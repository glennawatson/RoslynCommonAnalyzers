// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports questionable <c>lock</c> targets: a field or property reachable from
/// outside the declaring type (SST1901), <c>this</c>, a <see cref="System.Type"/>,
/// or a string (SST1902, opt-in), and a freshly-created object (SST1903). In each
/// case the lock either leaks to unrelated code or cannot coordinate with any
/// other caller, so the fix is to lock on a private, dedicated object instead.
/// These are general advice rather than a runtime feature, so they apply on every
/// framework.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LockTargetAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ConcurrencyRules.DoNotLockOnAccessibleMember,
        ConcurrencyRules.DoNotLockOnWeakIdentity,
        ConcurrencyRules.DoNotLockOnNewlyCreatedObject);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var typeSymbol = start.Compilation.GetTypeByMetadataName("System.Type");
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeLock(nodeContext, typeSymbol), SyntaxKind.LockStatement);
        });
    }

    /// <summary>Reports SST1901/SST1903 for a questionable lock target.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="typeSymbol">The resolved <c>System.Type</c> symbol, if any.</param>
    private static void AnalyzeLock(SyntaxNodeAnalysisContext context, INamedTypeSymbol? typeSymbol)
    {
        var expression = UnwrapParentheses(((LockStatementSyntax)context.Node).Expression);

        if (IsNewlyCreatedObject(expression, context.SemanticModel, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(ConcurrencyRules.DoNotLockOnNewlyCreatedObject, expression.GetLocation()));
        }

        if (IsWeakIdentity(expression, context.SemanticModel, typeSymbol, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(ConcurrencyRules.DoNotLockOnWeakIdentity, expression.GetLocation()));
        }

        ReportAccessibleMember(context, expression);
    }

    /// <summary>Reports SST1901 when the lock target is an externally-accessible field or property.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The lock target expression.</param>
    private static void ReportAccessibleMember(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        var symbol = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol;
        if (symbol is not (IFieldSymbol or IPropertySymbol) || !IsExternallyAccessible(symbol.DeclaredAccessibility))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ConcurrencyRules.DoNotLockOnAccessibleMember, expression.GetLocation(), symbol.Name));
    }

    /// <summary>Returns whether a lock expression creates a fresh object on every evaluation.</summary>
    /// <param name="expression">The lock target expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the target is an object creation expression.</returns>
    private static bool IsNewlyCreatedObject(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken)
        => (expression is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
            && model.GetTypeInfo(expression, cancellationToken).Type is not null;

    /// <summary>Returns whether a lock expression is <c>this</c>, a <c>Type</c>, or a string.</summary>
    /// <param name="expression">The lock target expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="typeSymbol">The resolved <c>System.Type</c> symbol, if any.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the target has weak identity.</returns>
    private static bool IsWeakIdentity(ExpressionSyntax expression, SemanticModel model, INamedTypeSymbol? typeSymbol, CancellationToken cancellationToken)
    {
        if (expression is ThisExpressionSyntax or TypeOfExpressionSyntax)
        {
            return true;
        }

        var type = model.GetTypeInfo(expression, cancellationToken).Type;
        if (type is null)
        {
            return false;
        }

        return type.SpecialType == SpecialType.System_String || (typeSymbol is not null && IsOrDerivesFrom(type, typeSymbol));
    }

    /// <summary>Returns whether a type is, or derives from, the target type.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="target">The target base type.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is or inherits <paramref name="target"/>.</returns>
    private static bool IsOrDerivesFrom(ITypeSymbol type, INamedTypeSymbol target)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an accessibility is reachable from outside the declaring assembly.</summary>
    /// <param name="accessibility">The member accessibility.</param>
    /// <returns><see langword="true"/> for public, protected, or protected-or-internal.</returns>
    private static bool IsExternallyAccessible(Accessibility accessibility)
        => accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;

    /// <summary>Removes parentheses around a lock target so checks can reason about the underlying expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax { Expression: { } inner })
        {
            expression = inner;
        }

        return expression;
    }
}
