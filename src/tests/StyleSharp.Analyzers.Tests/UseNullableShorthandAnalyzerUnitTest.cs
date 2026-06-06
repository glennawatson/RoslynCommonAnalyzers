// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNullableShorthand = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.UseNullableShorthandAnalyzer,
    StyleSharp.Analyzers.UseNullableShorthandCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the nullable-shorthand rule (SST1125).</summary>
public class UseNullableShorthandAnalyzerUnitTest
{
    /// <summary>Verifies a long-form Nullable&lt;T&gt; is reported (SST1125) and rewritten as the shorthand.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LongFormRewrittenAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private {|SST1125:System.Nullable<int>|} value;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int? value;
                                   }
                                   """;
        await VerifyNullableShorthand.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the shorthand form is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShorthandIsCleanAsync()
        => await VerifyNullableShorthand.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int? value;
            }
            """);
}
