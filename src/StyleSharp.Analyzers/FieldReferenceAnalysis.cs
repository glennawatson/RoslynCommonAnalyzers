// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Shared conservative semantic checks for private-field simplification rules.</summary>
internal static class FieldReferenceAnalysis
{
    /// <summary>Finds a private single-variable backing field referenced by a property and nowhere else.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="property">The property declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="field">The backing-field declaration.</param>
    /// <param name="variable">The backing-field variable.</param>
    /// <param name="symbol">The backing-field symbol.</param>
    /// <returns><see langword="true"/> when a suitable single-use field is found.</returns>
    public static bool TryFindSingleUseBackingField(
        SemanticModel model,
        PropertyDeclarationSyntax property,
        CancellationToken cancellationToken,
        out FieldDeclarationSyntax? field,
        out VariableDeclaratorSyntax? variable,
        out IFieldSymbol? symbol)
    {
        field = null;
        variable = null;
        symbol = null;
        if (property.Parent is not TypeDeclarationSyntax type
            || ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword)
            || property.AccessorList is null)
        {
            return false;
        }

        if (FindReferencedField(model, property, cancellationToken) is not { } candidate
            || !TryGetDeclaration(candidate, cancellationToken, out var declaration, out var declarator)
            || !IsEligible(candidate, declaration!)
            || !OnlyReferencedInside(model, type, candidate, property, cancellationToken))
        {
            return false;
        }

