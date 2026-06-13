// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNegatedIs = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.PatternMatchingAnalyzer,
    StyleSharp.Analyzers.NegatedIsPatternCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2006 (negated is check) and its fix.</summary>
public class NegatedIsPatternAnalyzerUnitTest
{
    /// <summary>Verifies <c>!(x is T)</c> becomes <c>x is not T</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedIsBecomesIsNotAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(object x) => {|SST2006:!(x is string)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(object x) => x is not string;
                                   }
                                   """;
        await VerifyNegatedIs.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an existing <c>is not</c> pattern and a negated boolean are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModernAndUnrelatedNegationsAreCleanAsync()
        => await VerifyNegatedIs.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Pattern(object x) => x is not string;

                public bool Flag(bool ready) => !ready;
            }
            """);
}
