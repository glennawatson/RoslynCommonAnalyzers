// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConsistent = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ConsistentBracesAnalyzer,
    StyleSharp.Analyzers.ConsistentBracesCodeFixProvider>;
using VerifyMultiLine = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MultiLineChildBraceAnalyzer,
    StyleSharp.Analyzers.MultiLineChildBraceCodeFixProvider>;

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
