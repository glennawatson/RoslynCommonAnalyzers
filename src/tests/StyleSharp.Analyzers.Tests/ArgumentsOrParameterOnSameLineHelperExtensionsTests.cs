// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the list-layout handlers independently of their analyzer registrations.</summary>
public class ArgumentsOrParameterOnSameLineHelperExtensionsTests
{
    /// <summary>The descriptor used to observe reports from each handler.</summary>
    private static readonly DiagnosticDescriptor Rule = new("TEST001", "Layout", "Layout", "Layout", DiagnosticSeverity.Warning, true);

    /// <summary>Verifies absent optional lists do not report or dereference the analysis context.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public Task NullListsAreIgnoredAsync()
    {
        var context = default(SyntaxNodeAnalysisContext);

        context.HandleFunctionPointerParameterListSyntax(null, Rule);
        context.HandleTypeArgumentListSyntax(null, Rule);
        context.HandleTypeParameterListSyntax(null, Rule);
        context.HandleParameterListSyntax((BracketedParameterListSyntax?)null, Rule);
        context.HandleParameterListSyntax((ParameterListSyntax?)null, Rule);
        context.HandleArgumentListSyntax((AttributeArgumentListSyntax?)null, Rule);
        context.HandleArgumentListSyntax((BracketedArgumentListSyntax?)null, Rule);
        context.HandleArgumentListSyntax((ArgumentListSyntax?)null, Rule);

        return Task.CompletedTask;
    }

    /// <summary>Verifies the opening delimiter participates in layout classification.</summary>
    /// <param name="source">The invocation containing the list.</param>
    /// <param name="expected">Whether the list mixes shared and separate lines.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("M()", false)]
    [Arguments("M(1)", false)]
    [Arguments("M(1, 2)", false)]
    [Arguments("M(\n1,\n2)", false)]
    [Arguments("M(1,\n2)", true)]
    [Arguments("M(\n1, 2)", true)]
    [Arguments("M(\r\n1,\r\n2)", false)]
    public async Task ArgumentLayoutUsesItemStartLinesAsync(string source, bool expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(source);
        await Assert.That(ArgumentsOrParameterOnSameLineHelperExtensions.ReportsJaggedLayout(invocation.ArgumentList, invocation.ArgumentList.Arguments)).IsEqualTo(expected);
    }
}
