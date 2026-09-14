// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the detection of source an inactive <c>#if</c> branch left out of the compilation.</summary>
public class InactivePreprocessorRegionsTests
{
    /// <summary>Verifies an untaken branch anywhere in the file is found, and a taken or absent one is not.</summary>
    /// <param name="source">The compilation unit.</param>
    /// <param name="expected">Whether inactive source is present.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }", false)]
    [Arguments("#region R\nclass C { }\n#endregion", false)]
    [Arguments("#if DEBUG\nclass C { }\n#endif", true)]
    [Arguments("#if !DEBUG\nclass C { }\n#endif", false)]
    [Arguments("class C\n{\n    void M()\n    {\n#if DEBUG\n        M();\n#endif\n    }\n}", true)]
    [Arguments("class C\n{\n#if !DEBUG\n    int x;\n#else\n    int y;\n#endif\n}", true)]
    public async Task FindsTextOfAnUntakenBranchAsync(string source, bool expected)
    {
        var root = SyntaxFactory.ParseCompilationUnit(source);

        await Assert.That(InactivePreprocessorRegions.Contains(root)).IsEqualTo(expected);
        await Assert.That(InactivePreprocessorRegions.ContainsDisabledText(root)).IsEqualTo(expected);
    }

    /// <summary>Verifies a subtree only reports the inactive text that falls inside it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ScopesTheSearchToTheNodeAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class A\n{\n#if DEBUG\n    int x;\n#endif\n}\nclass B { }");

        await Assert.That(InactivePreprocessorRegions.Contains(root.Members[0])).IsTrue();
        await Assert.That(InactivePreprocessorRegions.Contains(root.Members[1])).IsFalse();
    }

    /// <summary>Verifies a single token's trivia list reports the inactive text it carries and nothing else.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ChecksOneTriviaListOnItsOwnAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("#if DEBUG\nusing System;\n#endif\nclass C { }");
        var leading = root.Members[0].GetFirstToken().LeadingTrivia;

        await Assert.That(InactivePreprocessorRegions.ContainsDisabledText(leading)).IsTrue();
        await Assert.That(InactivePreprocessorRegions.ContainsDisabledText(root.EndOfFileToken.LeadingTrivia)).IsFalse();
    }
}
