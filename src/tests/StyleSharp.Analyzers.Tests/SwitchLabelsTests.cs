// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests where a switch statement's default label is found.</summary>
public class SwitchLabelsTests
{
    /// <summary>Verifies a default label in any section is found, whether alone or shared with a case.</summary>
    /// <param name="source">The switch statement.</param>
    /// <param name="expected">Whether any section carries the default label.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("switch (x) { case 1: break; }", false)]
    [Arguments("switch (x) { }", false)]
    [Arguments("switch (x) { case 1: default: break; }", true)]
    [Arguments("switch (x) { case 1: break; default: break; }", true)]
    public async Task AnySectionHasDefaultSearchesEverySectionAsync(string source, bool expected)
    {
        var statement = (SwitchStatementSyntax)SyntaxFactory.ParseStatement(source);

        await Assert.That(SwitchLabels.AnySectionHasDefault(statement.Sections)).IsEqualTo(expected);
    }

    /// <summary>Verifies one section's labels are read on their own.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ContainsDefaultReadsOneSectionAsync()
    {
        var statement = (SwitchStatementSyntax)SyntaxFactory.ParseStatement("switch (x) { case 1: break; case 2: default: break; }");

        await Assert.That(SwitchLabels.ContainsDefault(statement.Sections[0].Labels)).IsFalse();
        await Assert.That(SwitchLabels.ContainsDefault(statement.Sections[1].Labels)).IsTrue();
    }
}
