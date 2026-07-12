// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyOverwrittenElement = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1487OverwrittenCollectionElementAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1487 (collection elements should not be overwritten before they are read).</summary>
public class OverwrittenCollectionElementAnalyzerUnitTest
{
    /// <summary>Verifies the same dictionary key assigned twice in a row is reported on the lost write.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedKeyAssignmentIsReportedAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void Fill(Dictionary<string, int> map)
                {
                    {|SST1487:map["alpha"]|} = 1;
                    map["alpha"] = 2;
                }
            }
            """);

    /// <summary>Verifies an index that does not advance is reported even when both values are computed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedIndexAssignmentWithComputedValuesIsReportedAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Fill(int[] values, int i)
                {
                    {|SST1487:values[i]|} = ComputeX();
                    values[i] = ComputeY();
                }

                private static int ComputeX() => 1;

                private static int ComputeY() => 2;
            }
            """);

    /// <summary>Verifies a qualified receiver is matched just as a bare one is.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task QualifiedReceiverIsReportedAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                private readonly Dictionary<string, int> _map = new Dictionary<string, int>();

                public void Fill()
                {
                    {|SST1487:this._map["alpha"]|} = 1;
                    this._map["alpha"] = 2;
                }
            }
            """);

    /// <summary>Verifies three writes to one slot report the two that are lost.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThreeWritesReportTheTwoLostOnesAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Fill(int[] values, int i)
                {
                    {|SST1487:values[i]|} = 1;
                    {|SST1487:values[i]|} = 2;
                    values[i] = 3;
                }
            }
            """);

    /// <summary>Verifies statements written directly into a switch section are checked too.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StatementsInASwitchSectionAreReportedAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Fill(int[] values, int i, int mode)
                {
                    switch (mode)
                    {
                        case 0:
                            {|SST1487:values[i]|} = 1;
                            values[i] = 2;
                            break;
                    }
                }
            }
            """);

    /// <summary>Verifies different indexes are different slots.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DifferentIndexesAreCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Fill(int[] values, int i)
                {
                    values[i] = 1;
                    values[i + 1] = 2;
                }
            }
            """);

    /// <summary>Verifies different receivers are different collections.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DifferentReceiversAreCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Fill(int[] first, int[] second, int i)
                {
                    first[i] = 1;
                    second[i] = 2;
                }
            }
            """);

    /// <summary>Verifies two writes separated by another statement are not an obvious repeat.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonAdjacentWritesAreCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void Fill(int[] values, int i)
                {
                    values[i] = 1;
                    Console.WriteLine(values[i]);
                    values[i] = 2;
                }
            }
            """);

    /// <summary>Verifies a compound assignment reads before it writes, so nothing is lost.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CompoundAssignmentsAreCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Accumulate(int[] sums, int i, int x, int y)
                {
                    sums[i] += x;
                    sums[i] += y;
                }

                public void Mix(int[] sums, int i, int x, int y)
                {
                    sums[i] = x;
                    sums[i] += y;
                }

                public void Follow(int[] sums, int i, int x, int y)
                {
                    sums[i] |= x;
                    sums[i] = y;
                }
            }
            """);

    /// <summary>Verifies a null-coalescing assignment only writes when the element is empty.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullCoalescingAssignmentsAreCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void Fill(Dictionary<string, string> map)
                {
                    map["alpha"] ??= "first";
                    map["alpha"] ??= "second";
                }

                public void Seed(Dictionary<string, string> map)
                {
                    map["alpha"] = "first";
                    map["alpha"] ??= "second";
                }
            }
            """);

    /// <summary>Verifies a right-hand side that reads the element is a read-modify-write, not a lost write.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RightHandSideReadingTheElementIsCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Bump(int[] values, int i)
                {
                    values[i] = 1;
                    values[i] = values[i] + 1;
                }
            }
            """);

    /// <summary>Verifies a right-hand side that reads the collection at all leaves the first write alive.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RightHandSideReadingTheCollectionIsCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void Fill(int[] values, int i)
                {
                    values[i] = 1;
                    values[i] = values.Length;
                }

                public void Count(Dictionary<string, int> map)
                {
                    map["alpha"] = 1;
                    map["alpha"] = map.Count;
                }
            }
            """);

    /// <summary>Verifies an index that can answer differently on a second call is never matched.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SideEffectingIndexIsCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                private int _next;

                public void Fill(Dictionary<int, int> map)
                {
                    map[Next()] = 1;
                    map[Next()] = 2;
                }

                public void Advance(int[] values, int i)
                {
                    values[i++] = 1;
                    values[i++] = 2;
                }

                private int Next() => _next++;
            }
            """);

    /// <summary>Verifies a receiver that can answer differently on a second call is never matched.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SideEffectingReceiverIsCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Fill(int i)
                {
                    Buffer()[i] = 1;
                    Buffer()[i] = 2;
                }

                private static int[] Buffer() => new int[4];
            }
            """);

    /// <summary>Verifies a nested element access is not provably the same slot twice.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>An inner indexer is a call this rule cannot see through, so it stays quiet rather than guess.</remarks>
    [Test]
    public async Task NestedElementAccessIsCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Fill(int[][] grid, int i, int j)
                {
                    grid[i][j] = 1;
                    grid[i][j] = 2;
                }
            }
            """);

    /// <summary>Verifies a repeated property assignment is out of scope; only indexers are matched.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedPropertyAssignmentIsCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Width { get; set; }

                public void Resize(C target)
                {
                    target.Width = 1;
                    target.Width = 2;
                }
            }
            """);

    /// <summary>Verifies two writes that are only adjacent in this build configuration are left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The skipped region could read the element, so the first write is not provably dead.</remarks>
    [Test]
    public async Task WritesSeparatedByAnInactiveRegionAreCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void Fill(int[] values, int i)
                {
                    values[i] = 1;
            #if EXTRA_LOGGING
                    Console.WriteLine(values[i]);
            #endif
                    values[i] = 2;
                }
            }
            """);

    /// <summary>Verifies a lone write in a block is never reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SingleWriteIsCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Fill(int[] values, int i)
                {
                    values[i] = 1;
                }
            }
            """);

    /// <summary>Verifies writes in separate blocks are not adjacent statements.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WritesInSeparateBlocksAreCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Fill(int[] values, int i, bool flag)
                {
                    if (flag)
                    {
                        values[i] = 1;
                    }

                    values[i] = 2;
                }
            }
            """);

    /// <summary>Verifies a loop that writes one slot per iteration is never reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LoopBodyWritingOneSlotIsCleanAsync()
        => await VerifyOverwrittenElement.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Fill(int[] values)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = i;
                    }
                }
            }
            """);
}
