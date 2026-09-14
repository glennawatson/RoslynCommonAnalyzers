// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Direct tests for <see cref="MemberIndentation"/>, shared by the PSH1005 and PSH1223 code fixes.</summary>
public class MemberIndentationUnitTest
{
    /// <summary>Verifies the member indentation is the type's own indentation plus four spaces.</summary>
    /// <param name="source">The document source.</param>
    /// <param name="expected">The member indentation.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("struct S { }", "    ")]
    [Arguments("namespace N\n{\n    struct S { }\n}", "        ")]
    [Arguments("namespace N\n{\n\tstruct S { }\n}", "\t    ")]
    public async Task AddsOneLevelToTheTypeIndentationAsync(string source, string expected)
    {
        var owner = SyntaxFactory.ParseCompilationUnit(source).DescendantNodes().OfType<StructDeclarationSyntax>().Single();

        await Assert.That(MemberIndentation.Of(owner)).IsEqualTo(expected);
    }
}
