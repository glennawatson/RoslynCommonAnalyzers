// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyAsNull = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.PatternMatchingAnalyzer,
    StyleSharp.Analyzers.IsPatternOverAsNullCheckCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2005 (as cast compared to null) and its fix.</summary>
public class IsPatternOverAsNullCheckAnalyzerUnitTest
{
    /// <summary>Verifies <c>(x as T) != null</c> becomes <c>x is T</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NotNullBecomesIsPatternAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(object x) => {|SST2005:(x as string) != null|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(object x) => x is string;
                                   }
                                   """;
        await VerifyAsNull.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies <c>(x as T) == null</c> becomes <c>x is not T</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualNullBecomesIsNotPatternAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(object x) => {|SST2005:(x as string) == null|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(object x) => x is not string;
                                   }
                                   """;
        await VerifyAsNull.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every <c>as</c>/null comparison in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool A(object x) => {|SST2005:(x as string) != null|};

                                  public bool B(object x) => {|SST2005:(x as string) == null|};

                                  public bool D(object x) => {|SST2005:(x as System.Exception) != null|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool A(object x) => x is string;

                                       public bool B(object x) => x is not string;

                                       public bool D(object x) => x is System.Exception;
                                   }
                                   """;
        await VerifyAsNull.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an ordinary null check and an existing pattern are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainNullChecksAreCleanAsync()
        => await VerifyAsNull.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Plain(string x) => x != null;

                public bool Pattern(object x) => x is string;
            }
            """);

    /// <summary>Verifies the <c>== null</c> branch stays silent below C# 9, where the <c>is not</c> pattern the fix emits does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualNullSilentBelowCSharp9Async()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(object x) => (x as string) == null;
                              }
                              """;
        var test = new VerifyAsNull.Test { TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp8));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
