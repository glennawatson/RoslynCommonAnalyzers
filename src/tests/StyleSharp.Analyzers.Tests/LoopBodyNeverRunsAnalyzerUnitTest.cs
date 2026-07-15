// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLoop = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.LoopConditionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2413 (a for loop's condition is already false at its starting value).</summary>
public class LoopBodyNeverRunsAnalyzerUnitTest
{
    /// <summary>Verifies an ascending loop whose start is past its bound is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StartPastBoundIsReportedAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    for (var i = 10; {|SST2413:i < 5|}; i++)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a descending loop whose start is below its bound is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DescendingStartBelowBoundIsReportedAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    for (var i = 5; {|SST2413:i > 10|}; i--)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies an inclusive bound that still folds false is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InclusiveBoundThatFoldsFalseIsReportedAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    for (var i = 5; {|SST2413:i <= 4|}; i++)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a loop whose condition holds at the start is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionTrueAtStartIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    for (var i = 0; i < 5; i++)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a bound that is a collection count is clean: an empty collection is not a bug.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionCountBoundIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(List<int> items)
                {
                    for (var i = 0; i < items.Count; i++)
                    {
                        Console.WriteLine(items[i]);
                    }
                }
            }
            """);

    /// <summary>Verifies a non-constant bound is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantBoundIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int n)
                {
                    for (var i = 10; i < n; i++)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);
}
