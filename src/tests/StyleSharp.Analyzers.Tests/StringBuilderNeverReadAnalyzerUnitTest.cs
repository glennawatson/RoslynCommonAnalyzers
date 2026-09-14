// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using VerifyStringBuilder = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2408StringBuilderNeverReadAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2408 (a StringBuilder that is filled and never read).</summary>
public class StringBuilderNeverReadAnalyzerUnitTest
{
    /// <summary>Checks declaration spellings, scope boundaries, and inferred non-builder locals.</summary>
    /// <param name="source">The complete source, including intentionally incomplete declarations.</param>
    /// <param name="expected">The expected diagnostic count.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() { System.Text.StringBuilder builder = new(); builder.Append(1); } }", 1)]
    [Arguments("class C { void M() { int value = 0; var other = new object(); var missing; } }", 0)]
    [Arguments("class C { void M() { var builder = Make(); } object Make() => null; }", 0)]
    [Arguments("class C { void M(int value) { switch (value) { case 0: StringBuilder builder = new(); builder.Append(1); break; } } }", 1)]
    [Arguments("StringBuilder builder = new(); builder.Append(1);", 0)]
    [Arguments("class C { void M() { StringBuilder first = new(), second = new(); first.Append(1); second.Clear(); } }", 1)]
    [Arguments("class StringBuilder { public void Append(int value) {} } class C { void M() { global::StringBuilder builder = new(); builder.Append(1); } }", 0)]
    [Arguments("namespace Other.Text { class StringBuilder { public void Append(int value) {} } class C { void M() { StringBuilder builder = new(); builder.Append(1); } } }", 0)]
    [Arguments("namespace Other.System.Text { class StringBuilder { public void Append(int value) {} } class C { void M() { StringBuilder builder = new(); builder.Append(1); } } }", 0)]
    [Arguments("class C { void M() { StringBuilder[] builders = []; } }", 0)]
    [Arguments("class C { void M<StringBuilder>() { StringBuilder builder = default; } }", 0)]
    [Arguments("using StringBuilder = System.Int32; class C { void M() { StringBuilder builder = 0; } }", 0)]
    public async Task DeclarationShapeAndScopeDetermineEligibilityAsync(string source, int expected)
    {
        var compilation = CSharpCompilation.Create(
            nameof(DeclarationShapeAndScopeDetermineEligibilityAsync),
            [CSharpSyntaxTree.ParseText($"using System.Text; {source}")],
            RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Sst2408StringBuilderNeverReadAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(expected);
        await Assert.That(diagnostics.All(static diagnostic => diagnostic.Id == "SST2408")).IsTrue();
    }

    /// <summary>Checks every mutator and the reads that stop the local scan.</summary>
    /// <param name="statement">The statement following an append.</param>
    /// <param name="expected">The expected diagnostic count.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("builder.AppendLine();", 1)]
    [Arguments("builder.AppendFormat(\"{0}\", 1);", 1)]
    [Arguments("builder.AppendJoin(\",\", new[] { 1, 2 });", 1)]
    [Arguments("builder.Insert(0, \"x\");", 1)]
    [Arguments("builder.Remove(0, 1);", 1)]
    [Arguments("builder.Replace(\"x\", \"y\");", 1)]
    [Arguments("builder.Clear();", 1)]
    [Arguments("builder = new StringBuilder();", 1)]
    [Arguments("builder += new StringBuilder();", 0)]
    [Arguments("_ = builder.Append(2);", 0)]
    [Arguments("_ = builder.Length;", 0)]
    [Arguments("_ = builder.Append;", 0)]
    [Arguments("builder.ToString();", 0)]
    [Arguments("builder.EnsureCapacity(20);", 0)]
    [Arguments("builder.Other();", 0)]
    [Arguments("builder.AppendX();", 0)]
    [Arguments("builder.AppendJoiX();", 0)]
    [Arguments("builder.AppendFormaX();", 0)]
    [Arguments("builder.AlterX();", 0)]
    [Arguments("builder.Invert();", 0)]
    [Arguments("builder.Return();", 0)]
    [Arguments("builder.OtherX();", 0)]
    [Arguments("builder.AppendLinX();", 0)]
    [Arguments("builder.AppendXXXX();", 0)]
    public async Task DiscardedMutationsDoNotReadTheContentsAsync(string statement, int expected)
    {
        var source = $$"""
            using System.Text;
            class C { void M() { var builder = new StringBuilder(); builder.Append(1); {{statement}} } }
            """;
        var compilation = CSharpCompilation.Create(nameof(DiscardedMutationsDoNotReadTheContentsAsync), [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Sst2408StringBuilderNeverReadAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(expected);
        await Assert.That(diagnostics.All(static diagnostic => diagnostic.Id == "SST2408")).IsTrue();
    }

    /// <summary>Verifies a builder that is appended to and never read is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnreadBuilderIsReportedAsync() =>
        VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public void M(string[] lines)
                {
                    var {|SST2408:builder|} = new StringBuilder();
                    foreach (var line in lines)
                    {
                        builder.AppendLine(line);
                    }
                }
            }
            """);

    /// <summary>Verifies a chain of discarded appends is still no read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ChainedAppendsAreStillUnreadAsync() =>
        VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public void M(string name)
                {
                    StringBuilder {|SST2408:builder|} = new StringBuilder();
                    builder.Append("name: ").Append(name).AppendLine();
                    builder.Clear();
                }
            }
            """);

    /// <summary>Verifies a builder whose contents are collected is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BuilderThatIsReadIsCleanAsync() =>
        VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public string M(string[] lines)
                {
                    var builder = new StringBuilder();
                    foreach (var line in lines)
                    {
                        builder.AppendLine(line);
                    }

                    return builder.ToString();
                }
            }
            """);

    /// <summary>Verifies handing the builder to something else counts as a read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BuilderPassedOnIsCleanAsync() =>
        VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public void M(string name)
                {
                    var builder = new StringBuilder();
                    builder.Append(name);
                    Write(builder);
                }

                private static void Write(StringBuilder builder)
                {
                }
            }
            """);

    /// <summary>Verifies reading a property of the builder counts as a read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BuilderLengthReadIsCleanAsync() =>
        VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System;
            using System.Text;

            public sealed class C
            {
                public void M(string name)
                {
                    var builder = new StringBuilder();
                    builder.Append(name);
                    Console.WriteLine(builder.Length);
                }
            }
            """);

    /// <summary>Verifies a builder that is returned is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReturnedBuilderIsCleanAsync() =>
        VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public StringBuilder M(string name)
                {
                    var builder = new StringBuilder();
                    builder.Append(name);
                    return builder;
                }
            }
            """);

    /// <summary>Verifies a builder that is never appended to is not this rule's business.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BuilderWithNoAppendIsCleanAsync() =>
        VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public void M()
                {
                    var builder = new StringBuilder();
                    builder.Clear();
                }
            }
            """);

    /// <summary>Verifies a field is left alone: any other member of the type may read it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BuilderFieldIsCleanAsync() =>
        VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                private readonly StringBuilder _builder = new();

                public void Add(string line) => _builder.AppendLine(line);
            }
            """);

    /// <summary>Verifies a type of the project's own with the same name is not measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedProjectTypeIsCleanAsync() =>
        VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            namespace Custom
            {
                public sealed class StringBuilder
                {
                    public void Append(string value)
                    {
                    }
                }

                public sealed class C
                {
                    public void M(string name)
                    {
                        var builder = new StringBuilder();
                        builder.Append(name);
                    }
                }
            }
            """);
}
