// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using VerifyRecord = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.RecordAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the record rules (SST1800 sealing and SST1801 positional-parameter casing).</summary>
public class RecordAnalyzerUnitTest
{
    /// <summary>Verifies static, constant, and readonly fields do not make a record struct mutable.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonWritableFieldsStillAllowReadonlyRecordAsync() =>
        VerifyRecord.VerifyAnalyzerAsync($$"""
            public record struct {|SST1803:Point|}
            {
                public static int Shared;
                public const int Zero = 0;
                public readonly int Value;
                public static int Count { get; set; }
                public int Read() => Value;
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies the rule-specific convention takes precedence and invalid values fall back to the general option.</summary>
    /// <param name="specific">The rule-specific convention.</param>
    /// <param name="general">The general record convention.</param>
    /// <param name="parameters">The parameters with diagnostic markup for the effective convention.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("camel_case", "pascal_case", "int value, int {|SST1801:Other|}")]
    [Arguments("pascal_case", "camel_case", "int Value, int {|SST1801:other|}")]
    [Arguments("invalid", "camel_case", "int value, int {|SST1801:Other|}")]
    [Arguments("invalid", "invalid", "int Value, int {|SST1801:other|}")]
    public async Task SpecificRecordConventionPrecedesGeneralAsync(string specific, string general, string parameters)
    {
        var test = new VerifyRecord.Test
        {
            TestCode = $"public sealed record Point({parameters});{IsExternalInit}",
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $$"""
            root = true
            [*.cs]
            stylesharp.SST1801.record_parameter_naming = {{specific}}
            stylesharp.record_parameter_naming = {{general}}

            """));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies accessor ordering and incomplete accessor lists select only a set accessor.</summary>
    /// <param name="accessors">The accessor list to parse.</param>
    /// <param name="expectedIndex">The set accessor's index, or minus one when absent.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("set; get;", 0)]
    [Arguments("init; get;", -1)]
    [Arguments("set;", 0)]
    [Arguments("get;", -1)]
    [Arguments("", -1)]
    [Arguments("get; init; set;", 2)]
    [Arguments("get; init; get;", -1)]
    public async Task AccessorShapesSelectOnlySetAsync(string accessors, int expectedIndex)
    {
        var property = ParseProperty($"public int Value {{ {accessors} }}");
        var list = property.AccessorList!.Accessors;
        var actual = RecordAnalyzer.TryGetSetAccessorToReport(list);
        await Assert.That(actual).IsEqualTo(expectedIndex < 0 ? null : list[expectedIndex]);
    }

    /// <summary>Verifies prefix mutations retain a positional record struct's writable properties.</summary>
    /// <param name="operation">The prefix write through the record property.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("++scan.Count")]
    [Arguments("--scan.Count")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PrefixMutationKeepsRecordMutableAsync(string operation) =>
        VerifyRecord.VerifyAnalyzerAsync($$"""
            class C
            {
                private record struct Scan(int Count);
                int M() { var scan = new Scan(1); return {{operation}}; }
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies Unicode casing follows uppercase character classification.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnicodePositionalParameterCasingIsRespectedAsync() =>
        VerifyRecord.VerifyAnalyzerAsync($$"""
            public sealed record Point(int Étage, int {|SST1801:étage|});{{IsExternalInit}}
            """);

    /// <summary>The <c>init</c>-accessor polyfill positional records require on the test reference assemblies.</summary>
    private const string IsExternalInit = """

        namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
        """;

    /// <summary>Verifies a record class that is neither sealed nor abstract is reported (SST1800, force-enabled here).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnsealedRecordClassReportedAsync() =>
        VerifyRecord.VerifyAnalyzerAsync("public record {|SST1800:Animal|};");

    /// <summary>Verifies sealed and abstract record classes are not reported by SST1800.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SealedAndAbstractRecordClassesAreCleanAsync() =>
        VerifyRecord.VerifyAnalyzerAsync(
            """
            public sealed record Cat;
            public abstract record Shape;
            """);

    /// <summary>Verifies camelCase positional record parameters are reported (SST1801, default PascalCase).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LowercasePositionalParametersReportedAsync() =>
        VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            public sealed record Point(int {|SST1801:x|}, int {|SST1801:y|});{{IsExternalInit}}
            """);

    /// <summary>Verifies PascalCase positional record parameters are not reported by SST1801.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PascalCasePositionalParametersAreCleanAsync() =>
        VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            public sealed record Point(int X, int Y);{{IsExternalInit}}
            """);

    /// <summary>Verifies an editorconfig override to camel_case flags a PascalCase positional parameter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CamelCaseConfiguredAsync()
    {
        var test = new VerifyRecord.Test
        {
            TestCode = $$"""
                       public sealed record Point(int {|SST1801:X|}, int y);{{IsExternalInit}}
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.record_parameter_naming = camel_case

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the specialized PascalCase fast path accepts compliant positional parameter names.</summary>
    /// <param name="name">The candidate positional parameter name.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Value")]
    [Arguments("X")]
    public async Task PascalCaseFastPathAcceptsCompliantNames(string name) =>
        await Assert.That(RecordAnalyzer.IsPascalCaseFastPathCompliant(name)).IsTrue();

    /// <summary>Verifies the specialized PascalCase fast path rejects non-compliant positional parameter names.</summary>
    /// <param name="name">The candidate positional parameter name.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("")]
    [Arguments("value")]
    [Arguments("1Value")]
    public async Task PascalCaseFastPathRejectsNonCompliantNames(string name) =>
        await Assert.That(RecordAnalyzer.IsPascalCaseFastPathCompliant(name)).IsFalse();

    /// <summary>Verifies the clean PascalCase fast path short-circuits without needing later naming scans.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PositionalParameterNamingSkipsCleanPascalCaseFastPathAsync() =>
        await Assert.That(RecordAnalyzer.ShouldReportPositionalParameterNaming("Value", NamingConvention.PascalCase)).IsFalse();

    /// <summary>Verifies underscore placeholders remain exempt from positional-parameter naming diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PositionalParameterNamingSkipsAllUnderscoresAsync() =>
        await Assert.That(RecordAnalyzer.ShouldReportPositionalParameterNaming("___", NamingConvention.PascalCase)).IsFalse();

    /// <summary>Verifies the clean get/init property shape skips SST1802 without scanning for set accessors.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitOnlyPropertyFastPathSkipsCleanShapeAsync()
    {
        var property = ParseProperty("public string Name { get; init; }");

        await Assert.That(RecordAnalyzer.TryGetSetAccessorToReport(property.AccessorList!.Accessors)).IsNull();
    }

    /// <summary>Verifies a set accessor is selected for SST1802 reporting.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitOnlyPropertyFastPathFindsSetAccessorAsync()
    {
        var property = ParseProperty("public string Name { get; set; }");

        await Assert.That(RecordAnalyzer.TryGetSetAccessorToReport(property.AccessorList!.Accessors)?.Kind())
            .IsEqualTo(SyntaxKind.SetAccessorDeclaration);
    }

    /// <summary>Verifies a mutable record struct keeps both its set accessor and its mutability.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MutableRecordStructIsCleanAsync() =>
        VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            public record struct Scan
            {
                public bool Found { get; set; }
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a writable field keeps a record struct out of SST1803.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RecordStructWithWritableFieldIsCleanAsync() =>
        VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            public record struct Scan
            {
                public int Count;
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a positional record struct written by its declaring type keeps its mutability.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PositionalRecordStructWrittenByItsOwnTypeIsCleanAsync() =>
        VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            internal sealed class Scanner
            {
                internal bool Run()
                {
                    var scan = new Scan(false);
                    scan.Found = true;
                    return scan.Found;
                }

                private record struct Scan(bool Found);
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a positional record struct stepped with <c>--</c> keeps its mutability.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PositionalRecordStructSteppedByItsOwnTypeIsCleanAsync() =>
        VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            internal sealed class Scanner
            {
                internal int Run()
                {
                    var scan = new Scan(2);
                    scan.Remaining--;
                    return scan.Remaining;
                }

                private record struct Scan(int Remaining);
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies an untouched positional record struct is still asked to become readonly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PositionalRecordStructLeftAloneStillReportsAsync() =>
        VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            internal sealed class Scanner
            {
                internal bool Run() => new Scan(true).Found;

                private record struct {|SST1803:Scan|}(bool Found);
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a record struct with only readonly members is still asked to become readonly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImmutableRecordStructStillReportsAsync() =>
        VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            public record struct {|SST1803:Scan|}
            {
                public bool Found { get; init; }
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a settable property on a record class is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RecordClassSetAccessorStillReportsAsync() =>
        VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            public sealed record Scan
            {
                public bool Found { get; {|SST1802:set|}; }
            }{{IsExternalInit}}
            """);

    /// <summary>Parses a single property declaration for helper-level tests.</summary>
    /// <param name="source">The property declaration source.</param>
    /// <returns>The parsed property declaration.</returns>
    private static PropertyDeclarationSyntax ParseProperty(string source) =>
        (PropertyDeclarationSyntax)SyntaxFactory.ParseCompilationUnit($$"""public sealed record Person { {{source}} }""")
            .Members[0]
            .ChildNodes()
            .Single();
}
