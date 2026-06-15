// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyVerbatim = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.RedundantVerbatimStringCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1184 (verbatim string needing no verbatim quoting) and its fix.</summary>
public class RedundantVerbatimStringAnalyzerUnitTest
{
    /// <summary>Verifies a verbatim string with plain text is reported and the prefix removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainVerbatimBecomesRegularAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M() => {|SST1184:@"hello"|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M() => "hello";
                                   }
                                   """;
        await VerifyVerbatim.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All strips every needless verbatim prefix across a document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string A() => {|SST1184:@"hello"|};

                                  public string B() => {|SST1184:@"world"|};

                                  public string D() => {|SST1184:@"again"|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string A() => "hello";

                                       public string B() => "world";

                                       public string D() => "again";
                                   }
                                   """;
        await VerifyVerbatim.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a verbatim string with a backslash and a regular string are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerbatimWithEscapesIsCleanAsync()
        => await VerifyVerbatim.VerifyAnalyzerAsync(
            """
            public class C
            {
                public string Path() => @"C:\temp";

                public string Plain() => "hello";
            }
            """);
}
