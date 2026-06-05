// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Fast, allocation-free helpers that report when the items of a comma-delimited
/// list are laid out "jaggedly" — i.e. they are neither all on a single line nor
/// each on its own line.
/// </summary>
/// <remarks>
/// The scan exploits the fact that, in source order, the start lines of a list's
/// items are monotonically non-decreasing and the list's opening delimiter sits
/// at or before the first item. A list is therefore valid when every adjacent
/// pair shares a line (all on one line) or when every adjacent pair is separated
/// (each on its own line); it is jagged precisely when both a shared pair and a
/// separated pair occur. This lets a single pass with two flags replace the old
/// <c>HashSet</c> + LINQ approach with zero per-node heap allocations.
/// </remarks>
internal static class ArgumentsOrParameterOnSameLineHelper
{
    /// <summary>Analyzes an <see cref="ArgumentListSyntax"/> for arguments that are not on unique lines.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="argumentList">The argument list to analyze.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    public static void HandleArgumentListSyntax(this in SyntaxNodeAnalysisContext context, ArgumentListSyntax? argumentList, DiagnosticDescriptor rule)
    {
        if (argumentList is null)
        {
            return;
        }

        Analyze(context, argumentList, argumentList.Arguments, rule);
    }

    /// <summary>Analyzes a <see cref="BracketedArgumentListSyntax"/> for arguments that are not on unique lines.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="argumentList">The bracketed argument list to analyze.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    public static void HandleArgumentListSyntax(this in SyntaxNodeAnalysisContext context, BracketedArgumentListSyntax? argumentList, DiagnosticDescriptor rule)
    {
        if (argumentList is null)
        {
            return;
        }

        Analyze(context, argumentList, argumentList.Arguments, rule);
    }

    /// <summary>Analyzes an <see cref="AttributeArgumentListSyntax"/> for arguments that are not on unique lines.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="argumentList">The attribute argument list to analyze.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    public static void HandleArgumentListSyntax(this in SyntaxNodeAnalysisContext context, AttributeArgumentListSyntax? argumentList, DiagnosticDescriptor rule)
    {
        if (argumentList is null)
        {
            return;
        }

        Analyze(context, argumentList, argumentList.Arguments, rule);
    }

    /// <summary>Analyzes a <see cref="ParameterListSyntax"/> for parameters that are not on unique lines.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameterList">The parameter list to analyze.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    public static void HandleParameterListSyntax(this in SyntaxNodeAnalysisContext context, ParameterListSyntax? parameterList, DiagnosticDescriptor rule)
    {
        if (parameterList is null)
        {
            return;
        }

        Analyze(context, parameterList, parameterList.Parameters, rule);
    }

    /// <summary>Analyzes a <see cref="BracketedParameterListSyntax"/> for parameters that are not on unique lines.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameterList">The bracketed parameter list to analyze.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    public static void HandleParameterListSyntax(this in SyntaxNodeAnalysisContext context, BracketedParameterListSyntax? parameterList, DiagnosticDescriptor rule)
    {
        if (parameterList is null)
        {
            return;
        }

        Analyze(context, parameterList, parameterList.Parameters, rule);
    }

    /// <summary>Analyzes a <see cref="TypeParameterListSyntax"/> for parameters that are not on unique lines.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="typeParameterList">The type parameter list to analyze.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    public static void HandleTypeParameterListSyntax(this in SyntaxNodeAnalysisContext context, TypeParameterListSyntax? typeParameterList, DiagnosticDescriptor rule)
    {
        if (typeParameterList is null)
        {
            return;
        }

        Analyze(context, typeParameterList, typeParameterList.Parameters, rule);
    }

    /// <summary>Analyzes a <see cref="TypeArgumentListSyntax"/> for arguments that are not on unique lines.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="typeArgumentList">The type argument list to analyze.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    public static void HandleTypeArgumentListSyntax(this in SyntaxNodeAnalysisContext context, TypeArgumentListSyntax? typeArgumentList, DiagnosticDescriptor rule)
    {
        if (typeArgumentList is null)
        {
            return;
        }

        Analyze(context, typeArgumentList, typeArgumentList.Arguments, rule);
    }

    /// <summary>Analyzes a <see cref="FunctionPointerParameterListSyntax"/> for parameters that are not on unique lines.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="functionPointerParameterList">The function pointer parameter list to analyze.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    public static void HandleFunctionPointerParameterListSyntax(this in SyntaxNodeAnalysisContext context, FunctionPointerParameterListSyntax? functionPointerParameterList, DiagnosticDescriptor rule)
    {
        if (functionPointerParameterList is null)
        {
            return;
        }

        Analyze(context, functionPointerParameterList, functionPointerParameterList.Parameters, rule);
    }

    /// <summary>
    /// Reports a diagnostic when the items in the list are laid out jaggedly
    /// (some sharing a line with a neighbour while others are wrapped).
    /// </summary>
    /// <typeparam name="T">The type of syntax node contained in the list.</typeparam>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="listNode">The node owning the items; its opening delimiter anchors the first line.</param>
    /// <param name="list">The separated list of arguments or parameters.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    public static void Analyze<T>(in SyntaxNodeAnalysisContext context, SyntaxNode listNode, in SeparatedSyntaxList<T> list, DiagnosticDescriptor rule)
        where T : SyntaxNode
    {
        var count = list.Count;
        if (count <= 1)
        {
            return;
        }

        var tree = listNode.SyntaxTree;
        if (tree is null)
        {
            return;
        }

        // Anchor line = the list's opening delimiter. Start lines are
        // monotonically non-decreasing from here through every item.
        var previousLine = tree.GetLineSpan(listNode.Span).StartLinePosition.Line;
        var sawShared = false;
        var sawSeparated = false;

        foreach (var item in list)
        {
            var line = tree.GetLineSpan(item.Span).StartLinePosition.Line;
            if (line == previousLine)
            {
                sawShared = true;
            }
            else
            {
                sawSeparated = true;
            }

            // A shared pair and a separated pair together mean a jagged layout;
            // the verdict cannot change after this, so report once and stop.
            if (sawShared && sawSeparated)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, context.Node.GetLocation()));
                return;
            }

            previousLine = line;
        }
    }
}
