// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1013Utf8SpanPropertyAnalyzer,
    PerformanceSharp.Analyzers.Psh1013Utf8SpanPropertyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1013Utf8SpanPropertyAnalyzer"/> (PSH1013 UTF-8 span properties).</summary>
public class Utf8SpanPropertyAnalyzerUnitTest
{
    /// <summary>Verifies a u8 ToArray field with span-only reads is flagged and becomes a property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToArrayFieldIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private static readonly byte[] {|PSH1013:Prefix|} = "v1:"u8.ToArray();

                                  public bool M(ReadOnlySpan<byte> input) => input.StartsWith(Prefix);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private static ReadOnlySpan<byte> Prefix => "v1:"u8;

                                       public bool M(ReadOnlySpan<byte> input) => input.StartsWith(Prefix);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a spread-built field indexed and measured is flagged and becomes a property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpreadFieldWithElementReadsIsFlaggedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private static readonly byte[] {|PSH1013:Marker|} = [.. "ok"u8];

                                  public int M() => Marker.Length + Marker[0];
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private static ReadOnlySpan<byte> Marker => "ok"u8;

                                       public int M() => Marker.Length + Marker[0];
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a field passed to a byte-array parameter stays clean; the property would not compile.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArrayArgumentUsageIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                private static readonly byte[] Marker = "ok"u8.ToArray();

                public void M() => Use(Marker);

