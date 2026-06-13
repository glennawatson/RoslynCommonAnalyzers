// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Decides whether an <c>override</c> member does nothing but forward to the same base member with the
/// parameters passed straight through. The whole check is syntactic so the common (non-redundant) case
/// never binds a symbol or allocates.
/// </summary>
internal static class OverrideForwardingAnalysis
{
    /// <summary>Returns whether a method override only forwards to <c>base.Name(args)</c> with its parameters in order.</summary>
    /// <param name="method">The method declaration to inspect.</param>
    /// <returns><see langword="true"/> when the override adds nothing over the inherited member.</returns>
    public static bool IsPlainForwardingMethod(MethodDeclarationSyntax method)
    {
        if (!IsForwardingCandidate(method.Modifiers, method.AttributeLists)
            || method.TypeParameterList is not null)
        {
            return false;
        }

        var call = ExtractForwardedCall(method.ExpressionBody, method.Body);
        return call is not null
            && IsBaseAccessTo(call.Expression, method.Identifier.ValueText)
            && CallForwardsParameters(call.ArgumentList, method.ParameterList);
    }

    /// <summary>Returns whether a property override only forwards each accessor to the base property of the same name.</summary>
    /// <param name="property">The property declaration to inspect.</param>
    /// <returns><see langword="true"/> when the override adds nothing over the inherited member.</returns>
    public static bool IsPlainForwardingProperty(PropertyDeclarationSyntax property)
    {
        if (!IsForwardingCandidate(property.Modifiers, property.AttributeLists))
        {
            return false;
        }

        var name = property.Identifier.ValueText;

        // An expression-bodied property is a read-only getter: '=> base.Name'.
        if (property.ExpressionBody is { } arrow)
        {
            return IsBaseAccessTo(arrow.Expression, name);
        }

        if (property.AccessorList is not { } accessors || accessors.Accessors.Count == 0)
        {
            return false;
        }

        var list = accessors.Accessors;
        for (var i = 0; i < list.Count; i++)
        {
            if (!AccessorOnlyForwards(list[i], name))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a member is an unsealed, attribute-free <c>override</c> worth checking.</summary>
    /// <param name="modifiers">The member modifiers.</param>
    /// <param name="attributeLists">The member attribute lists.</param>
    /// <returns><see langword="true"/> when the member could be a pure forwarder.</returns>
    private static bool IsForwardingCandidate(SyntaxTokenList modifiers, SyntaxList<AttributeListSyntax> attributeLists)
        => attributeLists.Count == 0
            && ModifierListHelper.Contains(modifiers, SyntaxKind.OverrideKeyword)

            // 'sealed override' intentionally stops further overriding, so it carries meaning.
            && !ModifierListHelper.Contains(modifiers, SyntaxKind.SealedKeyword);

    /// <summary>Returns the single invocation an accessor or body forwards to, or <see langword="null"/>.</summary>
    /// <param name="expressionBody">The member's arrow body, if any.</param>
    /// <param name="body">The member's block body, if any.</param>
    /// <returns>The forwarded invocation, or <see langword="null"/> when the body is anything else.</returns>
    private static InvocationExpressionSyntax? ExtractForwardedCall(ArrowExpressionClauseSyntax? expressionBody, BlockSyntax? body)
    {
        if (expressionBody is not null)
        {
            return expressionBody.Expression as InvocationExpressionSyntax;
        }

        if (body is not { Statements.Count: 1 })
        {
            return null;
        }

        return body.Statements[0] switch
        {
            ReturnStatementSyntax { Expression: InvocationExpressionSyntax returned } => returned,
            ExpressionStatementSyntax { Expression: InvocationExpressionSyntax called } => called,
            _ => null
        };
    }

    /// <summary>Returns whether a single accessor only forwards to the matching base property accessor.</summary>
    /// <param name="accessor">The accessor declaration.</param>
    /// <param name="propertyName">The owning property's name.</param>
    /// <returns><see langword="true"/> when the accessor adds nothing over the base accessor.</returns>
    private static bool AccessorOnlyForwards(AccessorDeclarationSyntax accessor, string propertyName)
    {
        var inner = accessor.ExpressionBody?.Expression ?? SingleBodyExpression(accessor.Body);
        if (inner is null)
        {
            return false;
        }

        return accessor.Kind() switch
        {
            SyntaxKind.GetAccessorDeclaration => IsBaseAccessTo(inner, propertyName),
            SyntaxKind.SetAccessorDeclaration or SyntaxKind.InitAccessorDeclaration => IsBaseSetterForward(inner, propertyName),
            _ => false
        };
    }

    /// <summary>Returns the lone expression of a single-statement accessor body, or <see langword="null"/>.</summary>
    /// <param name="body">The accessor block body.</param>
    /// <returns>The single expression (returned or statement form), or <see langword="null"/>.</returns>
    private static ExpressionSyntax? SingleBodyExpression(BlockSyntax? body)
    {
        if (body is not { Statements.Count: 1 })
        {
            return null;
        }

        return body.Statements[0] switch
        {
            ReturnStatementSyntax { Expression: { } returned } => returned,
            ExpressionStatementSyntax { Expression: { } called } => called,
            _ => null
        };
    }

    /// <summary>Returns whether a setter body is exactly <c>base.Name = value</c>.</summary>
    /// <param name="expression">The setter's single expression.</param>
    /// <param name="propertyName">The owning property's name.</param>
    /// <returns><see langword="true"/> when the setter only forwards the assigned value to the base.</returns>
    private static bool IsBaseSetterForward(ExpressionSyntax expression, string propertyName)
        => expression is AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment
            && assignment.Right is IdentifierNameSyntax { Identifier.ValueText: "value" }
            && IsBaseAccessTo(assignment.Left, propertyName);

    /// <summary>Returns whether an expression is <c>base.member</c> naming the given member.</summary>
    /// <param name="expression">The candidate member access.</param>
    /// <param name="memberName">The expected member name.</param>
    /// <returns><see langword="true"/> for a <c>base.member</c> access to the named member.</returns>
    private static bool IsBaseAccessTo(ExpressionSyntax expression, string memberName)
        => expression is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } access
            && access.Name is IdentifierNameSyntax identifier
            && string.Equals(identifier.Identifier.ValueText, memberName, StringComparison.Ordinal);

    /// <summary>Returns whether a call passes every parameter straight through in declaration order.</summary>
    /// <param name="arguments">The forwarded call's argument list.</param>
    /// <param name="parameters">The override's parameter list.</param>
    /// <returns><see langword="true"/> when the arguments are the parameters, in order, with matching ref kinds.</returns>
    private static bool CallForwardsParameters(ArgumentListSyntax arguments, ParameterListSyntax parameters)
    {
        var args = arguments.Arguments;
        var pars = parameters.Parameters;
        if (args.Count != pars.Count)
        {
            return false;
        }

        for (var i = 0; i < args.Count; i++)
        {
            if (!ArgumentIsParameter(args[i], pars[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether one argument is its matching parameter with a matching ref kind.</summary>
    /// <param name="argument">The forwarded argument.</param>
    /// <param name="parameter">The matching parameter.</param>
    /// <returns><see langword="true"/> when the argument re-passes the parameter unchanged.</returns>
    private static bool ArgumentIsParameter(ArgumentSyntax argument, ParameterSyntax parameter)
    {
        if (argument.Expression is not IdentifierNameSyntax identifier
            || !string.Equals(identifier.Identifier.ValueText, parameter.Identifier.ValueText, StringComparison.Ordinal))
        {
            return false;
        }

        // 'ref'/'out'/'in' must appear on both sides or neither; 'params' needs no call-site keyword.
        return RefKind(argument.RefKindKeyword) == ParameterPassingKind(parameter.Modifiers);
    }

    /// <summary>Maps an argument's ref keyword to a comparable passing kind.</summary>
    /// <param name="keyword">The argument's ref-kind keyword (may be none).</param>
    /// <returns>The token kind, or <see cref="SyntaxKind.None"/>.</returns>
    private static SyntaxKind RefKind(SyntaxToken keyword) => keyword.Kind() switch
    {
        SyntaxKind.RefKeyword => SyntaxKind.RefKeyword,
        SyntaxKind.OutKeyword => SyntaxKind.OutKeyword,
        SyntaxKind.InKeyword => SyntaxKind.InKeyword,
        _ => SyntaxKind.None
    };

    /// <summary>Maps a parameter's modifiers to the call-site ref kind it requires.</summary>
    /// <param name="modifiers">The parameter modifiers.</param>
    /// <returns>The required call-site token kind, or <see cref="SyntaxKind.None"/>.</returns>
    private static SyntaxKind ParameterPassingKind(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            var kind = modifiers[i].Kind();
            if (kind is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword or SyntaxKind.InKeyword)
            {
                return kind;
            }
        }

        return SyntaxKind.None;
    }
}
