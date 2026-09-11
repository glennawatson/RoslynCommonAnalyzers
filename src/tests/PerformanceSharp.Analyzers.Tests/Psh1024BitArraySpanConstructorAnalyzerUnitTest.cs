// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using VerifyBitArray = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1024BitArraySpanConstructorAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for the BitArray span-constructor rule (PSH1024).</summary>
public class Psh1024BitArraySpanConstructorAnalyzerUnitTest
{
    /// <summary>Verifies an implicitly typed temporary array is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImplicitTemporaryArrayReportedAsync() =>
        RunAsync(
            """
            using System.Collections;

            public class C
            {
                public BitArray M() => new BitArray({|PSH1024:new[] { true, false }|});
            }
            """);

    /// <summary>Verifies an explicitly typed temporary array is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExplicitTemporaryArrayReportedAsync() =>
        RunAsync(
            """
            using System.Collections;

            public class C
            {
                public BitArray M() => new BitArray({|PSH1024:new byte[] { 1, 2, 3 }|});
            }
            """);

    /// <summary>Verifies an array the caller already holds is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExistingArrayIsCleanAsync() =>
        RunAsync(
            """
            using System.Collections;

            public class C
            {
                public BitArray M(bool[] flags) => new BitArray(flags);
            }
            """);

    /// <summary>Verifies the length constructor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LengthConstructorIsCleanAsync() =>
        RunAsync(
            """
            using System.Collections;

            public class C
            {
                public BitArray M() => new BitArray(8);
            }
            """);

    /// <summary>Verifies nothing is reported on a framework without a span constructor.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WithoutSpanConstructorIsCleanAsync() =>
        RunAsync(
            """
            using System.Collections;

            public class C
            {
                public BitArray M() => new BitArray(new[] { true, false });
            }
            """,
            ReferenceAssemblies.Net.Net80);

    /// <summary>Runs the analyzer verifier against the requested reference assemblies.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="referenceAssemblies">The framework to compile the test source against.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, ReferenceAssemblies? referenceAssemblies = null)
    {
        var test = new VerifyBitArray.Test { ReferenceAssemblies = referenceAssemblies ?? DotNet11ReferenceAssemblies.Net110, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }
}
