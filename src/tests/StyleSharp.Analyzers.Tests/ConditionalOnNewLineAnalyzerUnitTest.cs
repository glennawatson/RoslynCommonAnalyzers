// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConditional = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1146ConditionalOnNewLineAnalyzer,
    StyleSharp.Analyzers.Sst1146ConditionalOnNewLineCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1146 (start an independent if statement on a new line).</summary>
public class ConditionalOnNewLineAnalyzerUnitTest
{
    /// <summary>Verifies a same-line closing brace and independent if are reported and fixed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SameLineIfIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(bool a, bool b)
                                  {
                                      if (a) { } {|SST1146:if|} (b) { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(bool a, bool b)
                                       {
                                           if (a) { }
                                           if (b) { }
                                       }
                                   }
                                   """;
        await VerifyConditional.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All moves every same-line independent if onto its own line in one pass (SST1146).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(bool a, bool b, bool c, bool d)
                                  {
                                      if (a) { } {|SST1146:if|} (b) { } {|SST1146:if|} (c) { } {|SST1146:if|} (d) { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(bool a, bool b, bool c, bool d)
                                       {
                                           if (a) { }
                                           if (b) { }
                                           if (c) { }
                                           if (d) { }
                                       }
                                   }
                                   """;
        await VerifyConditional.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies normal multi-line and else-if forms are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NormalFormsAreCleanAsync()
        => await VerifyConditional.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a) { }
                    if (b) { }
                    else if (a) { }
                }
            }
            """);
}
