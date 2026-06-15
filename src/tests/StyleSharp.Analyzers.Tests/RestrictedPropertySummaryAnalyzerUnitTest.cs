// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRestrictedSummary = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer,
    StyleSharp.Analyzers.RestrictedPropertySummaryCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1624 (omit restricted set accessors from property summaries).</summary>
public class RestrictedPropertySummaryAnalyzerUnitTest
{
    /// <summary>Verifies a restricted setter summary is reported and fixed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RestrictedSetterSummaryIsFixedAsync()
    {
        const string Source = """
                              /// <summary>A container.</summary>
                              internal class C
                              {
                                  /// {|SST1624:<summary>Gets or sets the value.</summary>|}
                                  public int Value { get; private set; }
                              }
                              """;
        const string FixedSource = """
                                   /// <summary>A container.</summary>
                                   internal class C
                                   {
                                       /// <summary>Gets the value.</summary>
                                       public int Value { get; private set; }
                                   }
                                   """;
        await VerifyRestrictedSummary.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every restricted setter summary in one pass (SST1624).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              /// <summary>A container.</summary>
                              internal class C
                              {
                                  /// {|SST1624:<summary>Gets or sets the value.</summary>|}
                                  public int Value { get; private set; }

                                  /// {|SST1624:<summary>Gets or sets the name.</summary>|}
                                  public string Name { get; private set; }

                                  /// {|SST1624:<summary>Gets or sets the flag.</summary>|}
                                  public bool Flag { get; private set; }
                              }
                              """;
        const string FixedSource = """
                                   /// <summary>A container.</summary>
                                   internal class C
                                   {
                                       /// <summary>Gets the value.</summary>
                                       public int Value { get; private set; }

                                       /// <summary>Gets the name.</summary>
                                       public string Name { get; private set; }

                                       /// <summary>Gets the flag.</summary>
                                       public bool Flag { get; private set; }
                                   }
                                   """;
        await VerifyRestrictedSummary.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies equal accessor accessibility remains governed by SST1623.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EqualAccessibilityIsCleanAsync()
        => await VerifyRestrictedSummary.VerifyAnalyzerAsync(
            """
            /// <summary>A container.</summary>
            internal class C
            {
                /// <summary>Gets or sets the value.</summary>
                public int Value { get; set; }
            }
            """);
}
