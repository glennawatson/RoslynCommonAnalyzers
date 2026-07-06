// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1658NoRepeatedWordsAnalyzer,
    StyleSharp.Analyzers.Sst1658NoRepeatedWordsCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the repeated-word documentation rule (SST1658).</summary>
public class NoRepeatedWordsAnalyzerUnitTest
{
    /// <summary>Verifies documentation without adjacent repeated words produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CleanDocumentationAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Gets the value from the store.</summary>
                /// <param name="x">The input value.</param>
                public void M(int x)
                {
                }
            }
            """);

    /// <summary>Verifies a word typed twice in a row is reported on the second occurrence and removed by the fix (SST1658).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RepeatedWordReportedAndRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Gets the {|SST1658:the|} value.</summary>
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Gets the value.</summary>
                                       public void M()
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the word comparison is case-insensitive, so "The the" is reported and fixed (SST1658).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CaseInsensitivePairReportedAndRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>The {|SST1658:the|} value.</summary>
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>The value.</summary>
                                       public void M()
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a pair split by a documentation line break is reported and the fix keeps a single instance (SST1658).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RepeatedWordAcrossLineBreakReportedAndRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Gets the
                                  /// {|SST1658:the|} value.</summary>
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Gets the
                                       /// value.</summary>
                                       public void M()
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies repeated words inside a code element are not scanned.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeElementIsNotScannedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Use <c>code code</c> to run it.</summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies punctuation between two matching words keeps them from forming a pair.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PunctuationBreaksThePairAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>The end. End of story.</summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies parameter documentation text is scanned and fixed (SST1658).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterTextScannedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Does the work.</summary>
                                  /// <param name="x">The {|SST1658:the|} input.</param>
                                  public void M(int x)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does the work.</summary>
                                       /// <param name="x">The input.</param>
                                       public void M(int x)
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies two independent pairs in one comment produce two diagnostics and both are removed (SST1658).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoIndependentPairsReportedAndRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Gets the {|SST1658:the|} value.</summary>
                                  /// <param name="x">Sets a {|SST1658:a|} flag.</param>
                                  public void M(int x)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Gets the value.</summary>
                                       /// <param name="x">Sets a flag.</param>
                                       public void M(int x)
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }
}
