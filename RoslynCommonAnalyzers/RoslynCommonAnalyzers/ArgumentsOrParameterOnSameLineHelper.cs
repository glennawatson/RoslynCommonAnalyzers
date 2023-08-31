// Copyright (c) 2023 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommonAnalyzers;

internal static class ArgumentsOrParameterOnSameLineHelper
{
    public static void HandleArgumentListSyntax(this in SyntaxNodeAnalysisContext context, ArgumentListSyntax argumentList, DiagnosticDescriptor rule)
    {
        var arguments = argumentList.Arguments;
        Analyze(context, arguments, rule);
    }

    public static void HandleArgumentListSyntax(this in SyntaxNodeAnalysisContext context, BracketedArgumentListSyntax argumentList, DiagnosticDescriptor rule)
    {
        var arguments = argumentList.Arguments;
        Analyze(context, arguments, rule);
    }

    public static void HandleArgumentListSyntax(this in SyntaxNodeAnalysisContext context, AttributeArgumentListSyntax argumentList, DiagnosticDescriptor rule)
    {
        var arguments = argumentList.Arguments;
        Analyze(context, arguments, rule);
    }

    public static void HandleParameterListSyntax(this in SyntaxNodeAnalysisContext context, ParameterListSyntax parameterList, DiagnosticDescriptor rule)
    {
        var parameters = parameterList.Parameters;
        Analyze(context, parameters, rule);
    }

    public static void HandleParameterListSyntax(this in SyntaxNodeAnalysisContext context, BracketedParameterListSyntax parameterList, DiagnosticDescriptor rule)
    {
        var parameters = parameterList.Parameters;
        Analyze(context, parameters, rule);
    }

    public static void Analyze<T>(in SyntaxNodeAnalysisContext context, in SeparatedSyntaxList<T> list, DiagnosticDescriptor rule)
        where T : SyntaxNode
    {
        if (list.Count <= 1)
        {
            return;
        }

        var nodeLine = context.Node.GetLocation().GetLineSpan().StartLinePosition.Line;
        var diffChecker = new HashSet<int>() { nodeLine };
        var lineNumbers = list.Select(x => x.GetLocation().GetLineSpan().StartLinePosition.Line);
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
