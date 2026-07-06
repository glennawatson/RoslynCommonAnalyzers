// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyIsNotPattern = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2008IsNotPatternAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2008IsNotPatternAnalyzer"/>.</summary>
public class IsNotPatternAnalyzerUnitTest
{
    /// <summary>Verifies a negated null pattern is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NegatedPatternIsReportedAsync()
        => await VerifyIsNotPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object value) => {|SST2008:!(value is null)|};
            }
            """);

    /// <summary>Verifies a declaration pattern is skipped because the declared name cannot be preserved.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NegatedDeclarationPatternIsCleanAsync()
        => await VerifyIsNotPattern.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object value) => !(value is string text);
            }
            """);

    /// <summary>Verifies the rule stays silent below C# 9, where the <c>is not</c> pattern the fix emits does not exist.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SilentBelowCSharp9Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(object value) => !(value is null);
                              }
                              """;
        var test = new VerifyIsNotPattern.Test { TestCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp8));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
