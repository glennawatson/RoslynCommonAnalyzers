// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads the type-parameter list and <c>where</c> constraint clauses off any generic declaration —
/// type, method, delegate, or local function — without repeating the per-kind cast at every call site.
/// Shared by the constraint-ordering rule (SST1221) and its code fix.
/// </summary>
internal static class GenericConstraintLayout
{
    /// <summary>Reads the type-parameter list and constraint clauses of a generic declaration.</summary>
    /// <param name="node">The candidate declaration.</param>
    /// <param name="typeParameters">The type-parameter list, or <see langword="null"/> when the node is not generic.</param>
    /// <param name="constraintClauses">The declaration's constraint clauses.</param>
    /// <returns><see langword="true"/> when the node is a declaration that can carry constraint clauses.</returns>
    public static bool TryGet(
        SyntaxNode node,
        out TypeParameterListSyntax? typeParameters,
        out SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses)
    {
        switch (node)
        {
            case TypeDeclarationSyntax type:
            {
                typeParameters = type.TypeParameterList;
                constraintClauses = type.ConstraintClauses;
                return true;
            }

            case MethodDeclarationSyntax method:
            {
                typeParameters = method.TypeParameterList;
                constraintClauses = method.ConstraintClauses;
                return true;
            }

            case DelegateDeclarationSyntax @delegate:
            {
                typeParameters = @delegate.TypeParameterList;
                constraintClauses = @delegate.ConstraintClauses;
                return true;
            }

            case LocalFunctionStatementSyntax localFunction:
            {
                typeParameters = localFunction.TypeParameterList;
                constraintClauses = localFunction.ConstraintClauses;
                return true;
            }

            default:
            {
                typeParameters = null;
                constraintClauses = default;
                return false;
            }
        }
    }

    /// <summary>Returns the declaration position of a type parameter by name, or <c>-1</c> when absent.</summary>
    /// <param name="typeParameters">The type-parameter list.</param>
    /// <param name="name">The type parameter name a constraint clause targets.</param>
    /// <returns>The zero-based position, or <c>-1</c>.</returns>
    public static int PositionOf(TypeParameterListSyntax typeParameters, string name)
    {
        var parameters = typeParameters.Parameters;
        for (var i = 0; i < parameters.Count; i++)
        {
            if (string.Equals(parameters[i].Identifier.ValueText, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns the declaration with its constraint clauses replaced.</summary>
    /// <param name="node">The generic declaration.</param>
    /// <param name="constraintClauses">The reordered constraint clauses.</param>
    /// <returns>The updated declaration, or the node unchanged when its kind is not generic.</returns>
    public static SyntaxNode WithConstraintClauses(SyntaxNode node, SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses)
        => node switch
        {
            TypeDeclarationSyntax type => type.WithConstraintClauses(constraintClauses),
            MethodDeclarationSyntax method => method.WithConstraintClauses(constraintClauses),
            DelegateDeclarationSyntax @delegate => @delegate.WithConstraintClauses(constraintClauses),
            LocalFunctionStatementSyntax localFunction => localFunction.WithConstraintClauses(constraintClauses),
            _ => node,
        };
}
