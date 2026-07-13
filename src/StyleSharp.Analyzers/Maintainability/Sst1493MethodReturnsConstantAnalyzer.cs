// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a method whose whole body is a constant (SST1493): <c>int Limit() => 5;</c>, or a body whose only
/// statement returns a literal or a named constant.
/// </summary>
/// <remarks>
/// <para>
/// Only a parameterless, non-generic method is reported, because only that method has a value the caller could
/// have read instead of computed — the rule's advice, "expose it as a constant or a property", has to be
/// followable, and a property can take neither arguments nor type arguments.
/// </para>
/// <para>
/// A member whose shape is decided elsewhere is left alone: an override, a virtual or abstract member, an
/// interface member and an implementation of one, and a partial member. Answering a question with a constant
/// is how a derived type overrides — <c>protected override bool IsReadOnly => true;</c> is the point of the
/// mechanism, not a mistake. So is a method carrying any attribute, which may be the whole reason the member
/// exists as a method: a test case, a benchmark, a serialization or dependency-injection hook. Returning
/// <see langword="null"/> or <c>default</c> is a "nothing to give you" answer rather than a value, so it is
/// not reported either.
/// </para>
/// <para>
/// The clean path never reaches the semantic model. A method is rejected on the shape of its body — anything
/// but a lone return of a literal or a name is out — long before the constant is proven or the interfaces are
/// searched.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1493MethodReturnsConstantAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.MethodReturnsConstant);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Gets the constant a method's body consists of, if that is all the body does.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns>The returned expression, or <see langword="null"/> when the body is not a lone constant.</returns>
    /// <remarks>Shared with the code fix, which rebuilds the member around the same expression.</remarks>
    internal static ExpressionSyntax? TryGetConstantBody(MethodDeclarationSyntax method)
    {
        var value = method.ExpressionBody?.Expression;
        if (value is null)
        {
            if (method.Body is not { Statements.Count: 1 } body || body.Statements[0] is not ReturnStatementSyntax { Expression: { } returned })
            {
                return null;
            }

            value = returned;
        }

        return IsConstantShape(value) ? value : null;
    }

    /// <summary>Reports one method whose body is a single constant.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (TryGetConstantBody(method) is not { } value || !IsExposableAsValue(method))
        {
            return;
        }

        if (!context.SemanticModel.GetConstantValue(value, context.CancellationToken).HasValue
            || ImplementsInterfaceMember(method, context))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.MethodReturnsConstant,
            method.Identifier.GetLocation(),
            method.Identifier.ValueText));
    }

    /// <summary>Returns whether an expression is written in a shape a constant can take.</summary>
    /// <param name="value">The returned expression.</param>
    /// <returns><see langword="true"/> for a literal or a name that may bind to a constant.</returns>
    /// <remarks>
    /// <c>null</c> and <c>default</c> are excluded: they are how a method says it has nothing to return, not a
    /// value it was supposed to compute. A name — <c>Limit</c>, <c>Color.Red</c>, <c>int.MaxValue</c> — is only a
    /// candidate here; whether it really is a constant is settled by the semantic model afterwards.
    /// </remarks>
    private static bool IsConstantShape(ExpressionSyntax value) => value switch
    {
        LiteralExpressionSyntax literal => literal.Kind() is not (SyntaxKind.NullLiteralExpression or SyntaxKind.DefaultLiteralExpression),
        PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax } prefix =>
            prefix.Kind() is SyntaxKind.UnaryMinusExpression or SyntaxKind.UnaryPlusExpression,
        IdentifierNameSyntax or MemberAccessExpressionSyntax => true,
        _ => false,
    };

    /// <summary>Returns whether the method could be exposed as the value it returns.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns><see langword="true"/> when nothing about the declaration dictates that it stays a method.</returns>
    private static bool IsExposableAsValue(MethodDeclarationSyntax method)
        => HasValueShape(method) && !HasShapeFixingModifier(method.Modifiers);

    /// <summary>Returns whether the method's signature is one a value could take its place.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns><see langword="true"/> for a parameterless, non-generic, unattributed method that returns something.</returns>
    /// <remarks>
    /// An attribute is enough on its own to keep the method: the attribute may be the reason the member is a
    /// method at all, and this rule cannot know what reads it.
    /// </remarks>
    private static bool HasValueShape(MethodDeclarationSyntax method)
        => method.ParameterList.Parameters.Count == 0
            && method.TypeParameterList is null
            && method.AttributeLists.Count == 0
            && method.ExplicitInterfaceSpecifier is null
            && method.Parent is not InterfaceDeclarationSyntax
            && !IsVoid(method.ReturnType);

    /// <summary>Returns whether a modifier means the member's shape is decided somewhere other than here.</summary>
    /// <param name="modifiers">The method's modifiers.</param>
    /// <returns><see langword="true"/> for an override, a virtual or abstract member, and a partial or extern one.</returns>
    private static bool HasShapeFixingModifier(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].Kind() is SyntaxKind.OverrideKeyword
                or SyntaxKind.VirtualKeyword
                or SyntaxKind.AbstractKeyword
                or SyntaxKind.PartialKeyword
                or SyntaxKind.ExternKeyword)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a return type is <see langword="void"/>.</summary>
    /// <param name="returnType">The declared return type.</param>
    /// <returns><see langword="true"/> when the method returns nothing.</returns>
    private static bool IsVoid(TypeSyntax returnType)
        => returnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword };

    /// <summary>Returns whether the method implements an interface member.</summary>
    /// <param name="method">The method declaration.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when an interface dictates that the member is a method.</returns>
    /// <remarks>Runs last: only a method that already looks like a constant pays for the bind and the walk.</remarks>
    private static bool ImplementsInterfaceMember(MethodDeclarationSyntax method, SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not { ContainingType: { } containingType } symbol)
        {
            return false;
        }

        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidates = interfaces[i].GetMembers(symbol.Name);
            for (var j = 0; j < candidates.Length; j++)
            {
                if (SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(candidates[j]), symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
