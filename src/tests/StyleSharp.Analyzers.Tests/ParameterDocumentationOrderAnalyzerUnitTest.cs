// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1660ParameterDocumentationOrderAnalyzer,
    StyleSharp.Analyzers.Sst1660ParameterDocumentationOrderCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1660 (parameter documentation should be ordered to match the parameters).</summary>
public class ParameterDocumentationOrderAnalyzerUnitTest
{
    /// <summary>Verifies <c>&lt;param&gt;</c> elements in declaration order produce no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InOrderIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Does things.</summary>
                /// <param name="a">The a.</param>
                /// <param name="b">The b.</param>
                public void M(int a, int b)
                {
                }
            }
            """);

    /// <summary>Verifies a partially-documented member is out of scope (the set does not match).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IncompleteSetIsIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Does things.</summary>
                /// <param name="a">The a.</param>
                public void M(int a, int b)
                {
                }
            }
            """);

    /// <summary>Verifies out-of-order parameter documentation is reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OutOfOrderIsReorderedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Does things.</summary>
                                  /// {|SST1660:<param name="b">The b.</param>|}
                                  /// <param name="a">The a.</param>
                                  public void M(int a, int b)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does things.</summary>
                                       /// <param name="a">The a.</param>
                                       /// <param name="b">The b.</param>
                                       public void M(int a, int b)
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a three-parameter rotation is reordered to match the declaration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RotationIsReorderedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// <summary>Does things.</summary>
                                  /// {|SST1660:<param name="c">The c.</param>|}
                                  /// <param name="a">The a.</param>
                                  /// <param name="b">The b.</param>
                                  public void M(int a, int b, int c)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does things.</summary>
                                       /// <param name="a">The a.</param>
                                       /// <param name="b">The b.</param>
                                       /// <param name="c">The c.</param>
                                       public void M(int a, int b, int c)
                                       {
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }
}