                private static void Use(byte[] data)
                {
                }
            }
            """);

    /// <summary>Verifies a mutated field stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MutatedFieldIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                private static readonly byte[] Marker = "ok"u8.ToArray();

                public void M() => Marker[0] = 1;
            }
            """);

    /// <summary>Checks every field-shape gate without requiring malformed declarations to compile.</summary>
    /// <param name="declaration">The field declaration.</param>
    /// <param name="expected">Whether it can be a span-property candidate.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("static readonly byte[] Marker = null;", true)]
    [Arguments("private static readonly byte[] Marker = null;", true)]
    [Arguments("static readonly byte[] Marker;", false)]
    [Arguments("static readonly byte[] Marker = null, Other = null;", false)]
    [Arguments("readonly byte[] Marker = null;", false)]
    [Arguments("static byte[] Marker = null;", false)]
    [Arguments("public static readonly byte[] Marker = null;", false)]
    [Arguments("internal static readonly byte[] Marker = null;", false)]
    [Arguments("protected static readonly byte[] Marker = null;", false)]
    [Arguments("static readonly byte Marker = 0;", false)]
    [Arguments("static readonly byte[,] Marker = null;", false)]
    [Arguments("static readonly byte[][] Marker = null;", false)]
    [Arguments("static readonly int[] Marker = null;", false)]
    [Arguments("static readonly System.Byte[] Marker = null;", false)]
    public async Task CandidateShapeRequiresPrivateReadonlyByteArrayAsync(string declaration, bool expected)
    {
        var field = (FieldDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(declaration)!;
        await Assert.That(Psh1013Utf8SpanPropertyAnalyzer.HasCandidateShape(field)).IsEqualTo(expected);
    }

    /// <summary>Checks initializer recognition accepts only direct UTF-8 literals.</summary>
    /// <param name="initializer">The initializer expression.</param>
    /// <param name="expected">Whether the expression exposes a UTF-8 literal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("\"ok\"u8.ToArray()", true)]
    [Arguments("[.. \"ok\"u8]", true)]
    [Arguments("null", false)]
    [Arguments("ToArray()", false)]
    [Arguments("\"ok\"u8.Other()", false)]
    [Arguments("\"ok\"u8.ToArray(1)", false)]
    [Arguments("\"ok\".ToArray()", false)]
    [Arguments("value.ToArray()", false)]
    [Arguments("[]", false)]
    [Arguments("[1]", false)]
    [Arguments("[.. value]", false)]
    [Arguments("[.. \"ok\"u8, 1]", false)]
    public async Task Utf8SourceRequiresOneLiteralAsync(string initializer, bool expected)
    {
        var expression = SyntaxFactory.ParseExpression(initializer);
        await Assert.That(Psh1013Utf8SpanPropertyAnalyzer.TryGetUtf8Source(expression) is not null).IsEqualTo(expected);
    }

    /// <summary>Checks span-compatible reads and escaping or unresolved uses.</summary>
    /// <param name="members">The members that consume the field.</param>
    /// <param name="expected">The expected diagnostic count.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("void M() { foreach (var item in Marker) { } }", 1)]
    [Arguments("int M() => C.Marker.Length;", 1)]
    [Arguments("void M() { byte value = 0; value = Marker[0]; }", 1)]
    [Arguments("void M() => Use(Marker, Marker); void Use(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b) { }", 1)]
    [Arguments("void M() { Use(Marker); Use(Marker); } void Use(ReadOnlySpan<byte> a) { }", 1)]
    [Arguments("int M() => Marker.Rank;", 0)]
    [Arguments("byte[] M() => Marker;", 0)]
    [Arguments("void M() => Use(in Marker); void Use(in byte[] a) { }", 0)]
    [Arguments("void M() => Missing(Marker);", 0)]
    [Arguments("void M() => _ = new D(Marker); class D { public D(byte[] a) { } }", 0)]
    [Arguments("void M() => Use(Marker); void Use<T>(T a) { }", 0)]
    [Arguments("void M() => Use(Marker); void Use(Span<byte> a) { }", 0)]
    [Arguments("void M() => Use(Marker); void Use(ReadOnlySpan<int> a) { }", 0)]
    [Arguments("void M() { foreach (Marker item in new object[0]) { } }", 0)]
    [Arguments("void Marker() { }", 1)]
    [Arguments("object M(D other) => other[Marker]; class D { public object this[byte[] value] => null; }", 0)]
    [Arguments(
        """
        void M() => Use(Marker);
        void Use(ReadOnlySpan<byte> value) { }
        class ReadOnlySpan<T> { public static implicit operator ReadOnlySpan<T>(byte[] value) => null; }
        """,
        0)]
    [Arguments(
        """
        void M() => Use(Marker);
        void Use(ReadOnlySpan<int> value) { }
        class ReadOnlySpan<T> { public static implicit operator ReadOnlySpan<T>(byte[] value) => null; }
        """,
        0)]
    [Arguments(
        """
        void M() => Use(Marker);
        void Use(ReadOnlySpan value) { }
        class ReadOnlySpan { public static implicit operator ReadOnlySpan(byte[] value) => null; }
        """,
        0)]
    [Arguments(
        """
        void M() => Use(Marker);
        void Use(ReadOnlySpan<byte, byte> value) { }
        class ReadOnlySpan<T, U> { public static implicit operator ReadOnlySpan<T, U>(byte[] value) => null; }
        """,
        0)]
    public async Task FieldUsesMustRemainValidForSpansAsync(string members, int expected)
    {
        var source = $$"""
            using System;
            class C
            {
                private static readonly byte[] Marker = "ok"u8.ToArray();
                {{members}}
            }
            """;
        var compilation = CSharpCompilation.Create(nameof(FieldUsesMustRemainValidForSpansAsync), [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1013Utf8SpanPropertyAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(expected);
        await Assert.That(diagnostics.All(static diagnostic => diagnostic.Id == "PSH1013")).IsTrue();
    }

    /// <summary>Checks partial declarations, unsupported initializers, and missing framework spans are ignored.</summary>
    /// <param name="source">The complete source.</param>
    /// <param name="hasFramework">Whether framework references are available.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("partial class C { static readonly byte[] Marker = \"ok\"u8.ToArray(); }", true)]
    [Arguments("class C { static readonly byte[] Marker = new byte[0]; }", true)]
    [Arguments("namespace N { static readonly byte[] Marker = \"ok\"u8.ToArray(); }", true)]
    [Arguments("class C { static readonly byte[] Marker = \"ok\"u8.ToArray(); }", false)]
    public async Task UnsupportedFieldContextIsCleanAsync(string source, bool hasFramework)
    {
        var compilation = CSharpCompilation.Create(nameof(UnsupportedFieldContextIsCleanAsync), [CSharpSyntaxTree.ParseText(source)], hasFramework ? RuntimeMetadataReferences.Platform : []);
        var diagnostics = await compilation.WithAnalyzers([new Psh1013Utf8SpanPropertyAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
