// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using VerifyKeyWrap = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1010HandRolledKeyWrapAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for the hand-rolled key-wrap rule (SES1010).</summary>
public class Ses1010HandRolledKeyWrapAnalyzerUnitTest
{
    /// <summary>Verifies the integrity check value written as one constant is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SixtyFourBitConstantReportedAsync()
        => await RunAsync(
            """
            public class C
            {
                private const ulong InitialValue = {|SES1010:0xA6A6A6A6A6A6A6A6|};

                public ulong Read() => InitialValue;
            }
            """);

    /// <summary>Verifies the integrity check value written as its eight bytes is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ByteArrayReportedAsync()
        => await RunAsync(
            """
            public class C
            {
                private static readonly byte[] InitialValue = new byte[] {|SES1010:{ 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6 }|};

                public byte[] Read() => InitialValue;
            }
            """);

    /// <summary>Verifies a shorter run of the same byte is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShorterByteRunIsCleanAsync()
        => await RunAsync(
            """
            public class C
            {
                private static readonly byte[] Pattern = new byte[] { 0xA6, 0xA6, 0xA6, 0xA6 };

                public byte[] Read() => Pattern;
            }
            """);

    /// <summary>Verifies an unrelated 64-bit constant is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedConstantIsCleanAsync()
        => await RunAsync(
            """
            public class C
            {
                private const ulong Mask = 0xFFFF_FFFF_FFFF_FFFF;

                public ulong Read() => Mask;
            }
            """);

    /// <summary>Verifies nothing is reported on a framework without the key-wrap API.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WithoutKeyWrapApiIsCleanAsync()
        => await RunAsync(
            """
            public class C
            {
                private const ulong InitialValue = 0xA6A6A6A6A6A6A6A6;

                public ulong Read() => InitialValue;
            }
            """,
            ReferenceAssemblies.Net.Net80);

    /// <summary>Runs the analyzer verifier against the requested reference assemblies.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="referenceAssemblies">The framework to compile the test source against.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, ReferenceAssemblies? referenceAssemblies = null)
    {
        var test = new VerifyKeyWrap.Test
        {
            ReferenceAssemblies = referenceAssemblies ?? DotNet11ReferenceAssemblies.Net110,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
