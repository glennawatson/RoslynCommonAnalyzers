// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1125MultipleEnumerationAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1125MultipleEnumerationAnalyzer"/> (PSH1125 multiple enumeration).</summary>
public class MultipleEnumerationAnalyzerUnitTest
{
    /// <summary>Verifies two foreach loops over an IEnumerable parameter are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoForEachLoopsAreReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    var total = 0;
                    foreach (var value in source)
                    {
                        total += value;
                    }

                    foreach (var value in {|PSH1125:source|})
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies a Count() followed by a foreach over the same parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountThenForEachIsReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    var total = source.Count();
                    foreach (var value in {|PSH1125:source|})
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies an Any() guard followed by a First() on the same parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnyThenFirstIsReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source) => source.Any() ? {|PSH1125:source|}.First() : 0;
            }
            """);

    /// <summary>Verifies a deferred chain ending in an eager call counts as a walk of its root.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeferredChainEndingInEagerCallIsReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    var high = source.Where(x => x > 10).Count();
                    var low = {|PSH1125:source|}.Where(x => x < 10).Count();
                    return high + low;
                }
            }
            """);

    /// <summary>Verifies a lazily-initialized IEnumerable local walked twice is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LazyLocalWalkedTwiceIsReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    IEnumerable<int> filtered = source.Where(x => x > 0);
                    var count = filtered.Count();
                    foreach (var value in {|PSH1125:filtered|})
                    {
                        count += value;
                    }

                    return count;
                }
            }
            """);

    /// <summary>Verifies a materialized collection type is never reported, because re-walking it is safe.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListParameterIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(List<int> source)
                {
                    var total = source.Count();
                    foreach (var value in source)
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies an IReadOnlyCollection parameter is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyCollectionParameterIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IReadOnlyCollection<int> source)
                {
                    var total = source.Count();
                    foreach (var value in source)
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies an array parameter is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayParameterIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Linq;

            public class C
            {
                public int M(int[] source)
                {
                    var total = source.Count();
                    foreach (var value in source)
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies a local materialized with ToList is not reported, even when typed as IEnumerable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MaterializedLocalIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    IEnumerable<int> cached = source.ToList();
                    var count = cached.Count();
                    foreach (var value in cached)
                    {
                        count += value;
                    }

                    return count;
                }
            }
            """);

    /// <summary>Verifies a parameter reassigned to a materialized copy is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReassignedParameterIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    source = source.ToList();
                    var total = source.Count();
                    foreach (var value in source)
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies two walks on opposite arms of an if/else are not reported, because only one runs.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WalksOnOppositeIfElseArmsAreNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source, bool flag)
                {
                    if (flag)
                    {
                        return source.Count();
                    }
                    else
                    {
                        return source.Sum();
                    }
                }
            }
            """);

    /// <summary>Verifies two walks in different switch sections are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WalksInDifferentSwitchSectionsAreNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source, int mode)
                {
                    switch (mode)
                    {
                        case 0:
                            return source.Count();
                        default:
                            return source.Sum();
                    }
                }
            }
            """);

    /// <summary>Verifies a single walk is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleWalkIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source) => source.Count();
            }
            """);

    /// <summary>Verifies a deferred-only chain is not treated as a walk.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeferredOnlyChainsAreNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public IEnumerable<int> M(IEnumerable<int> source)
                    => source.Where(x => x > 0).Select(x => x + source.Count());
            }
            """);

    /// <summary>Verifies handing the sequence to another method is not treated as a walk.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PassingToAnotherMethodIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> source)
                {
                    Consume(source);
                    return source.Count();
                }

                private static void Consume(IEnumerable<int> values)
                {
                }
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
