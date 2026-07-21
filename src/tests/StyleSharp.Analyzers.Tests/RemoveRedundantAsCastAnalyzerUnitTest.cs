// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRedundantAs = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2260RemoveRedundantAsCastAnalyzer,
    StyleSharp.Analyzers.Sst2260RemoveRedundantAsCastCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2260RemoveRedundantAsCastAnalyzer"/> and its code fix (SST2260).</summary>
public class RemoveRedundantAsCastAnalyzerUnitTest
{
    /// <summary>Verifies an <c>as</c> cast to the operand's own type is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameTypeAsCastIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public string M(string s)
                                  {
                                      var t = {|SST2260:s as string|};
                                      return t;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public string M(string s)
                                       {
                                           var t = s;
                                           return t;
                                       }
                                   }
                                   """;
        await VerifyRedundantAs.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an <c>as</c> cast on a wider operand type is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WideningAsCastIsCleanAsync()
        => await VerifyRedundantAs.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(object o)
                {
                    _ = o as string;
                }
            }
            """);

    /// <summary>Verifies an <c>as</c> cast that narrows to a base type is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NarrowingToBaseIsCleanAsync()
        => await VerifyRedundantAs.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(string s)
                {
                    _ = s as object;
                }
            }
            """);
}
