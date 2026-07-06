// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyDefaultLiteral = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.UseDefaultLiteralCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1188 (use the 'default' literal) and its fix.</summary>
public class UseDefaultLiteralAnalyzerUnitTest
{
    /// <summary>Verifies <c>default(int)</c> in a return position is reported and shortened.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnedDefaultShortenedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M() => {|SST1188:default(int)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M() => default;
                                   }
                                   """;
        await VerifyDefaultLiteral.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a <c>var</c> initializer and a converting context keep the explicit form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeRequiredContextsAreCleanAsync()
        => await VerifyDefaultLiteral.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M()
                {
                    var x = default(int);
                    object o = default(int);
                    _ = default(int).ToString();
                }
            }
            """);

    /// <summary>Verifies Fix All shortens every redundant default expression in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int First() => {|SST1188:default(int)|};
                                  public int Second() => {|SST1188:default(int)|};
                                  public int Third() => {|SST1188:default(int)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int First() => default;
                                       public int Second() => default;
                                       public int Third() => default;
                                   }
                                   """;
        await VerifyDefaultLiteral.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies SST1188 stays silent below C# 7.1, where the bare <c>default</c> literal the fix emits does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp71Async()
    {
        // 'default(int)' compiles at C# 7, but the fix's bare 'default' literal only arrived in
        // C# 7.1, so under the C# 7 pin the analyzer must not report the redundant type argument.
        const string Source = """
                              public class C
                              {
                                  public int M() => default(int);
                              }
                              """;
        var test = new VerifyDefaultLiteral.Test { TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp7));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
