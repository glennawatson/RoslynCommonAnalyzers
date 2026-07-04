// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1117UseIsEmptyAnalyzer,
    PerformanceSharp.Analyzers.Psh1117UseIsEmptyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1117UseIsEmptyAnalyzer"/> (PSH1117 IsEmpty).</summary>
public class UseIsEmptyAnalyzerUnitTest
{
    /// <summary>Verifies a concurrent queue count comparison is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcurrentQueueCountComparisonIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Concurrent;

                              public class C
                              {
                                  public bool M(ConcurrentQueue<int> queue) => {|PSH1117:queue.Count == 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Concurrent;

                                   public class C
                                   {
                                       public bool M(ConcurrentQueue<int> queue) => queue.IsEmpty;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a span length check maps to the negated property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpanLengthGreaterThanZeroBecomesNegatedIsEmptyAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(ReadOnlySpan<char> text) => {|PSH1117:text.Length > 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(ReadOnlySpan<char> text) => !text.IsEmpty;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a reversed operand order is recognized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReversedComparisonIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Immutable;

                              public class C
                              {
                                  public bool M(ImmutableArray<int> values) => {|PSH1117:0 != values.Length|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Immutable;

                                   public class C
                                   {
                                       public bool M(ImmutableArray<int> values) => !values.IsEmpty;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a list stays clean; it has no IsEmpty property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListCountComparisonIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public bool M(List<int> values) => values.Count == 0;
            }
            """);

    /// <summary>Verifies a non-emptiness comparison stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThresholdComparisonIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public bool M(ReadOnlySpan<char> text) => text.Length > 1;
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
