// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1122UseSortedSetExtremePropertyAnalyzer,
    PerformanceSharp.Analyzers.Psh1122UseSortedSetExtremePropertyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1122UseSortedSetExtremePropertyAnalyzer"/> (PSH1122 sorted-set Min and Max).</summary>
public class UseSortedSetExtremePropertyAnalyzerUnitTest
{
    /// <summary>Verifies Min on a sorted set is flagged and rewritten to the property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SortedSetMinIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(SortedSet<int> values) => values.{|PSH1122:Min|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(SortedSet<int> values) => values.Min;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Max on a sorted set is flagged and rewritten to the property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SortedSetMaxIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(SortedSet<int> values) => values.{|PSH1122:Max|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(SortedSet<int> values) => values.Max;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Min on an immutable sorted set is flagged and rewritten to the property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImmutableSortedSetMinIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Immutable;
                              using System.Linq;

                              public class C
                              {
                                  public int M(ImmutableSortedSet<int> values) => values.{|PSH1122:Min|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Immutable;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(ImmutableSortedSet<int> values) => values.Min;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a property read on a member-access receiver is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberReceiverIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  private readonly SortedSet<int> _values = new();

                                  public int Smallest => _values.{|PSH1122:Min|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       private readonly SortedSet<int> _values = new();

                                       public int Smallest => _values.Min;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the selector overload stays clean; it asks a different question.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelectorOverloadIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(SortedSet<int> values) => values.Min(x => x % 10);
            }
            """);

    /// <summary>Verifies the comparer overload stays clean; the set is ordered by its own comparer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparerOverloadIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(SortedSet<int> values) => values.Min(Comparer<int>.Default);
            }
            """);

    /// <summary>Verifies an unordered set stays clean; it has no Min property to read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HashSetReceiverIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(HashSet<int> values) => values.Min();
            }
            """);

    /// <summary>Verifies a set exposed through an interface stays clean; the property is not on the interface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceReceiverIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(ISet<int> values) => values.Min();
            }
            """);

    /// <summary>Verifies a Queryable source stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QueryableSourceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Linq;

            public class C
            {
                public int M(IQueryable<int> values) => values.Min();
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string? fixedSource = null)
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