        field = declaration;
        variable = declarator;
        symbol = candidate;
        return true;
    }

    /// <summary>Returns whether every bound reference to a field lies inside one allowed node.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The containing type.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="allowed">The node allowed to contain references.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when all references are inside the allowed node.</returns>
    public static bool OnlyReferencedInside(
        SemanticModel model,
        TypeDeclarationSyntax type,
        IFieldSymbol field,
        SyntaxNode allowed,
        CancellationToken cancellationToken)
    {
        var found = false;
        foreach (var node in type.DescendantNodes())
        {
            if (node is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            if (identifier.Identifier.ValueText != field.Name
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, field))
            {
                continue;
            }

            if (!allowed.FullSpan.Contains(identifier.Span))
            {
                return false;
            }

            found = true;
        }

        return found;
    }

    /// <summary>Returns whether an expression is syntactically known to reference a private object field declared in the same type.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="expression">The already-unwrapped expression to inspect.</param>
    /// <returns><see langword="true"/> when the expression safely names a private object field.</returns>
    internal static bool IsPrivateObjectFieldLockTarget(TypeDeclarationSyntax type, ExpressionSyntax expression)
    {
        if (expression is not IdentifierNameSyntax identifier || IsShadowed(identifier))
        {
            return false;
        }

        return TryFindPrivateObjectField(type, identifier.Identifier.ValueText);
    }

    /// <summary>Returns whether a reference writes to a field.</summary>
    /// <param name="identifier">The field reference.</param>
    /// <returns><see langword="true"/> when the reference is a write.</returns>
    internal static bool IsWrite(IdentifierNameSyntax identifier)
    {
        SyntaxNode expression = identifier;
        if (identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier)
        {
            expression = access;
        }

        if (expression.Parent is AssignmentExpressionSyntax assignment && assignment.Left == expression)
        {
            return true;
        }

        if (expression.Parent is PrefixUnaryExpressionSyntax prefix)
        {
            return prefix.IsKind(SyntaxKind.PreIncrementExpression)
                || prefix.IsKind(SyntaxKind.PreDecrementExpression);
        }

        return expression.Parent is PostfixUnaryExpressionSyntax
            or ArgumentSyntax { RefOrOutKeyword.RawKind: not 0 };
    }

    /// <summary>Returns whether a field and declaration meet the shared eligibility requirements.</summary>
    /// <param name="candidate">The field symbol.</param>
    /// <param name="declaration">The field declaration.</param>
    /// <returns><see langword="true"/> when the field is eligible.</returns>
    private static bool IsEligible(IFieldSymbol candidate, FieldDeclarationSyntax declaration)
    {
        return !candidate.IsStatic
            && candidate.DeclaredAccessibility == Accessibility.Private
            && declaration.Declaration.Variables.Count == 1
            && declaration.AttributeLists.Count == 0
            && !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.VolatileKeyword);
    }

    /// <summary>Gets the single source declaration for a field symbol.</summary>
    /// <param name="candidate">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="declaration">The field declaration.</param>
    /// <param name="declarator">The variable declarator.</param>
    /// <returns><see langword="true"/> when a single declaration is available.</returns>
    private static bool TryGetDeclaration(
        IFieldSymbol candidate,
        CancellationToken cancellationToken,
        out FieldDeclarationSyntax? declaration,
        out VariableDeclaratorSyntax? declarator)
    {
        declaration = null;
        declarator = null;
        if (candidate.DeclaringSyntaxReferences is not [var syntaxReference]
            || syntaxReference.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax variable
            || variable.Parent?.Parent is not FieldDeclarationSyntax field)
        {
            return false;
        }

        declaration = field;
        declarator = variable;
        return true;
    }

    /// <summary>Finds the first field symbol referenced by a property.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="property">The property.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The field symbol, or <see langword="null"/>.</returns>
    private static IFieldSymbol? FindReferencedField(
        SemanticModel model,
        PropertyDeclarationSyntax property,
        CancellationToken cancellationToken)
    {
        foreach (var node in property.DescendantNodes())
        {
            if (node is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            if (model.GetSymbolInfo(identifier, cancellationToken).Symbol is IFieldSymbol field)
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>Returns whether the type contains a matching private object field.</summary>
    /// <param name="type">The containing type declaration.</param>
    /// <param name="name">The expected field name.</param>
    /// <returns><see langword="true"/> when the field exists.</returns>
    private static bool TryFindPrivateObjectField(TypeDeclarationSyntax type, string name)
    {
        for (var i = 0; i < type.Members.Count; i++)
        {
            if (type.Members[i] is FieldDeclarationSyntax field && IsPrivateObjectField(field, name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a field declaration defines the named private object field.</summary>
    /// <param name="field">The field declaration.</param>
    /// <param name="name">The expected field name.</param>
    /// <returns><see langword="true"/> when the field matches the private object pattern.</returns>
    private static bool IsPrivateObjectField(FieldDeclarationSyntax field, string name)
    {
        if (!IsUnambiguousObjectType(field.Declaration.Type) || !HasPrivateModifier(field.Modifiers))
        {
            return false;
        }

        var variables = field.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            if (variables[i].Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the identifier is shadowed by a parameter or earlier local declaration in the current callable scope.</summary>
    /// <param name="identifier">The identifier to inspect.</param>
    /// <returns><see langword="true"/> when syntax alone cannot safely bind the field.</returns>
    private static bool IsShadowed(IdentifierNameSyntax identifier)
    {
        var name = identifier.Identifier.ValueText;
        for (var current = identifier.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case BaseMethodDeclarationSyntax method when HasParameterNamed(method.ParameterList.Parameters, name):
                case LocalFunctionStatementSyntax localFunction when HasParameterNamed(localFunction.ParameterList.Parameters, name):
                case SimpleLambdaExpressionSyntax lambda when lambda.Parameter.Identifier.ValueText == name:
                case ParenthesizedLambdaExpressionSyntax parenthesizedLambda when HasParameterNamed(parenthesizedLambda.ParameterList.Parameters, name):
                case AnonymousMethodExpressionSyntax anonymousMethod when anonymousMethod.ParameterList is not null && HasParameterNamed(anonymousMethod.ParameterList.Parameters, name):
                    return true;

                case AccessorDeclarationSyntax accessor:
                    return HasEarlierLocalNamed(accessor, identifier.SpanStart, name);

                case BaseMethodDeclarationSyntax method:
                    return HasEarlierLocalNamed(method, identifier.SpanStart, name);

                case LocalFunctionStatementSyntax localFunction:
                    return HasEarlierLocalNamed(localFunction, identifier.SpanStart, name);

                case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                    return HasEarlierLocalNamed(parenthesizedLambda, identifier.SpanStart, name);

                case SimpleLambdaExpressionSyntax simpleLambda:
                    return HasEarlierLocalNamed(simpleLambda, identifier.SpanStart, name);

                case AnonymousMethodExpressionSyntax anonymousMethod:
                    return HasEarlierLocalNamed(anonymousMethod, identifier.SpanStart, name);
            }
        }

        return false;
    }

    /// <summary>Returns whether a parameter list contains the specified name.</summary>
    /// <param name="parameters">The parameters to inspect.</param>
    /// <param name="name">The expected parameter name.</param>
    /// <returns><see langword="true"/> when a parameter matches.</returns>
    private static bool HasParameterNamed(SeparatedSyntaxList<ParameterSyntax> parameters, string name)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a callable scope declares a matching local before the field reference.</summary>
    /// <param name="scope">The scope to inspect.</param>
    /// <param name="position">The reference position.</param>
    /// <param name="name">The identifier name.</param>
    /// <returns><see langword="true"/> when an earlier local declaration shadows the field.</returns>
    private static bool HasEarlierLocalNamed(SyntaxNode scope, int position, string name)
    {
        foreach (var node in scope.DescendantNodes())
        {
            if (node.SpanStart >= position)
            {
                continue;
            }

            switch (node)
            {
                case VariableDeclaratorSyntax variable when variable.Identifier.ValueText == name:
                case SingleVariableDesignationSyntax designation when designation.Identifier.ValueText == name:
                case ForEachStatementSyntax foreachStatement when foreachStatement.Identifier.ValueText == name:
                case CatchDeclarationSyntax catchDeclaration when catchDeclaration.Identifier.ValueText == name:
                    return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type syntax unambiguously denotes <c>System.Object</c> without semantic binding.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns><see langword="true"/> for unambiguous object spellings.</returns>
    private static bool IsUnambiguousObjectType(TypeSyntax type)
        => (type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword))
            || (type is QualifiedNameSyntax { Right.Identifier.ValueText: "Object", Left: var left } && IsSystemNamespace(left));

    /// <summary>Returns whether a modifier list contains <c>private</c>.</summary>
    /// <param name="modifiers">The modifier list to inspect.</param>
    /// <returns><see langword="true"/> when the field is private.</returns>
    private static bool HasPrivateModifier(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.PrivateKeyword))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a name syntax denotes the <c>System</c> namespace.</summary>
    /// <param name="name">The syntax to inspect.</param>
    /// <returns><see langword="true"/> when the syntax denotes <c>System</c>.</returns>
    private static bool IsSystemNamespace(NameSyntax name)
        => name is IdentifierNameSyntax { Identifier.ValueText: "System" }
            or AliasQualifiedNameSyntax { Alias.Identifier.ValueText: "global", Name.Identifier.ValueText: "System" };
}
