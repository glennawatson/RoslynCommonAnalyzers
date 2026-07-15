// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports questionable <c>lock</c> targets: a field or property reachable from
/// outside the declaring type (SST1901), <c>this</c>, a <see cref="Type"/>,
/// or a string (SST1902, opt-in), an object that is fresh on every evaluation —
/// <c>new object()</c> inline, or a local whose only initializer is a <c>new</c>
/// that never leaves the method (SST1903) — and a non-readonly field of the
/// declaring type that another assignment can swap out (SST1904). In each case the
/// lock either leaks to unrelated code, cannot coordinate with any other caller,
/// or can be changed mid-flight, so the fix is to lock on a private, dedicated,
/// readonly object instead. These are general advice rather than a runtime feature,
/// so they apply on every framework.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LockTargetAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ConcurrencyRules.DoNotLockOnAccessibleMember,
        ConcurrencyRules.DoNotLockOnWeakIdentity,
        ConcurrencyRules.DoNotLockOnNewlyCreatedObject,
        ConcurrencyRules.DoNotLockOnNonReadonlyField);

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

    /// <summary>Reports SST1901/SST1902/SST1903/SST1904 for a questionable lock target.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="typeSymbol">The resolved <c>System.Type</c> symbol, if any.</param>
    private static void AnalyzeLock(SyntaxNodeAnalysisContext context, INamedTypeSymbol? typeSymbol)
    {
        var expression = UnwrapLockTarget(((LockStatementSyntax)context.Node).Expression);

        if (TryReportPrivateObjectField(context, expression))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol;

        ReportSwappableField(context, expression, symbol);
        ReportFreshObject(context, expression, symbol);

        if (IsWeakIdentity(expression, symbol, context.SemanticModel, typeSymbol, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(ConcurrencyRules.DoNotLockOnWeakIdentity, expression.GetLocation()));
        }

        ReportAccessibleMember(context, expression, symbol);
    }

    /// <summary>Handles the private-object-field fast path, reporting SST1904 for a non-readonly one.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The unwrapped lock target expression.</param>
    /// <returns><see langword="true"/> when the target is a private object field, so no other rule applies.</returns>
    private static bool TryReportPrivateObjectField(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        if (context.Node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } type
            || !FieldReferenceAnalysis.TryGetPrivateObjectFieldLockTarget(type, expression, out var declaration))
        {
            return false;
        }

        // A private object field is clean unless it is not readonly, in which case it is SST1904.
        if (declaration is not { } field || field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            return true;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ConcurrencyRules.DoNotLockOnNonReadonlyField,
            expression.GetLocation(),
            ((IdentifierNameSyntax)expression).Identifier.ValueText));
        return true;
    }

    /// <summary>Reports SST1904 when the lock target is a non-readonly field of the enclosing type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The lock target expression.</param>
    /// <param name="symbol">The bound symbol for the lock target, if any.</param>
    private static void ReportSwappableField(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, ISymbol? symbol)
    {
        if (symbol is not IFieldSymbol field || !IsSwappableOwnField(field, context.ContainingSymbol?.ContainingType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ConcurrencyRules.DoNotLockOnNonReadonlyField, expression.GetLocation(), field.Name));
    }

    /// <summary>Reports SST1903 when the lock target is a fresh object, inline or through a local.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The lock target expression.</param>
    /// <param name="symbol">The bound symbol for the lock target, if any.</param>
    private static void ReportFreshObject(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, ISymbol? symbol)
    {
        if (!IsNewlyCreatedObject(expression, context.SemanticModel, context.CancellationToken)
            && !IsFreshLocalObject(expression, symbol, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ConcurrencyRules.DoNotLockOnNewlyCreatedObject, expression.GetLocation()));
    }

    /// <summary>Returns whether a lock target is a non-readonly field the declaring type could reassign.</summary>
    /// <param name="field">The bound field symbol.</param>
    /// <param name="enclosingType">The type the lock statement lives in.</param>
    /// <returns><see langword="true"/> when the field is a mutable, internally-owned lock target.</returns>
    /// <remarks>
    /// An externally accessible field is already SST1901's territory, and its fix — a private dedicated
    /// object — closes the mutability too; scoping SST1904 to fields SST1901 does not see keeps the two
    /// disjoint. The field must belong to the type that holds the lock, so the fix is a local one.
    /// </remarks>
    private static bool IsSwappableOwnField(IFieldSymbol field, INamedTypeSymbol? enclosingType)
        => !field.IsReadOnly
            && !field.IsConst
            && !IsExternallyAccessible(field.DeclaredAccessibility)
            && enclosingType is not null
            && SymbolEqualityComparer.Default.Equals(field.ContainingType, enclosingType);

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

    /// <summary>Returns whether a lock target is a local that only ever holds a freshly-created object that never leaves the method.</summary>
    /// <param name="expression">The lock target expression.</param>
    /// <param name="symbol">The bound symbol for the lock target, if any.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the local is a fresh, unpublished object and the lock can never contend.</returns>
    /// <remarks>
    /// This follows the local exactly one hop, to its initializer. A local initialized with <c>new</c> that is
    /// never reassigned and never escapes the method — not stored to a field, not passed as an argument, not
    /// captured — is a different instance on every call, so no two callers ever take the same lock. A local
    /// initialized from a field, a property, a parameter, or a method call is an alias for something shared and
    /// is left alone: <c>object local = _gate; lock (local)</c> is correct.
    /// </remarks>
    private static bool IsFreshLocalObject(ExpressionSyntax expression, ISymbol? symbol, SemanticModel model, CancellationToken cancellationToken)
    {
        if (expression is not IdentifierNameSyntax identifier
            || symbol is not ILocalSymbol local
            || local.DeclaringSyntaxReferences is not [var syntaxReference]
            || syntaxReference.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax declarator
            || declarator.Initializer?.Value is not (ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
            || GetLocalScope(declarator) is not { } scope)
        {
            return false;
        }

        var scan = new FreshLocalScan(model, local, identifier, scope, cancellationToken);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, FreshLocalScan>(scope, ref scan, VisitFreshLocalReference);
        return !scan.Escaped;
    }

    /// <summary>Classifies one reference to a fresh local, stopping the walk once the value is shown to escape.</summary>
    /// <param name="reference">The identifier being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once the local is reassigned or published.</returns>
    private static bool VisitFreshLocalReference(IdentifierNameSyntax reference, ref FreshLocalScan state)
    {
        if (reference.Identifier.ValueText != state.Local.Name || !state.IsTheLocal(reference))
        {
            return true;
        }

        // The lock target itself, and a plain member access on the local (reading or mutating the object
        // in place), keep the reference where it is. A reference captured by a lambda or a local function,
        // or handed anywhere else — an assignment, an argument, a return — publishes the instance.
        if (reference == state.LockTarget || (!IsCapturedBeyond(reference, state.Scope) && IsMemberAccessOnLocal(reference)))
        {
            return true;
        }

        state.Escaped = true;
        return false;
    }

    /// <summary>Returns whether the local is the receiver of a member access.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns><see langword="true"/> when the reference reads or calls a member of the local.</returns>
    private static bool IsMemberAccessOnLocal(IdentifierNameSyntax reference)
        => reference.Parent is MemberAccessExpressionSyntax access
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && access.Expression == reference;

    /// <summary>Returns whether a reference sits inside a lambda, an anonymous method, or a local function within the scope.</summary>
    /// <param name="reference">The reference.</param>
    /// <param name="scope">The block that bounds the walk.</param>
    /// <returns><see langword="true"/> when the value is captured and can outlive the method.</returns>
    private static bool IsCapturedBeyond(SyntaxNode reference, SyntaxNode scope)
    {
        for (var node = reference.Parent; node is not null && node != scope; node = node.Parent)
        {
            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the block a local lives in, which is as far as any reference to it can reach.</summary>
    /// <param name="declarator">The local's declarator.</param>
    /// <returns>The enclosing block, or <see langword="null"/> when the local is declared somewhere unusual.</returns>
    private static SyntaxNode? GetLocalScope(VariableDeclaratorSyntax declarator)
        => declarator.Parent?.Parent?.Parent switch
        {
            BlockSyntax block => block,
            SwitchSectionSyntax section => section,
            _ => null,
        };

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
            _ => model.GetTypeInfo(expression, cancellationToken).Type
        };

        return type is not null
               && (type.SpecialType == SpecialType.System_String
                   || (typeSymbol is not null && IsOrDerivesFrom(type, typeSymbol)));
    }

    /// <summary>Returns whether a type is, or derives from, the target type.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="target">The target base type.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is or inherits <paramref name="target"/>.</returns>
    private static bool IsOrDerivesFrom(ITypeSymbol type, INamedTypeSymbol target)
    {
        for (var current = type; current is not null; current = current.BaseType)
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
    private static bool IsObjectType(TypeSyntax type) =>
        type is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword } ||
        (type is QualifiedNameSyntax qualifiedName
            && qualifiedName.Right.Identifier.ValueText == "Object"
            && IsSystemTypeName(qualifiedName.Left));

    /// <summary>Returns whether a type name is spelled as <c>System</c> or <c>global::System</c>.</summary>
    /// <param name="name">The name syntax to inspect.</param>
    /// <returns><see langword="true"/> when the name refers to <c>System</c>.</returns>
    private static bool IsSystemTypeName(NameSyntax name) =>
        name is IdentifierNameSyntax { Identifier.ValueText: "System" }
            || (name is AliasQualifiedNameSyntax aliasQualifiedName
              && aliasQualifiedName.Alias.Identifier.ValueText == "global"
              && aliasQualifiedName.Name.Identifier.ValueText == "System");

    /// <summary>The state threaded through a fresh local's reference scan.</summary>
    /// <param name="Model">The semantic model.</param>
    /// <param name="Local">The declared local.</param>
    /// <param name="LockTarget">The lock-target reference, which is not itself an escape.</param>
    /// <param name="Scope">The block the local lives in.</param>
    /// <param name="CancellationToken">A token that cancels the operation.</param>
    private record struct FreshLocalScan(
        SemanticModel Model,
        ILocalSymbol Local,
        IdentifierNameSyntax LockTarget,
        SyntaxNode Scope,
        CancellationToken CancellationToken)
    {
        /// <summary>Gets or sets a value indicating whether the value was reassigned or published.</summary>
        public bool Escaped { get; set; }

        /// <summary>Returns whether a reference really resolves to this local.</summary>
        /// <param name="reference">The reference with a matching name.</param>
        /// <returns><see langword="true"/> when the name is not another symbol's.</returns>
        public readonly bool IsTheLocal(IdentifierNameSyntax reference)
            => SymbolEqualityComparer.Default.Equals(
                Model.GetSymbolInfo(reference, CancellationToken).Symbol,
                Local);
    }
}
