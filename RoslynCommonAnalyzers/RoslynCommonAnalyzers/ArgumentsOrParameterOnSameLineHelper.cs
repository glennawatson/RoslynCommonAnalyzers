using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using Microsoft.CodeAnalysis;

namespace RoslynCommonAnalyzers;

internal static class ArgumentsOrParameterOnSameLineHelper
{
    public static void HandleArgumentListSyntax(this in SyntaxNodeAnalysisContext context, ArgumentListSyntax argumentList, DiagnosticDescriptor rule)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = argumentList.Arguments;
        Analyze(context, arguments, rule);
    }

    public static void HandleArgumentListSyntax(this in SyntaxNodeAnalysisContext context, BracketedArgumentListSyntax argumentList, DiagnosticDescriptor rule)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = argumentList.Arguments;
        Analyze(context, arguments, rule);
    }

    public static void HandleArgumentListSyntax(this in SyntaxNodeAnalysisContext context, AttributeArgumentListSyntax argumentList, DiagnosticDescriptor rule)
    {
        var arguments = argumentList.Arguments;
        Analyze(context, arguments, rule);
    }

    public static void HandleParameterListSyntax(this in SyntaxNodeAnalysisContext context, ParameterListSyntax parameterList, DiagnosticDescriptor rule)
    {
        SeparatedSyntaxList<ParameterSyntax> parameters = parameterList.Parameters;
        Analyze(context, parameters, rule);
    }

    public static void HandleParameterListSyntax(this in SyntaxNodeAnalysisContext context, BracketedParameterListSyntax parameterList, DiagnosticDescriptor rule)
    {
        SeparatedSyntaxList<ParameterSyntax> parameters = parameterList.Parameters;
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
