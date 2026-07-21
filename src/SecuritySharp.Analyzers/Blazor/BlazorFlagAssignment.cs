// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Shared matcher for a Blazor options flag set to a specific boolean literal on a gated options type, used by
/// the SES1708 and SES1709 analyzers. It covers both a direct assignment (<c>options.Flag = true</c>) and an
/// object-initializer member (<c>new Options { Flag = true }</c>): a syntactic name-and-literal screen runs first,
/// and only a matching shape is bound to confirm the property's containing type. Keeping the two rules on one
/// matcher avoids duplicating the bind-and-compare logic.
/// </summary>
internal static class BlazorFlagAssignment
{
    /// <summary>Returns whether an assignment sets the named boolean flag on the gated type to the required literal.</summary>
    /// <param name="assignment">The assignment expression under inspection.</param>
    /// <param name="requiredValueKind">The literal the right-hand side must be (for example <see cref="SyntaxKind.TrueLiteralExpression"/>).</param>
    /// <param name="propertyName">The flag property's name.</param>
    /// <param name="declaringType">The gated type the flag must be declared on.</param>
    /// <param name="semanticModel">The semantic model used to bind the assignment target.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the assignment sets the gated flag to the required literal.</returns>
    public static bool AssignsFlag(
        AssignmentExpressionSyntax assignment,
        SyntaxKind requiredValueKind,
        string propertyName,
        INamedTypeSymbol declaringType,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        // Syntactic prefilter: '<expr>.Flag = <literal>' or the initializer member 'Flag = <literal>'.
        if (!assignment.Right.IsKind(requiredValueKind) || !TargetsProperty(assignment.Left, propertyName))
        {
            return false;
        }

        return semanticModel.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is IPropertySymbol property
            && string.Equals(property.Name, propertyName, StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(property.ContainingType, declaringType);
    }

    /// <summary>Returns whether an assignment target syntactically names the flag property.</summary>
    /// <param name="left">The assignment's left-hand expression.</param>
    /// <param name="propertyName">The flag property's name.</param>
    /// <returns><see langword="true"/> for a member access or bare initializer member naming the flag.</returns>
    private static bool TargetsProperty(ExpressionSyntax left, string propertyName)
        => left switch
        {
            // 'options.Flag = <literal>'.
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name } => string.Equals(name, propertyName, StringComparison.Ordinal),

            // 'new Options { Flag = <literal> }' (object-initializer member).
            IdentifierNameSyntax { Identifier.ValueText: var name } => string.Equals(name, propertyName, StringComparison.Ordinal),

            _ => false,
        };
}
