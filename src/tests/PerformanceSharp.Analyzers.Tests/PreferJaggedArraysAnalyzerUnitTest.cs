// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1020PreferJaggedArraysAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1020PreferJaggedArraysAnalyzer"/> (PSH1020 multidimensional arrays).</summary>
public class PreferJaggedArraysAnalyzerUnitTest
{
    /// <summary>Verifies a multidimensional field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultidimensionalFieldIsReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                private readonly {|PSH1020:int[,]|} _grid = new int[3, 4];
            }
            """);

    /// <summary>Verifies a multidimensional local is reported once, on the declaration and not again on the creation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The shape is chosen once, so it is reported once. The declaration is the site the author edits,
    /// so the creation that repeats the same type stays quiet.
    /// </remarks>
    [Test]
    public async Task MultidimensionalLocalIsReportedOnceAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public int M()
                {
                    {|PSH1020:int[,]|} grid = new int[3, 4];
                    return grid[0, 0];
                }
            }
            """);

    /// <summary>Verifies an implicitly typed local reports on the creation, which is the only site there is.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VarLocalReportsOnTheCreationAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public int M()
                {
                    var grid = new {|PSH1020:int[3, 4]|};
                    return grid[0, 0];
                }
            }
            """);

    /// <summary>Verifies a multidimensional parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultidimensionalParameterIsReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public int M({|PSH1020:double[,]|} matrix) => matrix.Length;
            }
            """);

    /// <summary>Verifies a multidimensional return type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultidimensionalReturnTypeIsReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public {|PSH1020:int[,]|} M() => new {|PSH1020:int[1, 1]|};
            }
            """);

    /// <summary>Verifies a three-dimensional array is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreeDimensionalArrayIsReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public int M({|PSH1020:int[,,]|} cube) => cube.Length;
            }
            """);

    /// <summary>Verifies an implicitly typed multidimensional creation is reported, though it names no type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitMultidimensionalCreationIsReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public int M()
                {
                    var grid = {|PSH1020:new[,] { { 1, 2 }, { 3, 4 } }|};
                    return grid[0, 0];
                }
            }
            """);

    /// <summary>Verifies a jagged array — the shape the rule is asking for — is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task JaggedArrayIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                private readonly int[][] _grid = new int[3][];

                public int M(int[][] rows) => rows[0][0];
            }
            """);

    /// <summary>Verifies a single-dimensional array is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleDimensionalArrayIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public int M(int[] values) => values[0];
            }
            """);

    /// <summary>Verifies merely naming a multidimensional type is not reported — there is nothing to change there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A <c>typeof</c>, a cast, or a type argument refers to a rectangular array that already exists,
    /// very often one handed over by an API the author does not own. Reporting there would be noise
    /// nobody can act on.
    /// </remarks>
    [Test]
    public async Task NamingTheTypeWithoutDeclaringItIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public Type M() => typeof(int[,]);

                public int N(object boxed) => ((int[,])boxed).Length;

                public int O(List<int[,]> grids) => grids.Count;
            }
            """);

    /// <summary>
    /// Verifies the rule behaves identically against netstandard2.0, because it suggests no API at all.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// PSH1020 is the one rule in this batch with nothing to prove the existence of: a jagged array is
    /// as available on .NET Framework as on .NET 10, so there is no gate to trip and no target on which
    /// the suggestion is unactionable. Being silent on netstandard2.0 would be a false negative, not a
    /// safety measure — so the rule reports there, and this test pins that down deliberately.
    /// </remarks>
    [Test]
    public async Task NetStandard20StillReportsAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = """
                       public class C
                       {
                           public int M({|PSH1020:int[,]|} grid) => grid[0, 0];
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        await test.RunAsync(CancellationToken.None);
    }
}
