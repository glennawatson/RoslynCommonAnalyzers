// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Blazor.Common.Analyzers;

/// <summary>Helper methods for analyzing whether arguments or parameters are placed on unique lines.</summary>
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

        var arguments = argumentList.Arguments;
        var argumentsLine = argumentList.GetLocation().GetLineSpan().StartLinePosition.Line;
        Analyze(context, argumentsLine, arguments, rule);
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

        var arguments = argumentList.Arguments;
        var argumentsLine = argumentList.GetLocation().GetLineSpan().StartLinePosition.Line;
        Analyze(context, argumentsLine, arguments, rule);
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

        var arguments = argumentList.Arguments;
        var argumentsLine = argumentList.GetLocation().GetLineSpan().StartLinePosition.Line;

        Analyze(context, argumentsLine, arguments, rule);
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

        var parameters = parameterList.Parameters;
        var paremeterLine = parameterList.GetLocation().GetLineSpan().StartLinePosition.Line;

        Analyze(context, paremeterLine, parameters, rule);
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

        var parameters = parameterList.Parameters;
        var paremeterLine = parameterList.GetLocation().GetLineSpan().StartLinePosition.Line;

        Analyze(context, paremeterLine, parameters, rule);
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

        var parameters = typeParameterList.Parameters;
        var paremeterLine = typeParameterList.GetLocation().GetLineSpan().StartLinePosition.Line;

        Analyze(context, paremeterLine, parameters, rule);
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

        var arguments = typeArgumentList.Arguments;
        var argumentsLine = typeArgumentList.GetLocation().GetLineSpan().StartLinePosition.Line;

        Analyze(context, argumentsLine, arguments, rule);
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

        var parameters = functionPointerParameterList.Parameters;
        var paremeterLine = functionPointerParameterList.GetLocation().GetLineSpan().StartLinePosition.Line;

        Analyze(context, paremeterLine, parameters, rule);
    }

    /// <summary>Reports a diagnostic when the items in the list are not all on unique lines.</summary>
    /// <typeparam name="T">The type of syntax node contained in the list.</typeparam>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameterLine">The line number of the enclosing list declaration.</param>
    /// <param name="list">The separated list of arguments or parameters.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    public static void Analyze<T>(in SyntaxNodeAnalysisContext context, in int parameterLine, in SeparatedSyntaxList<T> list, DiagnosticDescriptor rule)
        where T : SyntaxNode
    {
        if (list.Count <= 1)
        {
            return;
        }

        var diffChecker = new HashSet<int> { parameterLine };
        var lineNumbers = list.Select(x => x.GetLocation().GetLineSpan().StartLinePosition.Line).ToList();
        diffChecker.UnionWith(lineNumbers);

        var allDifferent = diffChecker.Count == list.Count + 1;
        if (allDifferent)
        {
            return;
        }

        if (diffChecker.Count == 1)
        {
            return;
        }

        // For all such syntax nodes, produce a diagnostic.
        var diagnostic = Diagnostic.Create(rule, context.Node.GetLocation());

        context.ReportDiagnostic(diagnostic);
    }
}
