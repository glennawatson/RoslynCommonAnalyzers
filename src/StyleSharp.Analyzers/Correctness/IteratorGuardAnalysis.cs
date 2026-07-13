// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Purely syntactic detection of an iterator whose argument checks are stranded behind its first
/// <c>MoveNext</c> (SST2404). Shared by <see cref="Sst2404IteratorValidatesTooLateAnalyzer"/> and its code
/// fix so that what is reported and what is split can never disagree about where the guards end.
/// </summary>
internal static class IteratorGuardAnalysis
{
    /// <summary>The prefix every runtime argument throw-helper shares.</summary>
    private const string ThrowHelperPrefix = "ThrowIf";

    /// <summary>The suffix the type declaring a throw-helper shares.</summary>
    private const string ExceptionSuffix = "Exception";

    /// <summary>Counts the validation statements a method body opens with.</summary>
    /// <param name="body">The method body.</param>
    /// <param name="parameters">The method's parameters.</param>
    /// <returns>The number of leading guards, or <c>0</c> when none of them checks an argument.</returns>
    /// <remarks>
    /// Every leading guard is counted, not only the ones naming a parameter: a disposed check standing in
    /// front of a null check is just as stranded, and splitting the method has to take both with it. At least
    /// one of them must check an argument, though — otherwise the method is not validating what it was
    /// handed, and this rule has nothing to say about when it runs.
    /// </remarks>
    public static int CountLeadingGuards(BlockSyntax body, ParameterListSyntax parameters)
    {
        if (parameters.Parameters.Count == 0)
        {
            return 0;
        }

        var statements = body.Statements;
        var count = 0;
        var checksAnArgument = false;
        while (count < statements.Count && IsValidation(statements[count], parameters, ref checksAnArgument))
        {
            count++;
        }

        return checksAnArgument ? count : 0;
    }

    /// <summary>Returns whether a method's own body yields.</summary>
    /// <param name="body">The method body.</param>
    /// <returns><see langword="true"/> when the method is an iterator.</returns>
    /// <remarks>
    /// A <c>yield</c> inside a nested lambda or local function belongs to that function, not to the method
    /// around it, so the walk stops at both.
    /// </remarks>
    public static bool IsIterator(BlockSyntax body) => ContainsYield(body);

    /// <summary>Returns whether a statement is a guard that throws.</summary>
    /// <param name="statement">The statement.</param>
    /// <param name="parameters">The method's parameters.</param>
    /// <param name="checksAnArgument">Set when the guard reads one of the method's parameters.</param>
    /// <returns><see langword="true"/> for an <c>if</c> that throws, or a runtime throw-helper call.</returns>
    private static bool IsValidation(StatementSyntax statement, ParameterListSyntax parameters, ref bool checksAnArgument)
    {
        switch (statement)
        {
            case IfStatementSyntax { Else: null } guard when Throws(guard.Statement):
            {
                checksAnArgument |= ReferencesParameter(guard.Condition, parameters);
                return true;
            }

            case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation } when IsThrowHelper(invocation):
            {
                checksAnArgument |= ReferencesParameter(invocation.ArgumentList, parameters);
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Returns whether a guard's body ends in a throw.</summary>
    /// <param name="statement">The guard's body.</param>
    /// <returns><see langword="true"/> when reaching it means throwing.</returns>
    private static bool Throws(StatementSyntax statement) => statement switch
    {
        ThrowStatementSyntax => true,
        BlockSyntax { Statements: [.., ThrowStatementSyntax] } => true,
        _ => false,
    };

    /// <summary>Returns whether an invocation is one of the runtime's argument throw-helpers.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns><see langword="true"/> for <c>SomethingException.ThrowIfSomething(…)</c>.</returns>
    /// <remarks>
    /// Matched on shape rather than on a list of names, so a helper added to the framework later — or one of
    /// the project's own, following the same convention — is recognized without a change here.
    /// </remarks>
    private static bool IsThrowHelper(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax access
            && access.Name.Identifier.ValueText.StartsWith(ThrowHelperPrefix, StringComparison.Ordinal)
            && GetSimpleName(access.Expression) is { } receiver
            && receiver.EndsWith(ExceptionSuffix, StringComparison.Ordinal);

    /// <summary>Gets the rightmost name of a possibly qualified expression.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The simple name, or <see langword="null"/> when the expression is not a name.</returns>
    private static string? GetSimpleName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns whether a node reads one of the method's parameters.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="parameters">The method's parameters.</param>
    /// <returns><see langword="true"/> when a parameter's name appears.</returns>
    private static bool ReferencesParameter(SyntaxNode node, ParameterListSyntax parameters)
    {
        var scan = new ParameterScan(parameters);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, ParameterScan>(node, ref scan, VisitIdentifier);
        return scan.Found || (node is IdentifierNameSyntax self && NamesParameter(self, parameters));
    }

    /// <summary>Records whether an identifier names one of the method's parameters.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once a parameter is found, which stops the walk.</returns>
    private static bool VisitIdentifier(IdentifierNameSyntax identifier, ref ParameterScan state)
    {
        if (!NamesParameter(identifier, state.Parameters))
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Returns whether an identifier names one of the parameters.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="parameters">The method's parameters.</param>
    /// <returns><see langword="true"/> when the name matches a parameter.</returns>
    private static bool NamesParameter(IdentifierNameSyntax identifier, ParameterListSyntax parameters)
    {
        var name = identifier.Identifier.ValueText;
        var list = parameters.Parameters;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node yields, without descending into a nested function.</summary>
    /// <param name="node">The node to search.</param>
    /// <returns><see langword="true"/> when the node's own code contains a <c>yield</c>.</returns>
    private static bool ContainsYield(SyntaxNode node)
    {
        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (!children[i].IsNode || children[i].AsNode() is not { } child)
            {
                continue;
            }

            if (child is YieldStatementSyntax)
            {
                return true;
            }

            if (child is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                continue;
            }

            if (ContainsYield(child))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The state threaded through the search for a parameter reference.</summary>
    /// <param name="Parameters">The method's parameters.</param>
    private record struct ParameterScan(ParameterListSyntax Parameters)
    {
        /// <summary>Gets or sets a value indicating whether a parameter was referenced.</summary>
        public bool Found { get; set; }
    }
}
