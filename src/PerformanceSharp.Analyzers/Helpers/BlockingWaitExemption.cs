// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Decides whether the member a blocking wait sits in is one its author could not change anyway.
/// Consulted only for a synchronous enclosing function — where the function is already
/// <c>async</c>, the whole fix is an <c>await</c> and no signature moves, so nothing here applies.
/// <para>
/// Three members are exempt. An <b>awaiter's own <c>GetResult</c></b>: a type that implements
/// <c>INotifyCompletion</c> and offers <c>IsCompleted</c> is an awaiter, and the awaiter pattern
/// requires <c>GetResult</c> to be synchronous — the compiler only calls it once <c>IsCompleted</c>
/// is true, so blocking there is the contract rather than a defect, and neither <c>IsCompleted</c>
/// nor <c>GetResult</c> is an interface member that the rule below would have caught. The
/// <b>entry point</b>: <c>Main</c> is the outermost frame, nothing is waiting behind it for the
/// thread it parks, and bridging sync to async is exactly what it is for. And any member that
/// <b>overrides or implements someone else's signature</b> — an override, an explicit interface
/// implementation, or an implicit one — because turning it async would mean changing a base type
/// or an interface the author may not own. That last rule is what quietly covers
/// <c>IDisposable.Dispose</c>: a type that must drain an async resource from a synchronous
/// <c>Dispose</c> cannot be told to await, and where it can offer <c>IAsyncDisposable</c> instead,
/// PSH1310 already says so at the call site.
/// </para>
/// </summary>
internal static class BlockingWaitExemption
{
    /// <summary>The completion check an awaiter must expose alongside <c>GetResult</c>.</summary>
    private const string IsCompletedPropertyName = "IsCompleted";

    /// <summary>Returns whether the member enclosing a blocking wait could not be made to await it.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="blocking">The blocking expression.</param>
    /// <param name="notifyCompletion">The awaiter marker interface, when the framework has one.</param>
    /// <param name="entryPoint">The compilation's entry point, when it has one.</param>
    /// <returns><see langword="true"/> when the wait must not be reported.</returns>
    public static bool IsExempt(
        SyntaxNodeAnalysisContext context,
        SyntaxNode blocking,
        INamedTypeSymbol? notifyCompletion,
        IMethodSymbol? entryPoint)
    {
        if (FindEnclosingMember(blocking) is not { } member)
        {
            // Top-level statements: the synthesized Main, exempt for the same reason a written one is.
            return true;
        }

        if (context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken) is not { } symbol)
        {
            return false;
        }

        if (symbol is IMethodSymbol method
            && (SymbolEqualityComparer.Default.Equals(method, entryPoint) || IsAwaiterGetResult(method, notifyCompletion)))
        {
            return true;
        }

        return symbol.IsOverride || ImplementsInterfaceMember(symbol);
    }

    /// <summary>Finds the member declaration a node sits in.</summary>
    /// <param name="node">The node to walk up from.</param>
    /// <returns>The enclosing member, or <see langword="null"/> when the node belongs to no member.</returns>
    /// <remarks>
    /// Null means a top-level statement — the synthesized entry point — or an expression in a type's
    /// own header, such as a base-list argument. Neither names a member whose signature could be
    /// made async, and both are left alone.
    /// </remarks>
    private static MemberDeclarationSyntax? FindEnclosingMember(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case GlobalStatementSyntax:
                case BaseTypeDeclarationSyntax:
                case CompilationUnitSyntax:
                    return null;
                case MemberDeclarationSyntax member:
                    return member;
                default:
                    continue;
            }
        }

        return null;
    }

    /// <summary>Returns whether a method is the <c>GetResult</c> of a type that implements the awaiter pattern.</summary>
    /// <param name="method">The enclosing method.</param>
    /// <param name="notifyCompletion">The awaiter marker interface, when the framework has one.</param>
    /// <returns><see langword="true"/> when blocking there is the awaiter contract.</returns>
    private static bool IsAwaiterGetResult(IMethodSymbol method, INamedTypeSymbol? notifyCompletion)
    {
        if (notifyCompletion is null
            || method.Name != BlockingWait.GetResultMethodName
            || method.ContainingType is not { } type
            || !Implements(type, notifyCompletion))
        {
            return false;
        }

        var candidates = type.GetMembers(IsCompletedPropertyName);
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is IPropertySymbol)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type implements an interface.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="interfaceType">The interface sought.</param>
    /// <returns><see langword="true"/> when the type implements it.</returns>
    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol interfaceType)
    {
        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], interfaceType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a member implements an interface member, explicitly or implicitly.</summary>
    /// <param name="symbol">The member the blocking wait sits in.</param>
    /// <returns><see langword="true"/> when its signature belongs to an interface.</returns>
    private static bool ImplementsInterfaceMember(ISymbol symbol)
    {
        if (symbol.ContainingType is not { } containingType)
        {
            return false;
        }

        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var members = interfaces[i].GetMembers();
            for (var j = 0; j < members.Length; j++)
            {
                if (SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(members[j]), symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
