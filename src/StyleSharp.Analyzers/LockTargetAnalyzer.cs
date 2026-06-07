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

    /// <summary>Returns whether the lock target is syntactically known to be a private object field declared in the same type.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="expression">The lock target expression.</param>
    /// <returns><see langword="true"/> when the target is a clean private object field use.</returns>
    internal static bool IsPrivateObjectFieldLockTarget(TypeDeclarationSyntax type, ExpressionSyntax expression)
        => FieldReferenceAnalysis.IsPrivateObjectFieldLockTarget(type, expression);

    /// <summary>Reports SST1901/SST1903 for a questionable lock target.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="typeSymbol">The resolved <c>System.Type</c> symbol, if any.</param>
    private static void AnalyzeLock(SyntaxNodeAnalysisContext context, INamedTypeSymbol? typeSymbol)
    {
        var expression = UnwrapLockTarget(((LockStatementSyntax)context.Node).Expression);

        if (context.Node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } type
            && IsPrivateObjectFieldLockTarget(type, expression))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol;

        if (IsNewlyCreatedObject(expression, context.SemanticModel, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(ConcurrencyRules.DoNotLockOnNewlyCreatedObject, expression.GetLocation()));
        }

        if (IsWeakIdentity(expression, symbol, context.SemanticModel, typeSymbol, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(ConcurrencyRules.DoNotLockOnWeakIdentity, expression.GetLocation()));
        }

        ReportAccessibleMember(context, expression, symbol);
    }

    /// <summary>Reports SST1901 when the lock target is an externally-accessible field or property.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The lock target expression.</param>
    /// <param name="symbol">The bound symbol for the lock target, if any.</param>
    private static void ReportAccessibleMember(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, ISymbol? symbol)
    {
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
    /// <param name="symbol">The bound symbol for the lock target, if any.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="typeSymbol">The resolved <c>System.Type</c> symbol, if any.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the target has weak identity.</returns>
    private static bool IsWeakIdentity(
        ExpressionSyntax expression,
        ISymbol? symbol,
        SemanticModel model,
        INamedTypeSymbol? typeSymbol,
        CancellationToken cancellationToken)
    {
        if (expression is ThisExpressionSyntax or TypeOfExpressionSyntax)
        {
            return true;
        }

        var type = symbol switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => model.GetTypeInfo(expression, cancellationToken).Type,
        };

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

    /// <summary>Removes wrappers that do not change the locked instance.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost lock-target expression.</returns>
    private static ExpressionSyntax UnwrapLockTarget(ExpressionSyntax expression)
    {
        var current = expression;

        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax { Expression: { } inner }:
                {
                    current = inner;
                    break;
                }

                case CastExpressionSyntax { Type: { } type, Expression: { } inner } when IsObjectType(type):
                {
                    current = inner;
                    break;
                }

                default:
                    return current;
            }
        }
    }

    /// <summary>Returns whether a cast type is spelled as <c>object</c> or <c>System.Object</c>.</summary>
    /// <param name="type">The cast target type syntax.</param>
    /// <returns><see langword="true"/> when the cast target is object.</returns>
    private static bool IsObjectType(TypeSyntax type)
    {
        if (type is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword })
        {
            return true;
        }

        if (type is not QualifiedNameSyntax qualifiedName
            || qualifiedName.Right.Identifier.ValueText != "Object")
        {
            return false;
        }

        return IsSystemTypeName(qualifiedName.Left);
    }

    /// <summary>Returns whether a type name is spelled as <c>System</c> or <c>global::System</c>.</summary>
    /// <param name="name">The name syntax to inspect.</param>
    /// <returns><see langword="true"/> when the name refers to <c>System</c>.</returns>
    private static bool IsSystemTypeName(NameSyntax name)
    {
        if (name is IdentifierNameSyntax { Identifier.ValueText: "System" })
        {
            return true;
        }

        return name is AliasQualifiedNameSyntax aliasQualifiedName
            && aliasQualifiedName.Alias.Identifier.ValueText == "global"
            && aliasQualifiedName.Name.Identifier.ValueText == "System";
    }
}
