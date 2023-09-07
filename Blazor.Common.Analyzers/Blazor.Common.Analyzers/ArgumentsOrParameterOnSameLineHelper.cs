// Copyright (c) 2023 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Blazor.Common.Analyzers;

internal static class ArgumentsOrParameterOnSameLineHelper
{
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

    public static void Analyze<T>(in SyntaxNodeAnalysisContext context, in int parameterLine, in SeparatedSyntaxList<T> list, DiagnosticDescriptor rule)
        where T : SyntaxNode
    {
        if (list.Count <= 1)
        {
            return;
        }

        var diffChecker = new HashSet<int>() { parameterLine };
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
