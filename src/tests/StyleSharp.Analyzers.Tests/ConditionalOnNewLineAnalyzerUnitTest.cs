// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConditional = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ConditionalOnNewLineAnalyzer,
    StyleSharp.Analyzers.ConditionalOnNewLineCodeFixProvider>;

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

    /// <summary>Verifies the code fix preserves CRLF line endings when it moves the independent if statement.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SameLineIfIsFixedWithCrLfAsync()
    {
        string source = """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a) { } {|SST1146:if|} (b) { }
                }
            }
            """.ReplaceLineEndings("\r\n");
        string fixedSource = """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a) { }
                    if (b) { }
                }
            }
            """.ReplaceLineEndings("\r\n");
        await VerifyConditional.VerifyCodeFixAsync(source, fixedSource);
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
