// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using VerifyKeyWrap = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1010HandRolledKeyWrapAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for the hand-rolled key-wrap rule (SES1010).</summary>
public class Ses1010HandRolledKeyWrapAnalyzerUnitTest
{
    /// <summary>Verifies typed bytes match while wrong values and nonconstant elements are ignored.</summary>
    /// <param name="elements">The eight initializer elements.</param>
    /// <param name="reported">Whether the sequence is the integrity value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("(byte)166, (byte)166, (byte)166, (byte)166, (byte)166, (byte)166, (byte)166, (byte)166", true)]
    [Arguments("(byte)165, 166, 166, 166, 166, 166, 166, 166", false)]
    [Arguments("165, 166, 166, 166, 166, 166, 166, 166", false)]
    [Arguments("166L, 166, 166, 166, 166, 166, 166, 166", false)]
    [Arguments("null, 166, 166, 166, 166, 166, 166, 166", false)]
    [Arguments("value, 166, 166, 166, 166, 166, 166, 166", false)]
    public async Task InitializerRequiresEightConstantBytesAsync(string elements, bool reported)
    {
        var source = $"class C {{ object[] M(int value) => new object[] {{ {elements} }}; }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Ses1010HandRolledKeyWrapAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(reported ? 1 : 0);
        if (reported)
        {
            await Assert.That(diagnostics[0].Id).IsEqualTo("SES1010");
        }
    }

    /// <summary>Verifies the byte form also respects framework availability.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ByteInitializerWithoutKeyWrapIsCleanAsync() =>
        RunAsync("class C { byte[] M() => new byte[] { 166, 166, 166, 166, 166, 166, 166, 166 }; }", AnalyzerFrameworks.Net80);

    /// <summary>Verifies absent cryptography types and signed numeric near misses are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingAesAndSignedLiteralAreCleanAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { ulong Value = 0xA6A6A6A6A6A6A6A6; long Signed = 166L; }");
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], [RuntimeMetadataReferences.CoreLibrary], new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Ses1010HandRolledKeyWrapAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies the integrity check value written as one constant is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SixtyFourBitConstantReportedAsync() =>
        RunAsync(
            """
            public class C
            {
                private const ulong InitialValue = {|SES1010:0xA6A6A6A6A6A6A6A6|};

                public ulong Read() => InitialValue;
            }
            """);

    /// <summary>Verifies the integrity check value written as its eight bytes is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ByteArrayReportedAsync() =>
        RunAsync(
            """
            public class C
            {
                private static readonly byte[] InitialValue = new byte[] {|SES1010:{ 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6 }|};

                public byte[] Read() => InitialValue;
            }
            """);

    /// <summary>Verifies a shorter run of the same byte is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ShorterByteRunIsCleanAsync() =>
        RunAsync(
            """
            public class C
            {
                private static readonly byte[] Pattern = new byte[] { 0xA6, 0xA6, 0xA6, 0xA6 };

                public byte[] Read() => Pattern;
            }
            """);

    /// <summary>Verifies an unrelated 64-bit constant is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedConstantIsCleanAsync() =>
        RunAsync(
            """
            public class C
            {
                private const ulong Mask = 0xFFFF_FFFF_FFFF_FFFF;

                public ulong Read() => Mask;
            }
            """);

    /// <summary>Verifies nothing is reported on a framework without the key-wrap API.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WithoutKeyWrapApiIsCleanAsync() =>
        RunAsync(
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
        var test = new VerifyKeyWrap.Test { ReferenceAssemblies = referenceAssemblies ?? DotNet11ReferenceAssemblies.Net110, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }
}
