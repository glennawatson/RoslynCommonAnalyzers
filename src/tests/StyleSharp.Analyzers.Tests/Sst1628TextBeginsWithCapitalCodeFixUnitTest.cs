// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCapitalFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.DocumentationTextAnalyzer,
    StyleSharp.Analyzers.Sst1628TextBeginsWithCapitalCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1628 code fix (documentation text begins with a capital letter).</summary>
public class Sst1628TextBeginsWithCapitalCodeFixUnitTest
{
    /// <summary>Verifies a single-line summary gains its capital.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineSummaryIsCapitalizedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// {|SST1628:<summary>does a thing.</summary>|}
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does a thing.</summary>
                                       public void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifyCapitalFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a summary written across lines gains its capital on the first word.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrappedSummaryIsCapitalizedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// {|SST1628:<summary>
                                  /// does a thing worth describing.
                                  /// </summary>|}
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>
                                       /// Does a thing worth describing.
                                       /// </summary>
                                       public void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifyCapitalFix.VerifyCodeFixAsync(Source, FixedSource);
    }
}
