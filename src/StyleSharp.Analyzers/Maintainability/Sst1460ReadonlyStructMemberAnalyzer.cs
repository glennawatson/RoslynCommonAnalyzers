// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Finds struct members that are cheap to prove non-mutating and can therefore be marked
/// <c>readonly</c>. The analyzer deliberately under-reports: calls, assignments, ref/out
/// arguments, increment/decrement operations, and writable <c>ref</c> returns from a member whose
/// <c>this</c> is unscoped are all treated as reasons to stay quiet. That keeps the rule correct without
/// expensive interprocedural analysis and keeps the no-diagnostic path a short syntax scan.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1460ReadonlyStructMemberAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(MaintainabilityRules.MakeStructMemberReadonly);

    /// <summary>A standalone <c>this</c> expression, bound speculatively inside a member to read its receiver's scope.</summary>
    private static readonly ThisExpressionSyntax ThisExpression = SyntaxFactory.ThisExpression();

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Reports a non-mutating struct method.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp8 })
        {
            return;
        }

        var method = (MethodDeclarationSyntax)context.Node;
        if (!IsStructInstanceMember(method.Modifiers, method.Parent)
            || (method.Body is null && method.ExpressionBody is null))
        {
            return;
        }

        var body = method.Body ?? (SyntaxNode)method.ExpressionBody!;
        if (HasRiskyOperation(body)
            || (ReturnsWritableRef(method.ReturnType) && !HasScopedThis(context.SemanticModel, body.SpanStart, context.CancellationToken)))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.MakeStructMemberReadonly,
            method.Identifier.GetLocation(),
            method.Identifier.ValueText));
    }

    /// <summary>Reports a get-only non-mutating struct property.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp8 })
        {
            return;
        }

        var property = (PropertyDeclarationSyntax)context.Node;
        if (!IsStructInstanceMember(property.Modifiers, property.Parent)
            || HasSetter(property))
        {
            return;
        }

        var body = property.ExpressionBody as SyntaxNode ?? property.AccessorList;
        if (body is null
            || HasRiskyOperation(body)
            || (ReturnsWritableRef(property.Type) && !HasScopedThis(context.SemanticModel, GetBodyPosition(body), context.CancellationToken)))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.MakeStructMemberReadonly,
            property.Identifier.GetLocation(),
            property.Identifier.ValueText));
    }

    /// <summary>Returns whether a declaration is a non-readonly instance member of a non-readonly struct.</summary>
    /// <param name="modifiers">The member modifiers.</param>
    /// <param name="parent">The member parent.</param>
    /// <returns><see langword="true"/> when the member is eligible.</returns>
    private static bool IsStructInstanceMember(in SyntaxTokenList modifiers, SyntaxNode? parent) =>
        parent is StructDeclarationSyntax { Modifiers: var structModifiers }
            && !ModifierListHelper.Contains(structModifiers, SyntaxKind.ReadOnlyKeyword)
            && !ModifierListHelper.Contains(modifiers, SyntaxKind.ReadOnlyKeyword)
            && !ModifierListHelper.Contains(modifiers, SyntaxKind.StaticKeyword)
            && !ModifierListHelper.Contains(modifiers, SyntaxKind.AbstractKeyword)
            && !ModifierListHelper.Contains(modifiers, SyntaxKind.ExternKeyword)
            && !ModifierListHelper.Contains(modifiers, SyntaxKind.PartialKeyword);

    /// <summary>Returns whether a member hands out a writable reference.</summary>
    /// <param name="type">The declared return or property type.</param>
    /// <returns><see langword="true"/> for a <c>ref</c> return that is not <c>ref readonly</c>.</returns>
    private static bool ReturnsWritableRef(TypeSyntax type) =>
        type is RefTypeSyntax { ReadOnlyKeyword.RawKind: 0 };

    /// <summary>Returns whether the compiler treats <c>this</c> inside a member as a scoped reference.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">A position inside the member body.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when <c>this</c> binds with <see cref="ScopedKind.ScopedRef"/>.</returns>
    /// <remarks>
    /// A writable <c>ref</c> can only point into the struct's own storage when <c>this</c> is unscoped, which the
    /// <c>[UnscopedRef]</c> attribute grants. Adding <c>readonly</c> to such a member stops it compiling. When
    /// <c>this</c> is scoped, the returned reference already points elsewhere and <c>readonly</c> is safe. A binding
    /// that does not yield the receiver is treated as unscoped, so the member stays unreported.
    /// </remarks>
    private static bool HasScopedThis(SemanticModel model, int position, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return model.GetSpeculativeSymbolInfo(position, ThisExpression, SpeculativeBindingOption.BindAsExpression).Symbol
            is IParameterSymbol { IsThis: true, ScopedKind: ScopedKind.ScopedRef };
    }

    /// <summary>Returns a position inside a property body where <c>this</c> binds to the property's receiver.</summary>
    /// <param name="body">The property's expression body or accessor list.</param>
    /// <returns>The start of the arrow expression, or of the first accessor's body.</returns>
    private static int GetBodyPosition(SyntaxNode body) => body switch
    {
        ArrowExpressionClauseSyntax arrow => arrow.Expression.SpanStart,
        AccessorListSyntax { Accessors: [{ Body: { } block }, ..] } => block.SpanStart + 1,
        AccessorListSyntax { Accessors: [{ ExpressionBody: { } arrow }, ..] } => arrow.Expression.SpanStart,
        _ => body.SpanStart,
    };

    /// <summary>Returns whether a property declares a setter or init accessor.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns><see langword="true"/> when the property can mutate state directly.</returns>
    private static bool HasSetter(PropertyDeclarationSyntax property)
    {
        if (property.AccessorList is null)
        {
            return false;
        }

        var accessors = property.AccessorList.Accessors;
        for (var i = 0; i < accessors.Count; i++)
        {
            if (accessors[i].Kind() is SyntaxKind.SetAccessorDeclaration or SyntaxKind.InitAccessorDeclaration)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a member body contains an operation that could mutate the receiver.</summary>
    /// <param name="node">The member body or expression body.</param>
    /// <returns><see langword="true"/> when the member is not cheap to prove readonly-safe.</returns>
    private static bool HasRiskyOperation(SyntaxNode node)
    {
        var found = false;
        _ = DescendantTraversalHelper.VisitDescendants(
            node,
            ref found,
            static (SyntaxNode descendant, ref bool state) =>
            {
                if (!IsRiskyKind(descendant.Kind())
                    && descendant is not ArgumentSyntax { RefOrOutKeyword.RawKind: not 0 })
                {
                    return true;
                }

                state = true;
                return false;
            });

        return found;
    }

    /// <summary>Returns whether a syntax kind can mutate state or call code that mutates state.</summary>
    /// <param name="kind">The syntax kind.</param>
    /// <returns><see langword="true"/> for risky operations.</returns>
    private static bool IsRiskyKind(SyntaxKind kind) =>
        IsAssignmentKind(kind) || IsIncrementKind(kind) || kind == SyntaxKind.InvocationExpression;

    /// <summary>Returns whether a syntax kind is an assignment expression.</summary>
    /// <param name="kind">The syntax kind.</param>
    /// <returns><see langword="true"/> for assignment expressions.</returns>
    private static bool IsAssignmentKind(SyntaxKind kind) =>
        IsBasicAssignmentKind(kind) || IsCompoundAssignmentKind(kind);

    /// <summary>Returns whether a syntax kind is a simple or null-coalescing assignment expression.</summary>
    /// <param name="kind">The syntax kind.</param>
    /// <returns><see langword="true"/> for simple assignment-like expressions.</returns>
    private static bool IsBasicAssignmentKind(SyntaxKind kind) =>
        kind is
            SyntaxKind.SimpleAssignmentExpression or
            SyntaxKind.CoalesceAssignmentExpression;

    /// <summary>Returns whether a syntax kind is a compound assignment expression.</summary>
    /// <param name="kind">The syntax kind.</param>
    /// <returns><see langword="true"/> for compound assignment expressions.</returns>
    private static bool IsCompoundAssignmentKind(SyntaxKind kind) =>
        kind is
            SyntaxKind.AddAssignmentExpression or
            SyntaxKind.SubtractAssignmentExpression or
            SyntaxKind.MultiplyAssignmentExpression or
            SyntaxKind.DivideAssignmentExpression or
            SyntaxKind.ModuloAssignmentExpression or
            SyntaxKind.AndAssignmentExpression or
            SyntaxKind.ExclusiveOrAssignmentExpression or
            SyntaxKind.OrAssignmentExpression or
            SyntaxKind.LeftShiftAssignmentExpression or
            SyntaxKind.RightShiftAssignmentExpression;

    /// <summary>Returns whether a syntax kind is an increment or decrement expression.</summary>
    /// <param name="kind">The syntax kind.</param>
    /// <returns><see langword="true"/> for increment or decrement expressions.</returns>
    private static bool IsIncrementKind(SyntaxKind kind) =>
        kind is
            SyntaxKind.PreIncrementExpression or
            SyntaxKind.PreDecrementExpression or
            SyntaxKind.PostIncrementExpression or
            SyntaxKind.PostDecrementExpression;
}
