// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using VerifyRecord = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.RecordAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the record rules (SST1800 sealing and SST1801 positional-parameter casing).</summary>
public class RecordAnalyzerUnitTest
{
    /// <summary>The <c>init</c>-accessor polyfill positional records require on the test reference assemblies.</summary>
    private const string IsExternalInit = """

        namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
        """;

    /// <summary>Verifies a record class that is neither sealed nor abstract is reported (SST1800, force-enabled here).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsealedRecordClassReportedAsync()
        => await VerifyRecord.VerifyAnalyzerAsync("public record {|SST1800:Animal|};");

    /// <summary>Verifies sealed and abstract record classes are not reported by SST1800.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SealedAndAbstractRecordClassesAreCleanAsync()
        => await VerifyRecord.VerifyAnalyzerAsync(
            """
            public sealed record Cat;
            public abstract record Shape;
            """);

    /// <summary>Verifies camelCase positional record parameters are reported (SST1801, default PascalCase).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LowercasePositionalParametersReportedAsync()
        => await VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            public sealed record Point(int {|SST1801:x|}, int {|SST1801:y|});{{IsExternalInit}}
            """);

    /// <summary>Verifies PascalCase positional record parameters are not reported by SST1801.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PascalCasePositionalParametersAreCleanAsync()
        => await VerifyRecord.VerifyAnalyzerAsync(
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
                       """
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
    public async Task PascalCaseFastPathAcceptsCompliantNames(string name)
        => await Assert.That(RecordAnalyzer.IsPascalCaseFastPathCompliant(name)).IsTrue();

    /// <summary>Verifies the specialized PascalCase fast path rejects non-compliant positional parameter names.</summary>
    /// <param name="name">The candidate positional parameter name.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("")]
    [Arguments("value")]
    [Arguments("1Value")]
    public async Task PascalCaseFastPathRejectsNonCompliantNames(string name)
        => await Assert.That(RecordAnalyzer.IsPascalCaseFastPathCompliant(name)).IsFalse();

    /// <summary>Verifies the clean PascalCase fast path short-circuits without needing later naming scans.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PositionalParameterNamingSkipsCleanPascalCaseFastPathAsync()
        => await Assert.That(RecordAnalyzer.ShouldReportPositionalParameterNaming("Value", NamingConvention.PascalCase)).IsFalse();

    /// <summary>Verifies underscore placeholders remain exempt from positional-parameter naming diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PositionalParameterNamingSkipsAllUnderscoresAsync()
        => await Assert.That(RecordAnalyzer.ShouldReportPositionalParameterNaming("___", NamingConvention.PascalCase)).IsFalse();

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
    [Test]
    public async Task MutableRecordStructIsCleanAsync()
        => await VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            public record struct Scan
            {
                public bool Found { get; set; }
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a writable field keeps a record struct out of SST1803.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordStructWithWritableFieldIsCleanAsync()
        => await VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            public record struct Scan
            {
                public int Count;
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a positional record struct written by its declaring type keeps its mutability.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PositionalRecordStructWrittenByItsOwnTypeIsCleanAsync()
        => await VerifyRecord.VerifyAnalyzerAsync(
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

    /// <summary>Verifies an untouched positional record struct is still asked to become readonly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PositionalRecordStructLeftAloneStillReportsAsync()
        => await VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            internal sealed class Scanner
            {
                internal bool Run() => new Scan(true).Found;

                private record struct {|SST1803:Scan|}(bool Found);
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a record struct with only readonly members is still asked to become readonly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImmutableRecordStructStillReportsAsync()
        => await VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            public record struct {|SST1803:Scan|}
            {
                public bool Found { get; init; }
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a settable property on a record class is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordClassSetAccessorStillReportsAsync()
        => await VerifyRecord.VerifyAnalyzerAsync(
            $$"""
            public sealed record Scan
            {
                public bool Found { get; {|SST1802:set|}; }
            }{{IsExternalInit}}
            """);

    /// <summary>Parses a single property declaration for helper-level tests.</summary>
    /// <param name="source">The property declaration source.</param>
    /// <returns>The parsed property declaration.</returns>
    private static PropertyDeclarationSyntax ParseProperty(string source)
        => (PropertyDeclarationSyntax)SyntaxFactory.ParseCompilationUnit($$"""public sealed record Person { {{source}} }""")
            .Members[0]
            .ChildNodes()
            .Single();
}
