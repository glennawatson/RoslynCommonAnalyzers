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
            || type.Modifiers.Any(SyntaxKind.PartialKeyword)
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
        foreach (var identifier in type.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
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

    /// <summary>Returns whether a reference writes to a field.</summary>
    /// <param name="identifier">The field reference.</param>
    /// <returns><see langword="true"/> when the reference is a write.</returns>
    public static bool IsWrite(IdentifierNameSyntax identifier)
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
        => !candidate.IsStatic
            && candidate.DeclaredAccessibility == Accessibility.Private
            && declaration.Declaration.Variables.Count == 1
            && declaration.AttributeLists.Count == 0
            && !declaration.Modifiers.Any(SyntaxKind.VolatileKeyword);

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
        foreach (var identifier in property.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (model.GetSymbolInfo(identifier, cancellationToken).Symbol is IFieldSymbol field)
            {
                return field;
            }
        }

        return null;
    }
}
