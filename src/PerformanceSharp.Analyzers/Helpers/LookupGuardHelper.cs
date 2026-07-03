// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Shared syntax and symbol checks for the redundant-lookup rules (PSH1104, PSH1105).
/// A membership guard and the lookup it protects can only be merged when the duplicated
/// receiver and key expressions are simple enough to be side-effect free, and when the
/// receiver's type actually exposes the combined API being suggested.
/// </summary>
internal static class LookupGuardHelper
{
    /// <summary>The parameter count shared by the probed <c>TryGetValue</c> and <c>TryAdd</c> shapes.</summary>
    private const int ProbedMethodParameterCount = 2;

    /// <summary>Returns whether an expression is a side-effect-free receiver shape.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> for an identifier, <c>this</c>, or a plain member-access chain of identifiers.</returns>
    public static bool IsSimpleReceiver(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax => true,
        ThisExpressionSyntax => true,
        MemberAccessExpressionSyntax memberAccess when memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && memberAccess.Name is IdentifierNameSyntax => IsSimpleReceiver(memberAccess.Expression),
        _ => false
    };

    /// <summary>Returns whether an expression is a side-effect-free key shape.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> for a literal or any simple receiver shape.</returns>
    public static bool IsSimpleKey(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax || IsSimpleReceiver(expression);

    /// <summary>Returns whether an argument is passed by value without a name colon.</summary>
    /// <param name="argument">The argument to inspect.</param>
    /// <returns><see langword="true"/> when the argument can be moved to another call site verbatim.</returns>
    public static bool IsPlainArgument(ArgumentSyntax argument)
        => argument.NameColon is null && argument.RefKindKeyword.IsKind(SyntaxKind.None);

    /// <summary>Returns whether a type exposes an accessible bool-returning two-parameter instance method.</summary>
    /// <param name="type">The receiver's static type; its base types and implemented interfaces are also probed.</param>
    /// <param name="name">The method name to probe (for example <c>TryGetValue</c> or <c>TryAdd</c>).</param>
    /// <param name="secondParameterIsOut">Whether the second parameter must be <c>out</c> (true for <c>TryGetValue</c>, false for <c>TryAdd</c>).</param>
    /// <param name="model">The semantic model used for the accessibility check.</param>
    /// <param name="position">The source position at which the method must be accessible.</param>
    /// <returns><see langword="true"/> when a matching accessible method exists.</returns>
    public static bool TypeExposesAccessibleMethod(ITypeSymbol type, string name, bool secondParameterIsOut, SemanticModel model, int position)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (HasMatchingMethod(current, name, secondParameterIsOut, model, position))
            {
                return true;
            }
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (HasMatchingMethod(interfaces[i], name, secondParameterIsOut, model, position))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether one type declares a matching accessible bool-returning two-parameter instance method.</summary>
    /// <param name="owner">The type whose direct members to probe.</param>
    /// <param name="name">The method name to probe.</param>
    /// <param name="secondParameterIsOut">Whether the second parameter must be <c>out</c>.</param>
    /// <param name="model">The semantic model used for the accessibility check.</param>
    /// <param name="position">The source position at which the method must be accessible.</param>
    /// <returns><see langword="true"/> when a matching accessible method is declared on <paramref name="owner"/>.</returns>
    private static bool HasMatchingMethod(ITypeSymbol owner, string name, bool secondParameterIsOut, SemanticModel model, int position)
    {
        var members = owner.GetMembers(name);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: false, ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: ProbedMethodParameterCount } method
                && method.Parameters[1].RefKind == (secondParameterIsOut ? RefKind.Out : RefKind.None)
                && model.IsAccessible(position, method))
            {
                return true;
            }
        }

        return false;
    }
}
