// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConsistent = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1520ConsistentBracesAnalyzer,
    StyleSharp.Analyzers.Sst1520ConsistentBracesCodeFixProvider>;
using VerifyMultiLine = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1519MultiLineChildBraceAnalyzer,
    StyleSharp.Analyzers.Sst1519MultiLineChildBraceCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the brace-requirement rules (SST1519/SST1520).</summary>
public class LayoutBraceRequirementUnitTest
{
    /// <summary>Verifies a multi-line unbraced child is reported (SST1519) and wrapped in braces.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiLineChildWrappedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M(bool x)
                {
                    {|SST1519:if|} (x)
                        System.Console
                            .WriteLine();
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                        System.Console
                            .WriteLine();
                    }
                }
            }
            """;
        await VerifyMultiLine.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All wraps every multi-line unbraced child (SST1519) in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiLineChildFixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
            internal class C
            {
                private void M(bool x)
                {
                    {|SST1519:if|} (x)
                        System.Console
                            .WriteLine();

                    {|SST1519:while|} (x)
                        System.Console
                            .WriteLine();
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                        System.Console
                            .WriteLine();
                    }

                    while (x)
                    {
                        System.Console
                            .WriteLine();
                    }
                }
            }
            """;
        await VerifyMultiLine.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a single-line unbraced child is not flagged (left to S121).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineChildIsCleanAsync()
        => await VerifyMultiLine.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                        System.Console.WriteLine();
                }
            }
            """);

    /// <summary>Verifies an if/else chain with mixed braces is reported (SST1520) and made consistent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InconsistentBracesAddedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M(bool x)
                {
                    {|SST1520:if|} (x)
                    {
                        System.Console.WriteLine();
                    }
                    else
                        System.Console.WriteLine();
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                        System.Console.WriteLine();
                    }
                    else
                    {
                        System.Console.WriteLine();
                    }
                }
            }
            """;
        await VerifyConsistent.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All makes every inconsistent if/else chain (SST1520) consistent in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConsistentBracesFixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
            internal class C
            {
                private void M(bool x)
                {
                    {|SST1520:if|} (x)
                    {
                        System.Console.WriteLine();
                    }
                    else
                        System.Console.WriteLine();

                    {|SST1520:if|} (x)
                    {
                        System.Console.WriteLine();
                    }
                    else
                        System.Console.WriteLine();
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                        System.Console.WriteLine();
                    }
                    else
                    {
                        System.Console.WriteLine();
                    }

                    if (x)
                    {
                        System.Console.WriteLine();
                    }
                    else
                    {
                        System.Console.WriteLine();
                    }
                }
            }
            """;
        await VerifyConsistent.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an if/else chain with consistent braces is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConsistentBracesAreCleanAsync()
        => await VerifyConsistent.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M(bool x)
                {
                    if (x)
                    {
                    }
                    else
                    {
                    }
                }
            }
            """);
}
