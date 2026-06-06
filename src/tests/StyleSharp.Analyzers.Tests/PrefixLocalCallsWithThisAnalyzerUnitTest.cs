// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyThisPrefix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.PrefixLocalCallsWithThisAnalyzer,
    StyleSharp.Analyzers.PrefixLocalCallsWithThisCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the this-prefix rule (SST1101, opt-in).</summary>
public class PrefixLocalCallsWithThisAnalyzerUnitTest
{
    /// <summary>Verifies a bare instance-member call is reported (SST1101) and prefixed with this.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareInstanceMemberPrefixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int field;

                                  private int M() => {|SST1101:field|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int field;

                                       private int M() => this.field;
                                   }
                                   """;
        await VerifyThisPrefix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies only the bare instance reference is flagged — qualified, static, and local references are skipped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedStaticAndLocalReferencesAreSkippedAsync()
        => await VerifyThisPrefix.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static int shared;

                private int field;

                private int M()
                {
                    var local = 1;
                    return {|SST1101:field|} + shared + local + this.field;
                }
            }
            """);
}
