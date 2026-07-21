// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRegularString = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2262UseRegularStringLiteralAnalyzer,
    StyleSharp.Analyzers.Sst2262UseRegularStringLiteralCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2262UseRegularStringLiteralAnalyzer"/> and its code fix (SST2262).</summary>
public class UseRegularStringLiteralAnalyzerUnitTest
{
    /// <summary>Verifies a single-line raw literal with plain content is reported and demoted to a regular literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainRawLiteralIsFlaggedAndFixedAsync()
    {
        const string Source = """"
                              internal class C
                              {
                                  private string _value = {|SST2262:"""plain text"""|};
                              }
                              """";
        const string FixedSource = """"
                                   internal class C
                                   {
                                       private string _value = "plain text";
                                   }
                                   """";
        await VerifyRegularString.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a raw literal whose content carries a quote is left alone; raw syntax is earning its keep.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RawLiteralWithQuoteIsCleanAsync()
        => await VerifyRegularString.VerifyAnalyzerAsync(
            """"
            internal class C
            {
                private string _value = """say "hi" there""";
            }
            """");

    /// <summary>Verifies a raw literal whose content carries a backslash is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RawLiteralWithBackslashIsCleanAsync()
        => await VerifyRegularString.VerifyAnalyzerAsync(
            """"
            internal class C
            {
                private string _value = """a\b""";
            }
            """");

    /// <summary>Verifies a regular literal is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RegularLiteralIsCleanAsync()
        => await VerifyRegularString.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private string _value = "plain text";
            }
            """);
}
